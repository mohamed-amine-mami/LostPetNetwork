using System;
using System.Threading.Tasks;
using LostPetNetwork.Services.Auth;
using LostPetNetwork.Services.Supabase;
using UnityEngine;

namespace LostPetNetwork.Services.Profile
{
    [Serializable]
    public class UserProfile
    {
        public string id;
        public string display_name;
        public string avatar_url;
        public string phone;
        public string city;
        public string created_at;
        public string updated_at;
    }

    [Serializable]
    class ProfileUpdateDto
    {
        public string display_name;
        public string city;
        public string phone;
    }

    public readonly struct ProfileResult
    {
        public readonly UserProfile Profile;
        public readonly string Error;
        public bool Ok => Profile != null;
        public ProfileResult(UserProfile profile, string error) { Profile = profile; Error = error; }
    }

    /// <summary>Reads/updates the signed-in user's row in public.profiles (protected by RLS).</summary>
    public sealed class ProfileService
    {
        static ProfileService _instance;
        public static ProfileService Instance => _instance ??= new ProfileService();

        /// <summary>Last profile loaded from Supabase (cleared on sign-out).</summary>
        public UserProfile Cached { get; private set; }

        ProfileService()
        {
            AuthService.Instance.SessionChanged += session => { if (session == null) Cached = null; };
        }

        public async Task<ProfileResult> GetMyProfileAsync()
        {
            string token = await AuthService.Instance.GetAccessTokenAsync();
            if (token == null) return new ProfileResult(null, "Please sign in again.");

            string url = $"{SupabaseConfig.RestUrl}/profiles?id=eq.{Uri.EscapeDataString(AuthService.Instance.UserId)}&select=*";
            var resp = await SupabaseHttp.SendAsync("GET", url, accessToken: token, singleObject: true);
            if (!resp.IsSuccess) return new ProfileResult(null, SupabaseHttp.FriendlyError(resp));
            try { Cached = JsonUtility.FromJson<UserProfile>(resp.Body); return new ProfileResult(Cached, null); }
            catch { return new ProfileResult(null, "Couldn't read your profile."); }
        }

        public async Task<ProfileResult> UpdateMyProfileAsync(string displayName, string city, string phone)
        {
            string token = await AuthService.Instance.GetAccessTokenAsync();
            if (token == null) return new ProfileResult(null, "Please sign in again.");

            string body = JsonUtility.ToJson(new ProfileUpdateDto { display_name = displayName, city = city, phone = phone });
            string url = $"{SupabaseConfig.RestUrl}/profiles?id=eq.{Uri.EscapeDataString(AuthService.Instance.UserId)}";
            var resp = await SupabaseHttp.SendAsync("PATCH", url, body, token, "return=representation", singleObject: true);
            if (!resp.IsSuccess) return new ProfileResult(null, SupabaseHttp.FriendlyError(resp));
            try { Cached = JsonUtility.FromJson<UserProfile>(resp.Body); return new ProfileResult(Cached, null); }
            catch { return new ProfileResult(null, "Couldn't read your profile."); }
        }
    }
}
