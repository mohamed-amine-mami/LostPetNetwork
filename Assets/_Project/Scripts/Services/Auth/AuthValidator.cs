using System.Text.RegularExpressions;

namespace LostPetNetwork.Services.Auth
{
    /// <summary>Client-side form checks. The server remains the source of truth.</summary>
    public static class AuthValidator
    {
        static readonly Regex EmailPattern = new Regex(@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$", RegexOptions.Compiled);

        public const int MinPasswordLength = 8;

        /// <returns>null when valid, otherwise a user-facing message.</returns>
        public static string ValidateEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return "Enter your email address.";
            return EmailPattern.IsMatch(email.Trim()) ? null : "Enter a valid email address.";
        }

        public static string ValidatePasswordForSignIn(string password) =>
            string.IsNullOrEmpty(password) ? "Enter your password." : null;

        public static string ValidateNewPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return "Enter a password.";
            if (password.Length < MinPasswordLength) return $"Use at least {MinPasswordLength} characters.";
            bool hasLetter = false, hasDigit = false;
            foreach (char c in password)
            {
                if (char.IsLetter(c)) hasLetter = true;
                else if (char.IsDigit(c)) hasDigit = true;
            }
            return hasLetter && hasDigit ? null : "Include at least one letter and one number.";
        }

        public static string ValidateDisplayName(string name)
        {
            string trimmed = name?.Trim() ?? string.Empty;
            if (trimmed.Length < 2) return "Enter your name (at least 2 characters).";
            return trimmed.Length > 50 ? "Name must be 50 characters or fewer." : null;
        }

        public static string ValidateResetCode(string code)
        {
            string trimmed = code?.Trim() ?? string.Empty;
            if (trimmed.Length == 0) return "Enter the code from your email.";
            foreach (char c in trimmed) if (!char.IsDigit(c)) return "The code contains digits only.";
            return trimmed.Length >= 6 && trimmed.Length <= 8 ? null : "Enter the full code from your email.";
        }
    }
}
