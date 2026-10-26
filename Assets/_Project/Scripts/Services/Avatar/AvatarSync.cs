using System;
using System.Threading.Tasks;
using LostPetNetwork.Services.Auth;
using LostPetNetwork.Services.Supabase;
using UnityEngine;

namespace LostPetNetwork.Services.Avatar
{
    public readonly struct AvatarSyncResult
    {
        /// <summary>The request succeeded (or there was nothing to do offline).</summary>
        public readonly bool Ok;
        /// <summary>No server call was made (signed out locally or no access token), so nothing was synced.</summary>
        public readonly bool Skipped;
        /// <summary>The avatar stored on the server, or null when the user has none.</summary>
        public readonly AvatarConfig Config;
        public readonly string Error;

        public AvatarSyncResult(bool ok, bool skipped, AvatarConfig config, string error)
        {
            Ok = ok; Skipped = skipped; Config = config; Error = error;
        }

        public static AvatarSyncResult Skip() => new AvatarSyncResult(true, true, null, null);
    }

    [Serializable]
    class AvatarTextDto
    {
        public string avatar_text;
    }

    /// <summary>Keeps the user's avatar settings in their profile row so it follows them to any device.</summary>
    public static class AvatarSync
    {
        static string ProfileUrl => $"{SupabaseConfig.RestUrl}/profiles?id=eq.{Uri.EscapeDataString(AuthService.Instance.UserId ?? string.Empty)}";

        public static async Task<AvatarSyncResult> FetchAsync()
        {
            string token = await AuthService.Instance.GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token)) return AvatarSyncResult.Skip();

            var resp = await SupabaseHttp.SendAsync("GET", ProfileUrl + "&select=avatar_text:avatar::text", accessToken: token, singleObject: true);
            if (!resp.IsSuccess) return new AvatarSyncResult(false, false, null, SupabaseHttp.FriendlyError(resp));
            try
            {
                var dto = JsonUtility.FromJson<AvatarTextDto>(resp.Body);
                AvatarConfig.TryParse(dto?.avatar_text, out var config);
                return new AvatarSyncResult(true, false, config, null);
            }
            catch (ArgumentException)
            {
                return new AvatarSyncResult(false, false, null, "Couldn't read your avatar.");
            }
        }

        public static async Task<AvatarSyncResult> PushAsync(AvatarConfig config)
        {
            string token = await AuthService.Instance.GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token)) return AvatarSyncResult.Skip();

            string body = new JsonBody().Raw("avatar", config.ToJson()).ToString();
            var resp = await SupabaseHttp.SendAsync("PATCH", ProfileUrl, body, token);
            return resp.IsSuccess
                ? new AvatarSyncResult(true, false, config, null)
                : new AvatarSyncResult(false, false, null, SupabaseHttp.FriendlyError(resp));
        }
    }
}
