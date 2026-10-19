using System.Text.RegularExpressions;

namespace LostPetNetwork.Services.Profile
{
    /// <summary>Client-side checks that mirror the database constraints on public.profiles.</summary>
    public static class ProfileValidator
    {
        static readonly Regex PhonePattern = new Regex(@"^[0-9+\s\-()]{6,30}$", RegexOptions.Compiled);

        public static string ValidateDisplayName(string name)
        {
            string t = name?.Trim() ?? string.Empty;
            if (t.Length < 2) return "Enter your name (at least 2 characters).";
            return t.Length > 50 ? "Name must be 50 characters or fewer." : null;
        }

        public static string ValidateCity(string city)
        {
            string t = city?.Trim() ?? string.Empty;
            return t.Length > 80 ? "City must be 80 characters or fewer." : null;
        }

        /// <summary>Phone is optional; when present it must look like a phone number.</summary>
        public static string ValidatePhone(string phone)
        {
            string t = phone?.Trim() ?? string.Empty;
            if (t.Length == 0) return null;
            return PhonePattern.IsMatch(t) ? null : "Enter a valid phone number or leave it empty.";
        }
    }
}
