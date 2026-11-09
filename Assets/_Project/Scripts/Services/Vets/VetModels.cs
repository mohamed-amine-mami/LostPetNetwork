using System.Globalization;
using System.Text;

namespace LostPetNetwork.Services.Vets
{
    /// <summary>A veterinary clinic from the directory.</summary>
    public class VetClinic
    {
        public string Id;
        public string Name;
        public string Address;
        public string City;
        public string Governorate;
        public string Phone;
        public double Latitude, Longitude;
        public string OpeningHours;
        public bool Is24h;
        public string Website;
        public string MapsUrl;
        public bool IsDemo;

        public bool HasPhone => !string.IsNullOrEmpty(Phone);

        /// <summary>Street and town on one line, skipping whatever is missing.</summary>
        public string Place
        {
            get
            {
                var sb = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(Address)) sb.Append(Address.Trim());
                if (!string.IsNullOrWhiteSpace(City) && (sb.Length == 0 || Address.IndexOf(City.Trim(), System.StringComparison.OrdinalIgnoreCase) < 0))
                {
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(City.Trim());
                }
                return sb.ToString();
            }
        }

        public string DirectionsUrl() =>
            "https://www.google.com/maps/dir/?api=1&destination=" +
            Latitude.ToString("0.#####", CultureInfo.InvariantCulture) + "," +
            Longitude.ToString("0.#####", CultureInfo.InvariantCulture);

        /// <summary>The link that opens the phone's dialer, or null when there is no usable number.</summary>
        public string DialUrl()
        {
            string digits = CleanPhone(Phone);
            return digits == null ? null : "tel:" + digits;
        }

        /// <summary>Keeps digits and a leading +; adds Tunisia's +216 to 8-digit local numbers. Null when not a number.</summary>
        public static string CleanPhone(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var sb = new StringBuilder();
            foreach (char c in raw.Trim())
            {
                if (char.IsDigit(c)) sb.Append(c);
                else if (c == '+' && sb.Length == 0) sb.Append(c);
            }
            string s = sb.ToString();
            if (s.StartsWith("00")) s = "+" + s.Substring(2);
            if (s.Length == 8 && !s.StartsWith("+")) s = "+216" + s;
            int digits = s.StartsWith("+") ? s.Length - 1 : s.Length;
            return digits < 6 || digits > 15 ? null : s;
        }
    }
}
