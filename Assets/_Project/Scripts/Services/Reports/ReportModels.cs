using System;
using System.Collections.Generic;

namespace LostPetNetwork.Services.Reports
{
    public enum ReportType { Lost, Found }
    public enum ReportStatus { Active, Reunited }
    public enum PetSpecies { Dog, Cat, Bird, Other }
    public enum WhenOption { JustNow, Today, Yesterday, Earlier }

    /// <summary>A lost/found pet report as shown in the UI. Backend tables for reports do not exist yet.</summary>
    public class PetReport
    {
        public string Id;
        public ReportType Type;
        public ReportStatus Status;
        public PetSpecies Species;
        public string PetName;
        public string Breed;
        public string Description;
        public string LocationText;
        public DateTime ReportedAtUtc;
        public WhenOption When;
        public string OwnerUserId;
        public string OwnerName;
        public int ContactRequests;
        public float DistanceKm;
        /// <summary>Where the pet was seen (0,0 means unknown).</summary>
        public double Latitude, Longitude;
        /// <summary>Normalised 0..1 position used by the stylised map preview.</summary>
        public float MapX, MapY;
        /// <summary>Key of a bundled demo photo ("biscuit", "luna", "max") or null.</summary>
        public string PhotoKey;
        /// <summary>JPEG bytes captured with the in-app camera, or null.</summary>
        public byte[] PhotoBytes;
        /// <summary>JSON of the pet's 3D look (PetConfig), or null when none was designed.</summary>
        public string PetAvatarJson;
        /// <summary>Storage path of the uploaded photo (reports from Supabase), or null.</summary>
        public string PhotoPath;
        /// <summary>True when the report was saved but its photo could not be uploaded.</summary>
        public bool PhotoUploadFailed;
        /// <summary>True for every report served by <see cref="MockReportRepository"/>.</summary>
        public bool IsDemo = true;

        public bool HasLocation => Math.Abs(Latitude) > 0.0001 || Math.Abs(Longitude) > 0.0001;

        /// <summary>Distance from the user in km: real when both positions are known, otherwise the sample value.</summary>
        public float DistanceFromKm(Services.Sensors.GeoFix? user, out bool isReal)
        {
            isReal = user.HasValue && HasLocation;
            if (!isReal) return DistanceKm;
            return (float)(Services.Sensors.GeoMath.DistanceMeters(user.Value.Latitude, user.Value.Longitude, Latitude, Longitude) / 1000.0);
        }

        public string DisplayName => string.IsNullOrWhiteSpace(PetName)
            ? (Type == ReportType.Lost ? "Missing " : "Found ") + Species.ToString().ToLowerInvariant()
            : PetName.Trim();

        public string Summary
        {
            get
            {
                string species = Species.ToString();
                return string.IsNullOrWhiteSpace(Breed) ? species : species + " · " + Breed.Trim();
            }
        }

        public string TimeAgo(DateTime nowUtc)
        {
            var span = nowUtc - ReportedAtUtc;
            if (span.TotalMinutes < 2) return "just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            if (span.TotalHours < 48) return "yesterday";
            return $"{(int)span.TotalDays} days ago";
        }
    }

    /// <summary>The report form's data while the user is filling it in.</summary>
    public class ReportDraft
    {
        public ReportType Type = ReportType.Lost;
        public PetSpecies? Species = PetSpecies.Dog;
        public string PetName = string.Empty;
        public string Description = string.Empty;
        public string LocationText = string.Empty;
        public WhenOption When = WhenOption.JustNow;
        public byte[] PhotoBytes;
        public string PetAvatarJson;
        /// <summary>Coordinates of the spot, when the user chose "Use my current location".</summary>
        public double? Latitude, Longitude;
        /// <summary>The place name that was filled in for those coordinates; editing the text discards them.</summary>
        public string GeoLabel;

        public const string LocationKey = "location";
        public const string SpeciesKey = "species";
        public const string NameKey = "name";
        public const string DescriptionKey = "description";

        /// <summary>Returns field-key → message for every invalid field (empty when valid).</summary>
        public Dictionary<string, string> Validate()
        {
            var errors = new Dictionary<string, string>();
            if (Species == null) errors[SpeciesKey] = "Choose the kind of pet.";
            string loc = LocationText?.Trim() ?? string.Empty;
            if (loc.Length < 3) errors[LocationKey] = "Tell us where the pet was last seen.";
            else if (loc.Length > 120) errors[LocationKey] = "Keep the location under 120 characters.";
            if ((PetName?.Trim().Length ?? 0) > 40) errors[NameKey] = "Name must be 40 characters or fewer.";
            if ((Description?.Trim().Length ?? 0) > 500) errors[DescriptionKey] = "Description must be 500 characters or fewer.";
            return errors;
        }

        public bool HasPhoto => PhotoBytes != null && PhotoBytes.Length > 0;
    }
}
