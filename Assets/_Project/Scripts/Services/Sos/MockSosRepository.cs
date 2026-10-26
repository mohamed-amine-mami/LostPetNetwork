using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LostPetNetwork.Services.Sos
{
    /// <summary>In-memory demo alerts (labelled "Demo data"). Used signed out and in tests.</summary>
    public sealed class MockSosRepository : ISosRepository
    {
        readonly List<SosAlert> _alerts = new List<SosAlert>();
        readonly int _latencyMs;
        int _next = 1;

        public bool IsDemo => true;

        public MockSosRepository(int latencyMs = 150, bool seed = true)
        {
            _latencyMs = latencyMs;
            if (!seed) return;
            var now = DateTime.UtcNow;
            _alerts.Add(new SosAlert
            {
                Id = "sos-1", ReporterId = "demo-a", ReporterName = "Yassine M.", Latitude = 36.8115, Longitude = 10.1790,
                Note = "Injured dog at the roadside near the market, limping and scared.", Status = SosStatus.Open,
                CreatedAtUtc = now.AddMinutes(-12), IsDemo = true
            });
            _alerts.Add(new SosAlert
            {
                Id = "sos-2", ReporterId = "demo-b", ReporterName = "Ines B.", Latitude = 36.8402, Longitude = 10.2270,
                Note = "Kitten stuck on a balcony ledge.", Status = SosStatus.Responding, ResponderId = "demo-rescuer",
                CreatedAtUtc = now.AddMinutes(-40), IsDemo = true
            });
        }

        async Task Delay()
        {
            if (_latencyMs > 0) await Task.Delay(_latencyMs);
        }

        public async Task<IReadOnlyList<SosAlert>> GetActiveAsync()
        {
            await Delay();
            return _alerts.Where(a => a.IsActive).OrderByDescending(a => a.CreatedAtUtc).ToList();
        }

        public async Task<SosAlert> CreateAsync(SosDraft draft, string reporterId, string reporterName)
        {
            await Delay();
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            string error = draft.Validate();
            if (error != null) throw new ArgumentException(error);
            if (_alerts.Any(a => a.ReporterId == reporterId && a.IsActive))
                throw new Reports.RepositoryException("You already have an open alert. Resolve it before sending another.");
            var alert = new SosAlert
            {
                Id = "local-sos-" + _next++, ReporterId = reporterId, ReporterName = string.IsNullOrWhiteSpace(reporterName) ? "You" : reporterName,
                Latitude = draft.Latitude, Longitude = draft.Longitude, Note = string.IsNullOrWhiteSpace(draft.Note) ? null : draft.Note.Trim(),
                AudioBytes = draft.AudioWav, Status = SosStatus.Open, IsDemo = true
            };
            _alerts.Add(alert);
            return alert;
        }

        public async Task<SosAlert> ClaimAsync(string id, string responderId)
        {
            await Delay();
            var alert = _alerts.FirstOrDefault(a => a.Id == id);
            if (alert == null || alert.Status != SosStatus.Open) return null;
            alert.Status = SosStatus.Responding;
            alert.ResponderId = responderId;
            return alert;
        }

        public async Task<SosAlert> ResolveAsync(string id)
        {
            await Delay();
            var alert = _alerts.FirstOrDefault(a => a.Id == id);
            if (alert != null) alert.Status = SosStatus.Resolved;
            return alert;
        }

        public async Task<byte[]> GetAudioAsync(SosAlert alert)
        {
            await Delay();
            return alert?.AudioBytes;
        }
    }
}
