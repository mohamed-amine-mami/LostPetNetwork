using System;
using System.Collections.Generic;

namespace LostPetNetwork.Services.Adoption
{
    public enum HomeType { Apartment, House }
    public enum Level { Low, Medium, High }
    public enum ListingStatus { Available, Adopted }

    /// <summary>A pet offered for adoption.</summary>
    public class AdoptionListing
    {
        public string Id;
        public string OwnerUserId;
        public string OwnerName;
        /// <summary>0 = dog, 1 = cat (same numbering as PetConfig.species).</summary>
        public int Species;
        public string Name;
        public int AgeMonths;
        /// <summary>0 = small, 1 = medium, 2 = large (same numbering as PetConfig.size).</summary>
        public int Size = 1;
        public Level Energy = Level.Medium;
        public bool GoodWithKids = true;
        public bool GoodWithPets = true;
        public bool NeedsGarden;
        public string Description;
        public string LocationText;
        public double Latitude, Longitude;
        public string PetAvatarJson;
        public ListingStatus Status;
        public DateTime CreatedAtUtc = DateTime.UtcNow;
        public bool IsDemo;

        public bool IsCat => Species == 1;
        public bool HasLocation => Math.Abs(Latitude) > 0.0001 || Math.Abs(Longitude) > 0.0001;

        public string AgeText
        {
            get
            {
                if (AgeMonths < 12) return AgeMonths <= 1 ? "1 month" : $"{AgeMonths} months";
                int years = AgeMonths / 12;
                return years == 1 ? "1 year" : $"{years} years";
            }
        }

        public string SizeText => Size == 0 ? "Small" : Size == 2 ? "Large" : "Medium";
        public string SpeciesText => IsCat ? "Cat" : "Dog";
        public string Summary => $"{SpeciesText} · {AgeText} · {SizeText}";
    }

    /// <summary>The adopter's home, used to score how well a pet would fit. Private to the user.</summary>
    public class AdopterProfile
    {
        public HomeType HomeType = HomeType.Apartment;
        public int HomeSizeM2 = 60;
        public bool HasGarden;
        public bool HasKids;
        public bool HasOtherPets;
        public Level Activity = Level.Medium;
    }

    /// <summary>What the owner types in the "List a pet" form.</summary>
    public class AdoptionDraft
    {
        public string Name = string.Empty;
        public int Species;
        public int AgeMonths = 24;
        public int Size = 1;
        public Level Energy = Level.Medium;
        public bool GoodWithKids = true;
        public bool GoodWithPets = true;
        public bool NeedsGarden;
        public string Description = string.Empty;
        public string LocationText = string.Empty;
        public double? Latitude, Longitude;
        public string GeoLabel;
        public string PetAvatarJson;

        public Dictionary<string, string> Validate()
        {
            var errors = new Dictionary<string, string>();
            string name = Name?.Trim() ?? string.Empty;
            if (name.Length < 1) errors["name"] = "Give the pet a name.";
            else if (name.Length > 40) errors["name"] = "Name must be 40 characters or fewer.";
            string loc = LocationText?.Trim() ?? string.Empty;
            if (loc.Length < 3) errors["location"] = "Tell us where the pet is.";
            else if (loc.Length > 120) errors["location"] = "Keep the location under 120 characters.";
            if ((Description?.Trim().Length ?? 0) > 500) errors["description"] = "Description must be 500 characters or fewer.";
            return errors;
        }
    }
}
