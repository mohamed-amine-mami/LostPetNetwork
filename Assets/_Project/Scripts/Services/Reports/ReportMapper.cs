using System;
using System.Collections.Generic;
using System.Globalization;
using LostPetNetwork.Services.Supabase;
using UnityEngine;

namespace LostPetNetwork.Services.Reports
{
    [Serializable]
    public class ReportRow
    {
        public string id;
        public string owner_id;
        public string owner_name;
        public string type;
        public string status;
        public string species;
        public string pet_name;
        public string breed;
        public string description;
        public string location_text;
        public double latitude;
        public double longitude;
        public string occurred_at;
        public string created_at;
        public string photo_path;
        public string pet_avatar_text;
    }

    [Serializable]
    class ReportRows
    {
        public ReportRow[] items;
    }

    /// <summary>Converts between database rows (JSON) and the app's report types. Pure logic, covered by tests.</summary>
    public static class ReportMapper
    {
        /// <summary>The columns to ask for; the pet look is cast to text so it arrives as a JSON string.</summary>
        public const string Select =
            "id,owner_id,owner_name,type,status,species,pet_name,breed,description,location_text,latitude,longitude," +
            "occurred_at,created_at,photo_path,pet_avatar_text:pet_avatar::text";

        public static DateTime WhenToTime(WhenOption when, DateTime now)
        {
            switch (when)
            {
                case WhenOption.Today: return now.AddHours(-3);
                case WhenOption.Yesterday: return now.AddHours(-24);
                case WhenOption.Earlier: return now.AddDays(-3);
                default: return now;
            }
        }

        public static List<ReportRow> ParseRows(string jsonArray)
        {
            var result = new List<ReportRow>();
            if (string.IsNullOrWhiteSpace(jsonArray)) return result;
            try
            {
                var rows = JsonUtility.FromJson<ReportRows>("{\"items\":" + jsonArray + "}");
                if (rows?.items != null) result.AddRange(rows.items);
            }
            catch (ArgumentException)
            {
                // unreadable answer: treat as empty
            }
            return result;
        }

        public static bool TryParseRow(string json, out ReportRow row)
        {
            row = null;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try { row = JsonUtility.FromJson<ReportRow>(json); }
            catch (ArgumentException) { return false; }
            return row != null && !string.IsNullOrEmpty(row.id);
        }

        public static PetReport ToReport(ReportRow row, DateTime nowUtc)
        {
            DateTime when = Parse(row.occurred_at) ?? Parse(row.created_at) ?? nowUtc;
            var age = nowUtc - when;
            return new PetReport
            {
                Id = row.id,
                Type = row.type == "found" ? ReportType.Found : ReportType.Lost,
                Status = row.status == "reunited" ? ReportStatus.Reunited : ReportStatus.Active,
                Species = ParseSpecies(row.species),
                PetName = row.pet_name,
                Breed = row.breed,
                Description = row.description,
                LocationText = row.location_text,
                Latitude = row.latitude,
                Longitude = row.longitude,
                ReportedAtUtc = when,
                When = age.TotalHours < 1 ? WhenOption.JustNow : age.TotalHours < 24 ? WhenOption.Today : age.TotalHours < 48 ? WhenOption.Yesterday : WhenOption.Earlier,
                OwnerUserId = row.owner_id,
                OwnerName = row.owner_name,
                PhotoPath = string.IsNullOrEmpty(row.photo_path) ? null : row.photo_path,
                PetAvatarJson = string.IsNullOrWhiteSpace(row.pet_avatar_text) ? null : row.pet_avatar_text,
                IsDemo = false
            };
        }

        /// <summary>Sample 0..1 position for the stylised (token-less) map, from real coordinates around Tunis.</summary>
        public static void ApplyMapPosition(PetReport report)
        {
            if (!report.HasLocation) { report.MapX = 0.5f; report.MapY = 0.5f; return; }
            report.MapX = Mathf.Clamp((float)((report.Longitude - 10.10) / 0.24), 0.06f, 0.94f);
            report.MapY = Mathf.Clamp((float)(1.0 - (report.Latitude - 36.78) / 0.12), 0.1f, 0.9f);
        }

        public static string SpeciesToDb(PetSpecies species) => species.ToString().ToLowerInvariant();

        static PetSpecies ParseSpecies(string value)
        {
            switch (value)
            {
                case "dog": return PetSpecies.Dog;
                case "cat": return PetSpecies.Cat;
                case "bird": return PetSpecies.Bird;
                default: return PetSpecies.Other;
            }
        }

        static DateTime? Parse(string iso) =>
            DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt) ? dt : (DateTime?)null;

        /// <summary>The JSON sent to create a report. Optional text is trimmed and sent as null when empty.</summary>
        public static string BuildInsertBody(ReportDraft draft, string ownerName, DateTime nowUtc)
        {
            var body = new JsonBody()
                .Str("owner_name", string.IsNullOrWhiteSpace(ownerName) ? "Neighbour" : ownerName.Trim())
                .Str("type", draft.Type == ReportType.Found ? "found" : "lost")
                .Str("species", SpeciesToDb(draft.Species ?? PetSpecies.Other))
                .Str("pet_name", NullIfEmpty(draft.PetName))
                .Str("description", NullIfEmpty(draft.Description))
                .Str("location_text", draft.LocationText.Trim())
                .NumOrNull("latitude", draft.Latitude)
                .NumOrNull("longitude", draft.Longitude)
                .Str("occurred_at", WhenToTime(draft.When, nowUtc).ToString("o", CultureInfo.InvariantCulture));
            if (!string.IsNullOrWhiteSpace(draft.PetAvatarJson)) body.Raw("pet_avatar", draft.PetAvatarJson);
            return body.ToString();
        }

        static string NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
