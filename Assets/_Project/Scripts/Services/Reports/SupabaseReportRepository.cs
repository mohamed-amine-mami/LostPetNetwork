using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LostPetNetwork.Services.Auth;
using LostPetNetwork.Services.Supabase;
using UnityEngine;

namespace LostPetNetwork.Services.Reports
{
    /// <summary>A failure with a message that is safe to show to the user.</summary>
    public class RepositoryException : Exception
    {
        public RepositoryException(string message) : base(message) { }
    }

    [Serializable]
    class ContactRow
    {
        public string report_id;
    }

    [Serializable]
    class ContactRows
    {
        public ContactRow[] items;
    }

    /// <summary>
    /// Reports stored in the Supabase tables (protected by Row Level Security). Photos live in the private
    /// "report-photos" bucket under the owner's folder.
    /// </summary>
    public sealed class SupabaseReportRepository : IReportRepository
    {
        const int MaxPhotoDownloads = 40;
        static readonly Dictionary<string, byte[]> PhotoCache = new Dictionary<string, byte[]>();

        public bool IsDemo => false;

        static string ReportsUrl => SupabaseConfig.RestUrl + "/reports";

        static async Task<string> TokenAsync()
        {
            string token = await AuthService.Instance.GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token)) throw new RepositoryException("Please sign in again.");
            return token;
        }

        public Task<IReadOnlyList<PetReport>> GetAllAsync() => LoadAsync(string.Empty);

        public Task<IReadOnlyList<PetReport>> GetMineAsync(string userId) =>
            LoadAsync("&owner_id=eq." + Uri.EscapeDataString(userId ?? string.Empty));

        async Task<IReadOnlyList<PetReport>> LoadAsync(string filter)
        {
            string token = await TokenAsync();
            var resp = await SupabaseHttp.SendAsync("GET",
                $"{ReportsUrl}?select={ReportMapper.Select}&order=created_at.desc&limit=200{filter}", accessToken: token);
            if (!resp.IsSuccess) throw new RepositoryException(SupabaseHttp.FriendlyError(resp));

            var now = DateTime.UtcNow;
            var reports = ReportMapper.ParseRows(resp.Body).Select(r => ReportMapper.ToReport(r, now)).ToList();
            foreach (var r in reports) ReportMapper.ApplyMapPosition(r);
            await FillContactCountsAsync(reports, token);
            await FillPhotosAsync(reports, token);
            return reports;
        }

        public async Task<PetReport> GetAsync(string id)
        {
            string token = await TokenAsync();
            var resp = await SupabaseHttp.SendAsync("GET",
                $"{ReportsUrl}?id=eq.{Uri.EscapeDataString(id)}&select={ReportMapper.Select}", accessToken: token, singleObject: true);
            if (resp.StatusCode == 406) return null;
            if (!resp.IsSuccess) throw new RepositoryException(SupabaseHttp.FriendlyError(resp));
            if (!ReportMapper.TryParseRow(resp.Body, out var row)) return null;
            var report = ReportMapper.ToReport(row, DateTime.UtcNow);
            ReportMapper.ApplyMapPosition(report);
            var list = new List<PetReport> { report };
            await FillContactCountsAsync(list, token);
            await FillPhotosAsync(list, token);
            return report;
        }

        public async Task<PetReport> CreateAsync(ReportDraft draft, string userId, string ownerName)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            if (draft.Validate().Count > 0) throw new ArgumentException("The report is not valid.");
            string token = await TokenAsync();

            string body = ReportMapper.BuildInsertBody(draft, ownerName, DateTime.UtcNow);
            var resp = await SupabaseHttp.SendAsync("POST", $"{ReportsUrl}?select={ReportMapper.Select}", body, token,
                "return=representation", singleObject: true);
            if (!resp.IsSuccess || !ReportMapper.TryParseRow(resp.Body, out var row))
                throw new RepositoryException(SupabaseHttp.FriendlyError(resp));

            var report = ReportMapper.ToReport(row, DateTime.UtcNow);
            ReportMapper.ApplyMapPosition(report);

            if (draft.HasPhoto)
            {
                string path = $"{report.OwnerUserId}/{report.Id}.jpg";
                var upload = await SupabaseHttp.UploadBytesAsync(
                    $"{SupabaseConfig.StorageUrl}/object/report-photos/{path}", draft.PhotoBytes, "image/jpeg", token);
                if (upload.IsSuccess)
                {
                    string patch = new JsonBody().Str("photo_path", path).ToString();
                    var linked = await SupabaseHttp.SendAsync("PATCH", $"{ReportsUrl}?id=eq.{report.Id}", patch, token);
                    if (linked.IsSuccess)
                    {
                        report.PhotoPath = path;
                        report.PhotoBytes = draft.PhotoBytes;
                        PhotoCache[path] = draft.PhotoBytes;
                    }
                    else report.PhotoUploadFailed = true;
                }
                else report.PhotoUploadFailed = true;
            }
            return report;
        }

        public async Task<PetReport> MarkReunitedAsync(string id)
        {
            string token = await TokenAsync();
            string body = new JsonBody().Str("status", "reunited").ToString();
            var resp = await SupabaseHttp.SendAsync("PATCH",
                $"{ReportsUrl}?id=eq.{Uri.EscapeDataString(id)}&select={ReportMapper.Select}", body, token,
                "return=representation", singleObject: true);
            if (resp.StatusCode == 406) return null; // not the owner: no row matched
            if (!resp.IsSuccess) throw new RepositoryException(SupabaseHttp.FriendlyError(resp));
            if (!ReportMapper.TryParseRow(resp.Body, out var row)) return null;
            var report = ReportMapper.ToReport(row, DateTime.UtcNow);
            ReportMapper.ApplyMapPosition(report);
            var list = new List<PetReport> { report };
            await FillPhotosAsync(list, token);
            return report;
        }

        public async Task<bool> RequestContactAsync(string id, string requesterUserId)
        {
            string token = await TokenAsync();
            string body = new JsonBody().Str("report_id", id).ToString();
            var resp = await SupabaseHttp.SendAsync("POST", SupabaseConfig.RestUrl + "/contact_requests", body, token);
            if (resp.IsSuccess) return true;
            if (resp.StatusCode == 409) return false; // already asked about this report
            if (resp.StatusCode == 403) return false; // own report, or the report is no longer active
            throw new RepositoryException(SupabaseHttp.FriendlyError(resp));
        }

        /// <summary>Owners see how many people asked about their reports; everyone else keeps 0.</summary>
        static async Task FillContactCountsAsync(List<PetReport> reports, string token)
        {
            string me = AuthService.Instance.UserId;
            if (!reports.Any(r => r.OwnerUserId == me)) return;
            var resp = await SupabaseHttp.SendAsync("GET", SupabaseConfig.RestUrl + "/contact_requests?select=report_id", accessToken: token);
            if (!resp.IsSuccess) return;
            try
            {
                var rows = JsonUtility.FromJson<ContactRows>("{\"items\":" + resp.Body + "}");
                if (rows?.items == null) return;
                var counts = rows.items.GroupBy(c => c.report_id).ToDictionary(g => g.Key, g => g.Count());
                foreach (var r in reports)
                    if (r.OwnerUserId == me && counts.TryGetValue(r.Id, out int n)) r.ContactRequests = n;
            }
            catch (ArgumentException)
            {
                // counts stay at 0
            }
        }

        static async Task FillPhotosAsync(List<PetReport> reports, string token)
        {
            var needed = reports.Where(r => !string.IsNullOrEmpty(r.PhotoPath)).Take(MaxPhotoDownloads).ToList();
            foreach (var r in needed)
                if (PhotoCache.TryGetValue(r.PhotoPath, out var cached)) r.PhotoBytes = cached;

            var missing = needed.Where(r => r.PhotoBytes == null).ToList();
            if (missing.Count == 0) return;

            using (var gate = new SemaphoreSlim(6))
            {
                await Task.WhenAll(missing.Select(async r =>
                {
                    await gate.WaitAsync();
                    try
                    {
                        var resp = await SupabaseHttp.GetBytesAsync(
                            $"{SupabaseConfig.StorageUrl}/object/authenticated/report-photos/{r.PhotoPath}", token);
                        if (resp.IsSuccess && resp.Data != null && resp.Data.Length > 0)
                        {
                            PhotoCache[r.PhotoPath] = resp.Data;
                            r.PhotoBytes = resp.Data;
                        }
                    }
                    finally { gate.Release(); }
                }));
            }
        }
    }
}
