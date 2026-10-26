using System;

namespace LostPetNetwork.Services.Sos
{
    public enum SosStatus { Open, Responding, Resolved }

    /// <summary>An emergency alert for an injured, trapped or abandoned animal.</summary>
    public class SosAlert
    {
        public string Id;
        public string ReporterId;
        public string ReporterName;
        public double Latitude, Longitude;
        public string Note;
        public string AudioPath;
        public byte[] AudioBytes;
        public SosStatus Status;
        public string ResponderId;
        public DateTime CreatedAtUtc = DateTime.UtcNow;
        public bool IsDemo;

        public bool IsActive => Status != SosStatus.Resolved;

        public string StatusText => Status == SosStatus.Open ? "Needs help" : Status == SosStatus.Responding ? "Help is on the way" : "Resolved";

        public string TimeAgo(DateTime nowUtc)
        {
            var span = nowUtc - CreatedAtUtc;
            if (span.TotalMinutes < 2) return "just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            return $"{(int)span.TotalDays} days ago";
        }
    }

    /// <summary>What the person fills in when raising an alert.</summary>
    public class SosDraft
    {
        public double Latitude, Longitude;
        public bool HasLocation;
        public string Note = string.Empty;
        public byte[] AudioWav;

        public string Validate()
        {
            if (!HasLocation) return "We need your location so rescuers can find the animal.";
            if ((Note?.Trim().Length ?? 0) > 300) return "Keep the note under 300 characters.";
            return null;
        }
    }
}
