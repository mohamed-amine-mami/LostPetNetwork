using System;
using System.Collections.Generic;
using UnityEngine;

namespace LostPetNetwork.Services.Vets
{
    [Serializable]
    public class VetRow
    {
        public string id;
        public string name;
        public string address;
        public string city;
        public string governorate;
        public string phone;
        public double latitude;
        public double longitude;
        public string opening_hours;
        public bool is_24_7;
        public string website;
        public string maps_url;
    }

    [Serializable]
    class VetRows
    {
        public VetRow[] items;
    }

    /// <summary>Converts vet rows (JSON) into app types. Pure logic, covered by tests.</summary>
    public static class VetMapper
    {
        public const string Select = "id,name,address,city,governorate,phone,latitude,longitude,opening_hours,is_24_7,website,maps_url";

        public static List<VetRow> ParseRows(string jsonArray)
        {
            var result = new List<VetRow>();
            if (string.IsNullOrWhiteSpace(jsonArray)) return result;
            try
            {
                var rows = JsonUtility.FromJson<VetRows>("{\"items\":" + jsonArray + "}");
                if (rows?.items != null) result.AddRange(rows.items);
            }
            catch (ArgumentException) { /* unreadable: treat as empty */ }
            return result;
        }

        static string Clean(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        /// <summary>
        /// The app's fonts have no Arabic letters (they would show as empty boxes), so Arabic script is left out of
        /// what is displayed; the database keeps the full text. The Arabic comma becomes a normal one.
        /// </summary>
        public static string ForDisplay(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (c == '\u060C') { sb.Append(','); continue; }
                bool arabic = (c >= '\u0600' && c <= '\u06FF') || (c >= '\u0750' && c <= '\u077F') ||
                              (c >= '\uFB50' && c <= '\uFDFF') || (c >= '\uFE70' && c <= '\uFEFF');
                if (!arabic) sb.Append(c);
            }
            string cleaned = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s{2,}", " ")
                .Replace(" ,", ",").Replace(",,", ",").Trim(' ', ',');
            return cleaned.Length == 0 ? null : cleaned;
        }

        public static VetClinic ToClinic(VetRow r) => new VetClinic
        {
            Id = r.id,
            Name = ForDisplay(r.name) ?? "Veterinary clinic",
            Address = ForDisplay(r.address),
            City = Clean(r.city),
            Governorate = Clean(r.governorate),
            Phone = Clean(r.phone),
            Latitude = r.latitude,
            Longitude = r.longitude,
            OpeningHours = Clean(r.opening_hours),
            Is24h = r.is_24_7,
            Website = Clean(r.website),
            MapsUrl = Clean(r.maps_url)
        };
    }
}
