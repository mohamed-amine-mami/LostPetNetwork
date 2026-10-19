using System;
using System.Threading;
using System.Threading.Tasks;
using LostPetNetwork.Services.Supabase;
using UnityEngine;

namespace LostPetNetwork.Services.Auth
{
    /// <summary>
    /// Email/password authentication against the Supabase Auth REST API (GoTrue).
    /// One shared instance; the UI listens to <see cref="SessionChanged"/>.
    /// </summary>
    public sealed class AuthService
    {
        static AuthService _instance;
        public static AuthService Instance => _instance ??= new AuthService();

        const int RefreshMarginSeconds = 60;
        readonly SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);

        public AuthSession Session { get; private set; }
        public bool IsSignedIn => Session != null && Session.IsValid;
        public string UserId => Session?.userId;

        /// <summary>Raised with the new session, or null after sign-out / session loss.</summary>
        public event Action<AuthSession> SessionChanged;

        AuthService() { }

        // ---------------------------------------------------------------- session

        /// <summary>Call once at startup. Loads the stored session and refreshes it if needed.</summary>
        public async Task<bool> RestoreSessionAsync()
        {
            var stored = SessionStore.Load();
            if (stored == null) return false;

            Session = stored;
            if (!Session.IsExpiringWithin(RefreshMarginSeconds)) { SessionChanged?.Invoke(Session); return true; }

            var refreshed = await RefreshAsync();
            return refreshed.Ok;
        }

        /// <summary>Returns a non-expired access token (refreshing if necessary) or null when signed out.</summary>
        public async Task<string> GetAccessTokenAsync()
        {
            if (!IsSignedIn) return null;
            if (Session.IsExpiringWithin(RefreshMarginSeconds))
            {
                var r = await RefreshAsync();
                if (!r.Ok) return null;
            }
            return Session?.accessToken;
        }

        async Task<AuthResult> RefreshAsync()
        {
            await _refreshLock.WaitAsync();
            try
            {
                if (!IsSignedIn) return AuthResult.Fail("Please sign in again.");
                // Another caller may have refreshed while we waited.
                if (!Session.IsExpiringWithin(RefreshMarginSeconds)) return AuthResult.Success();

                string body = JsonUtility.ToJson(new RefreshGrantDto { refresh_token = Session.refreshToken });
                var resp = await SupabaseHttp.SendAsync("POST", SupabaseConfig.AuthUrl + "/token?grant_type=refresh_token", body);

                if (resp.IsSuccess && TryParseSession(resp.Body, Session.email, out var fresh))
                {
                    ApplySession(fresh);
                    return AuthResult.Success();
                }

                // A network blip must not log the user out; only a rejected refresh token does.
                if (resp.NetworkError || resp.StatusCode >= 500)
                    return AuthResult.Fail(SupabaseHttp.FriendlyError(resp));

                ClearSession();
                return AuthResult.Fail(SupabaseHttp.FriendlyError(resp));
            }
            finally { _refreshLock.Release(); }
        }

        // ------------------------------------------------------------- operations

        public async Task<AuthResult> SignUpAsync(string displayName, string email, string password)
        {
            var body = JsonUtility.ToJson(new SignUpBodyDto
            {
                email = email.Trim(),
                password = password,
                data = new SignUpMetaDto { display_name = displayName.Trim() }
            });
            var resp = await SupabaseHttp.SendAsync("POST", SupabaseConfig.AuthUrl + "/signup", body);
            if (!resp.IsSuccess) return AuthResult.Fail(SupabaseHttp.FriendlyError(resp));

            // Email confirmation OFF -> the response already contains a session.
            if (TryParseSession(resp.Body, email.Trim(), out var session))
            {
                ApplySession(session);
                return AuthResult.Success();
            }
            // Email confirmation ON -> user created, no session until the email is confirmed.
            return AuthResult.ConfirmEmail();
        }

        public async Task<AuthResult> SignInAsync(string email, string password)
        {
            var body = JsonUtility.ToJson(new PasswordGrantDto { email = email.Trim(), password = password });
            var resp = await SupabaseHttp.SendAsync("POST", SupabaseConfig.AuthUrl + "/token?grant_type=password", body);
            if (!resp.IsSuccess) return AuthResult.Fail(SupabaseHttp.FriendlyError(resp));
            if (!TryParseSession(resp.Body, email.Trim(), out var session))
                return AuthResult.Fail("Something went wrong. Please try again.");
            ApplySession(session);
            return AuthResult.Success();
        }

        public async Task<AuthResult> SignOutAsync()
        {
            string token = Session?.accessToken;
            // Clear locally first so the user is signed out even if the network call fails.
            ClearSession();
            if (!string.IsNullOrEmpty(token))
                await SupabaseHttp.SendAsync("POST", SupabaseConfig.AuthUrl + "/logout?scope=local", "{}", token);
            return AuthResult.Success();
        }

        /// <summary>Step 1 of password reset: emails the user. Always reports success to avoid revealing which emails exist.</summary>
        public async Task<AuthResult> SendPasswordResetAsync(string email)
        {
            var body = JsonUtility.ToJson(new RecoverBodyDto { email = email.Trim() });
            var resp = await SupabaseHttp.SendAsync("POST", SupabaseConfig.AuthUrl + "/recover", body);
            if (resp.NetworkError || resp.StatusCode == 429 || resp.StatusCode >= 500)
                return AuthResult.Fail(SupabaseHttp.FriendlyError(resp));
            return AuthResult.Success();
        }

        /// <summary>
        /// Step 2: exchange the emailed 6-digit code for a session, then set the new password.
        /// Requires the project's "Reset password" email template to include {{ .Token }}.
        /// </summary>
        public async Task<AuthResult> ResetPasswordWithCodeAsync(string email, string code, string newPassword)
        {
            var verifyBody = JsonUtility.ToJson(new VerifyBodyDto { type = "recovery", email = email.Trim(), token = code.Trim() });
            var verify = await SupabaseHttp.SendAsync("POST", SupabaseConfig.AuthUrl + "/verify", verifyBody);
            if (!verify.IsSuccess || !TryParseSession(verify.Body, email.Trim(), out var session))
                return AuthResult.Fail(SupabaseHttp.FriendlyError(verify));

            var update = await SupabaseHttp.SendAsync("PUT", SupabaseConfig.AuthUrl + "/user",
                JsonUtility.ToJson(new UpdatePasswordDto { password = newPassword }), session.accessToken);
            if (!update.IsSuccess) return AuthResult.Fail(SupabaseHttp.FriendlyError(update));

            ApplySession(session); // the user is signed in after a successful reset
            return AuthResult.Success();
        }

        // ---------------------------------------------------------------- helpers

        void ApplySession(AuthSession session)
        {
            Session = session;
            SessionStore.Save(session);
            SessionChanged?.Invoke(Session);
        }

        void ClearSession()
        {
            Session = null;
            SessionStore.Clear();
            SessionChanged?.Invoke(null);
        }

        static bool TryParseSession(string json, string fallbackEmail, out AuthSession session)
        {
            session = null;
            try
            {
                var dto = JsonUtility.FromJson<TokenResponseDto>(json);
                if (dto == null || string.IsNullOrEmpty(dto.access_token) || string.IsNullOrEmpty(dto.refresh_token)) return false;
                string userId = dto.user?.id;
                if (string.IsNullOrEmpty(userId)) return false;

                long expiresAt = dto.expires_at > 0
                    ? dto.expires_at
                    : DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (dto.expires_in > 0 ? dto.expires_in : 3600);
                session = new AuthSession
                {
                    accessToken = dto.access_token,
                    refreshToken = dto.refresh_token,
                    expiresAtUnix = expiresAt,
                    userId = userId,
                    email = dto.user?.email ?? fallbackEmail
                };
                return true;
            }
            catch { return false; }
        }
    }
}
