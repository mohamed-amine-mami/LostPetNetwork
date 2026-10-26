using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LostPetNetwork.Services.Auth;
using LostPetNetwork.Services.Reports;
using LostPetNetwork.Services.Supabase;

namespace LostPetNetwork.Services.Adoption
{
    /// <summary>Adoption listings, requests and the user's home profile in Supabase (protected by Row Level Security).</summary>
    public sealed class SupabaseAdoptionRepository : IAdoptionRepository
    {
        public bool IsDemo => false;

        static string ListingsUrl => SupabaseConfig.RestUrl + "/adoption_listings";

        static async Task<string> TokenAsync()
        {
            string token = await AuthService.Instance.GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token)) throw new RepositoryException("Please sign in again.");
            return token;
        }

        public Task<IReadOnlyList<AdoptionListing>> GetAvailableAsync() => LoadAsync("&status=eq.available");

        public Task<IReadOnlyList<AdoptionListing>> GetMineAsync(string userId) =>
            LoadAsync("&owner_id=eq." + Uri.EscapeDataString(userId ?? string.Empty));

        async Task<IReadOnlyList<AdoptionListing>> LoadAsync(string filter)
        {
            string token = await TokenAsync();
            var resp = await SupabaseHttp.SendAsync("GET",
                $"{ListingsUrl}?select={AdoptionMapper.Select}&order=created_at.desc&limit=100{filter}", accessToken: token);
            if (!resp.IsSuccess) throw new RepositoryException(SupabaseHttp.FriendlyError(resp));
            return AdoptionMapper.ParseRows(resp.Body).Select(AdoptionMapper.ToListing).ToList();
        }

        public async Task<AdoptionListing> CreateAsync(AdoptionDraft draft, string userId, string ownerName)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            if (draft.Validate().Count > 0) throw new ArgumentException("The listing is not valid.");
            string token = await TokenAsync();
            var resp = await SupabaseHttp.SendAsync("POST", $"{ListingsUrl}?select={AdoptionMapper.Select}",
                AdoptionMapper.BuildInsertBody(draft, ownerName), token, "return=representation", singleObject: true);
            if (!resp.IsSuccess || !AdoptionMapper.TryParseRow(resp.Body, out var row))
                throw new RepositoryException(SupabaseHttp.FriendlyError(resp));
            return AdoptionMapper.ToListing(row);
        }

        public async Task<AdoptionListing> MarkAdoptedAsync(string id)
        {
            string token = await TokenAsync();
            var resp = await SupabaseHttp.SendAsync("PATCH",
                $"{ListingsUrl}?id=eq.{Uri.EscapeDataString(id)}&select={AdoptionMapper.Select}",
                new JsonBody().Str("status", "adopted").ToString(), token, "return=representation", singleObject: true);
            if (resp.StatusCode == 406) return null;
            if (!resp.IsSuccess) throw new RepositoryException(SupabaseHttp.FriendlyError(resp));
            return AdoptionMapper.TryParseRow(resp.Body, out var row) ? AdoptionMapper.ToListing(row) : null;
        }

        public async Task<bool> RequestAsync(string listingId, string message)
        {
            string token = await TokenAsync();
            string body = new JsonBody().Str("listing_id", listingId)
                .Str("message", string.IsNullOrWhiteSpace(message) ? null : message.Trim()).ToString();
            var resp = await SupabaseHttp.SendAsync("POST", SupabaseConfig.RestUrl + "/adoption_requests", body, token);
            if (resp.IsSuccess) return true;
            if (resp.StatusCode == 409 || resp.StatusCode == 403) return false;
            throw new RepositoryException(SupabaseHttp.FriendlyError(resp));
        }

        public async Task<AdopterProfile> GetMyHomeAsync()
        {
            string token = await TokenAsync();
            string url = $"{SupabaseConfig.RestUrl}/adopter_profiles?user_id=eq.{Uri.EscapeDataString(AuthService.Instance.UserId)}&select={AdoptionMapper.HomeSelect}";
            var resp = await SupabaseHttp.SendAsync("GET", url, accessToken: token, singleObject: true);
            if (resp.StatusCode == 406) return null;
            if (!resp.IsSuccess) throw new RepositoryException(SupabaseHttp.FriendlyError(resp));
            return AdoptionMapper.TryParseHome(resp.Body, out var home) ? home : null;
        }

        public async Task SaveMyHomeAsync(AdopterProfile home)
        {
            string token = await TokenAsync();
            var resp = await SupabaseHttp.SendAsync("POST", SupabaseConfig.RestUrl + "/adopter_profiles?on_conflict=user_id",
                AdoptionMapper.BuildHomeBody(home), token, "resolution=merge-duplicates,return=minimal");
            if (!resp.IsSuccess) throw new RepositoryException(SupabaseHttp.FriendlyError(resp));
        }
    }
}
