using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LostPetNetwork.Services.Auth;
using LostPetNetwork.Services.Reports;
using LostPetNetwork.Services.Supabase;

namespace LostPetNetwork.Services.Sos
{
    /// <summary>SOS alerts in Supabase. The app polls for new alerts; each user can have one open alert at a time.</summary>
    public sealed class SupabaseSosRepository : ISosRepository
    {
        public bool IsDemo => false;

        static string AlertsUrl => SupabaseConfig.RestUrl + "/sos_alerts";

        static async Task<string> TokenAsync()
        {
            string token = await AuthService.Instance.GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token)) throw new RepositoryException("Please sign in again.");
            return token;
        }

        public async Task<IReadOnlyList<SosAlert>> GetActiveAsync()
        {
            string token = await TokenAsync();
            var resp = await SupabaseHttp.SendAsync("GET",
                $"{AlertsUrl}?select={SosMapper.Select}&status=in.(open,responding)&order=created_at.desc&limit=100", accessToken: token);
            if (!resp.IsSuccess) throw new RepositoryException(SupabaseHttp.FriendlyError(resp));
            return SosMapper.ParseRows(resp.Body).Select(SosMapper.ToAlert).ToList();
        }

        public async Task<SosAlert> CreateAsync(SosDraft draft, string reporterId, string reporterName)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            string error = draft.Validate();
            if (error != null) throw new ArgumentException(error);
            string token = await TokenAsync();

            string audioPath = null;
            if (draft.AudioWav != null && draft.AudioWav.Length > 0)
            {
                // The path is chosen up front: the table only lets clients set it when the alert is created.
                string candidate = $"{AuthService.Instance.UserId}/{Guid.NewGuid():N}.wav";
                var upload = await SupabaseHttp.UploadBytesAsync(
                    $"{SupabaseConfig.StorageUrl}/object/sos-audio/{candidate}", draft.AudioWav, "audio/wav", token);
                if (upload.IsSuccess) audioPath = candidate;
            }

            var resp = await SupabaseHttp.SendAsync("POST", $"{AlertsUrl}?select={SosMapper.Select}",
                SosMapper.BuildInsertBody(draft, reporterName, audioPath), token, "return=representation", singleObject: true);
            if (resp.StatusCode == 409)
                throw new RepositoryException("You already have an open alert. Resolve it before sending another.");
            if (!resp.IsSuccess || !SosMapper.TryParseRow(resp.Body, out var row))
                throw new RepositoryException(SupabaseHttp.FriendlyError(resp));
            var alert = SosMapper.ToAlert(row);
            alert.AudioBytes = draft.AudioWav;
            return alert;
        }

        public Task<SosAlert> ClaimAsync(string id, string responderId) =>
            PatchAsync(id, new JsonBody().Str("status", "responding").Str("responder_id", responderId).ToString());

        public Task<SosAlert> ResolveAsync(string id) =>
            PatchAsync(id, new JsonBody().Str("status", "resolved").ToString());

        async Task<SosAlert> PatchAsync(string id, string body)
        {
            string token = await TokenAsync();
            var resp = await SupabaseHttp.SendAsync("PATCH",
                $"{AlertsUrl}?id=eq.{Uri.EscapeDataString(id)}&select={SosMapper.Select}", body, token,
                "return=representation", singleObject: true);
            if (resp.StatusCode == 406) return null; // not allowed or already taken: no row matched
            if (!resp.IsSuccess) throw new RepositoryException(SupabaseHttp.FriendlyError(resp));
            return SosMapper.TryParseRow(resp.Body, out var row) ? SosMapper.ToAlert(row) : null;
        }

        public async Task<byte[]> GetAudioAsync(SosAlert alert)
        {
            if (alert == null || string.IsNullOrEmpty(alert.AudioPath)) return null;
            string token = await TokenAsync();
            var resp = await SupabaseHttp.GetBytesAsync(
                $"{SupabaseConfig.StorageUrl}/object/authenticated/sos-audio/{alert.AudioPath}", token);
            return resp.IsSuccess ? resp.Data : null;
        }
    }
}
