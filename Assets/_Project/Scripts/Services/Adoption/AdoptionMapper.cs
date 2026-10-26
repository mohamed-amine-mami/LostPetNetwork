using System;
using System.Collections.Generic;
using System.Globalization;
using LostPetNetwork.Services.Pets;
using LostPetNetwork.Services.Supabase;
using UnityEngine;

namespace LostPetNetwork.Services.Adoption
{
    [Serializable]
    public class ListingRow
    {
        public string id;
        public string owner_id;
        public string owner_name;
        public string species;
        public string name;
        public int age_months;
        public string size;
        public string energy;
        public bool good_with_kids;
        public bool good_with_pets;
        public bool needs_garden;
        public string description;
        public string location_text;
        public double latitude;
        public double longitude;
        public string status;
        public string created_at;
        public string pet_avatar_text;
    }

    [Serializable]
    class ListingRows
    {
        public ListingRow[] items;
    }

    [Serializable]
    public class HomeRow
    {
        public string home_type;
        public int home_size_m2;
        public bool has_garden;
        public bool has_kids;
        public bool has_other_pets;
        public string activity;
    }

    /// <summary>Converts between adoption rows (JSON) and app types. Pure logic, covered by tests.</summary>
    public static class AdoptionMapper
    {
        public const string Select =
            "id,owner_id,owner_name,species,name,age_months,size,energy,good_with_kids,good_with_pets,needs_garden," +
            "description,location_text,latitude,longitude,status,created_at,pet_avatar_text:pet_avatar::text";

        public const string HomeSelect = "home_type,home_size_m2,has_garden,has_kids,has_other_pets,activity";

        public static string SizeToDb(int size) => size == 0 ? "small" : size == 2 ? "large" : "medium";
        public static string LevelToDb(Level level) => level.ToString().ToLowerInvariant();

        static int SizeFromDb(string s) => s == "small" ? 0 : s == "large" ? 2 : 1;

        static Level LevelFromDb(string s) => s == "low" ? Level.Low : s == "high" ? Level.High : Level.Medium;

        public static List<ListingRow> ParseRows(string jsonArray)
        {
            var result = new List<ListingRow>();
            if (string.IsNullOrWhiteSpace(jsonArray)) return result;
            try
            {
                var rows = JsonUtility.FromJson<ListingRows>("{\"items\":" + jsonArray + "}");
                if (rows?.items != null) result.AddRange(rows.items);
            }
            catch (ArgumentException) { /* unreadable: treat as empty */ }
            return result;
        }

        public static bool TryParseRow(string json, out ListingRow row)
        {
            row = null;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try { row = JsonUtility.FromJson<ListingRow>(json); }
            catch (ArgumentException) { return false; }
            return row != null && !string.IsNullOrEmpty(row.id);
        }

        public static AdoptionListing ToListing(ListingRow r) => new AdoptionListing
        {
            Id = r.id,
            OwnerUserId = r.owner_id,
            OwnerName = r.owner_name,
            Species = r.species == "cat" ? 1 : 0,
            Name = r.name,
            AgeMonths = r.age_months,
            Size = SizeFromDb(r.size),
            Energy = LevelFromDb(r.energy),
            GoodWithKids = r.good_with_kids,
            GoodWithPets = r.good_with_pets,
            NeedsGarden = r.needs_garden,
            Description = r.description,
            LocationText = r.location_text,
            Latitude = r.latitude,
            Longitude = r.longitude,
            Status = r.status == "adopted" ? ListingStatus.Adopted : ListingStatus.Available,
            CreatedAtUtc = DateTime.TryParse(r.created_at, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt) ? dt : DateTime.UtcNow,
            PetAvatarJson = string.IsNullOrWhiteSpace(r.pet_avatar_text) ? null : r.pet_avatar_text,
            IsDemo = false
        };

        /// <summary>A look to show when the owner did not design one: the right species and size, default colours.</summary>
        public static string DefaultLookJson(AdoptionDraft d) => new PetConfig { species = d.Species, size = d.Size }.ToJson();

        public static AdoptionListing FromDraft(AdoptionDraft d, string userId, string ownerName, string id) => new AdoptionListing
        {
            Id = id,
            OwnerUserId = userId,
            OwnerName = string.IsNullOrWhiteSpace(ownerName) ? "You" : ownerName,
            Species = d.Species,
            Name = d.Name.Trim(),
            AgeMonths = d.AgeMonths,
            Size = d.Size,
            Energy = d.Energy,
            GoodWithKids = d.GoodWithKids,
            GoodWithPets = d.GoodWithPets,
            NeedsGarden = d.NeedsGarden,
            Description = d.Description?.Trim(),
            LocationText = d.LocationText.Trim(),
            Latitude = d.Latitude ?? 36.8065,
            Longitude = d.Longitude ?? 10.1815,
            PetAvatarJson = string.IsNullOrWhiteSpace(d.PetAvatarJson) ? DefaultLookJson(d) : d.PetAvatarJson
        };

        public static string BuildInsertBody(AdoptionDraft d, string ownerName)
        {
            string look = string.IsNullOrWhiteSpace(d.PetAvatarJson) ? DefaultLookJson(d) : d.PetAvatarJson;
            return new JsonBody()
                .Str("owner_name", string.IsNullOrWhiteSpace(ownerName) ? "Neighbour" : ownerName.Trim())
                .Str("species", d.Species == 1 ? "cat" : "dog")
                .Str("name", d.Name.Trim())
                .Num("age_months", d.AgeMonths)
                .Str("size", SizeToDb(d.Size))
                .Str("energy", LevelToDb(d.Energy))
                .Bool("good_with_kids", d.GoodWithKids)
                .Bool("good_with_pets", d.GoodWithPets)
                .Bool("needs_garden", d.NeedsGarden)
                .Str("description", string.IsNullOrWhiteSpace(d.Description) ? null : d.Description.Trim())
                .Str("location_text", d.LocationText.Trim())
                .NumOrNull("latitude", d.Latitude)
                .NumOrNull("longitude", d.Longitude)
                .Raw("pet_avatar", look)
                .ToString();
        }

        public static string BuildHomeBody(AdopterProfile h) => new JsonBody()
            .Str("home_type", h.HomeType == HomeType.House ? "house" : "apartment")
            .Num("home_size_m2", h.HomeSizeM2)
            .Bool("has_garden", h.HasGarden)
            .Bool("has_kids", h.HasKids)
            .Bool("has_other_pets", h.HasOtherPets)
            .Str("activity", LevelToDb(h.Activity))
            .ToString();

        public static bool TryParseHome(string json, out AdopterProfile home)
        {
            home = null;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                var row = JsonUtility.FromJson<HomeRow>(json);
                if (row == null || string.IsNullOrEmpty(row.home_type)) return false;
                home = new AdopterProfile
                {
                    HomeType = row.home_type == "house" ? HomeType.House : HomeType.Apartment,
                    HomeSizeM2 = row.home_size_m2,
                    HasGarden = row.has_garden,
                    HasKids = row.has_kids,
                    HasOtherPets = row.has_other_pets,
                    Activity = LevelFromDb(row.activity)
                };
                return true;
            }
            catch (ArgumentException) { return false; }
        }
    }
}
