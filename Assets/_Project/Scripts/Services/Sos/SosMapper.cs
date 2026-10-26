using System;
using System.Collections.Generic;
using System.Globalization;
using LostPetNetwork.Services.Supabase;
using UnityEngine;

namespace LostPetNetwork.Services.Sos
{
    [Serializable]
    public class SosRow
    {
        public string id;
        public string reporter_id;
        public string reporter_name;
        public double latitude;
        public double longitude;
        public string note;
        public string audio_path;
        public string status;
        public string responder_id;
        public string created_at;
    }

    [Serializable]
    class SosRows
    {
        public SosRow[] items;
    }

    /// <summary>Converts between SOS rows (JSON) and app types. Pure logic, covered by tests.</summary>
    public static class SosMapper
    {
        public const string Select = "id,reporter_id,reporter_name,latitude,longitude,note,audio_path,status,responder_id,created_at";

        public static List<SosRow> ParseRows(string jsonArray)
        {
            var result = new List<SosRow>();
            if (string.IsNullOrWhiteSpace(jsonArray)) return result;
            try
            {
                var rows = JsonUtility.FromJson<SosRows>("{\"items\":" + jsonArray + "}");
                if (rows?.items != null) result.AddRange(rows.items);
            }
            catch (ArgumentException) { /* unreadable: treat as empty */ }
            return result;
        }

        public static bool TryParseRow(string json, out SosRow row)
        {
            row = null;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try { row = JsonUtility.FromJson<SosRow>(json); }
            catch (ArgumentException) { return false; }
            return row != null && !string.IsNullOrEmpty(row.id);
        }

        public static SosAlert ToAlert(SosRow r) => new SosAlert
        {
            Id = r.id,
            ReporterId = r.reporter_id,
            ReporterName = r.reporter_name,
            Latitude = r.latitude,
            Longitude = r.longitude,
            Note = string.IsNullOrWhiteSpace(r.note) ? null : r.note,
            AudioPath = string.IsNullOrEmpty(r.audio_path) ? null : r.audio_path,
            Status = r.status == "resolved" ? SosStatus.Resolved : r.status == "responding" ? SosStatus.Responding : SosStatus.Open,
            ResponderId = string.IsNullOrEmpty(r.responder_id) ? null : r.responder_id,
            CreatedAtUtc = DateTime.TryParse(r.created_at, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt) ? dt : DateTime.UtcNow
        };

        public static string BuildInsertBody(SosDraft d, string reporterName, string audioPath) => new JsonBody()
            .Str("reporter_name", string.IsNullOrWhiteSpace(reporterName) ? "Neighbour" : reporterName.Trim())
            .Num("latitude", d.Latitude)
            .Num("longitude", d.Longitude)
            .Str("note", string.IsNullOrWhiteSpace(d.Note) ? null : d.Note.Trim())
            .Str("audio_path", audioPath)
            .ToString();

        public static string StatusToDb(SosStatus s) => s == SosStatus.Open ? "open" : s == SosStatus.Responding ? "responding" : "resolved";
    }
}
