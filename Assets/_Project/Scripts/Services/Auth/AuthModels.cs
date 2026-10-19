using System;

namespace LostPetNetwork.Services.Auth
{
    /// <summary>The signed-in user's tokens. Passwords are never stored anywhere.</summary>
    [Serializable]
    public class AuthSession
    {
        public string accessToken;
        public string refreshToken;
        public long expiresAtUnix;
        public string userId;
        public string email;

        public bool IsValid => !string.IsNullOrEmpty(refreshToken) && !string.IsNullOrEmpty(userId);

        public bool IsExpiringWithin(int seconds) =>
            DateTimeOffset.UtcNow.ToUnixTimeSeconds() + seconds >= expiresAtUnix;
    }

    public readonly struct AuthResult
    {
        public readonly bool Ok;
        public readonly string Error;
        /// <summary>True when sign-up succeeded but the user must confirm their email before signing in.</summary>
        public readonly bool NeedsEmailConfirmation;

        AuthResult(bool ok, string error, bool needsConfirm)
        {
            Ok = ok;
            Error = error;
            NeedsEmailConfirmation = needsConfirm;
        }

        public static AuthResult Success() => new AuthResult(true, null, false);
        public static AuthResult ConfirmEmail() => new AuthResult(true, null, true);
        public static AuthResult Fail(string error) => new AuthResult(false, error, false);
    }

    // --- JSON DTOs (UnityEngine.JsonUtility) ---

    [Serializable]
    internal class UserDto
    {
        public string id;
        public string email;
    }

    [Serializable]
    internal class TokenResponseDto
    {
        public string access_token;
        public string refresh_token;
        public long expires_in;
        public long expires_at;
        public UserDto user;
        // A sign-up that requires email confirmation returns the user fields at the top level.
        public string id;
        public string email;
    }

    [Serializable] internal class SignUpMetaDto { public string display_name; }
    [Serializable] internal class SignUpBodyDto { public string email; public string password; public SignUpMetaDto data; }
    [Serializable] internal class PasswordGrantDto { public string email; public string password; }
    [Serializable] internal class RefreshGrantDto { public string refresh_token; }
    [Serializable] internal class RecoverBodyDto { public string email; }
    [Serializable] internal class VerifyBodyDto { public string type; public string email; public string token; }
    [Serializable] internal class UpdatePasswordDto { public string password; }
}
