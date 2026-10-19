using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LostPetNetwork.Services.Reports
{
    /// <summary>
    /// In-memory DEMO data. Nothing here is stored in Supabase and it is lost when the app closes.
    /// The three seeded reports are sample content from the Figma design, owned by a fictional neighbour.
    /// </summary>
    public sealed class MockReportRepository : IReportRepository
    {
        const string SeedOwnerId = "demo-owner";
        readonly List<PetReport> _reports = new List<PetReport>();
        readonly HashSet<string> _contacted = new HashSet<string>();
        readonly int _latencyMs;
        int _nextId = 1;

        public bool IsDemo => true;

        /// <param name="latencyMs">Artificial delay so loading states are exercised (use 0 in tests).</param>
        public MockReportRepository(int latencyMs = 350, bool seed = true)
        {
            _latencyMs = latencyMs;
            if (seed) Seed();
        }

        void Seed()
        {
            var now = DateTime.UtcNow;
            _reports.Add(new PetReport
            {
                Id = "demo-1", Type = ReportType.Lost, Status = ReportStatus.Active, Species = PetSpecies.Dog,
                PetName = "Biscuit", Breed = "Golden Retriever", When = WhenOption.Today,
                Description = "Slipped out of the garden gate around 6:30 PM. Wearing a red collar with a small bell, very friendly and responds to his name.",
                LocationText = "Parc du Belvédère, Tunis", ReportedAtUtc = now.AddHours(-2), OwnerUserId = SeedOwnerId,
                OwnerName = "Sarah K.", ContactRequests = 2, DistanceKm = 1.2f, MapX = 0.48f, MapY = 0.62f, PhotoKey = "biscuit"
            });
            _reports.Add(new PetReport
            {
                Id = "demo-2", Type = ReportType.Found, Status = ReportStatus.Active, Species = PetSpecies.Cat,
                PetName = "Luna", Breed = "Tabby", When = WhenOption.JustNow,
                Description = "Friendly tabby with green eyes and white paws, found near the café terrace. Safe at my place.",
                LocationText = "Lac 2, Tunis", ReportedAtUtc = now.AddMinutes(-40), OwnerUserId = "demo-finder",
                OwnerName = "Karim B.", ContactRequests = 1, DistanceKm = 3.4f, MapX = 0.28f, MapY = 0.4f, PhotoKey = "luna"
            });
            _reports.Add(new PetReport
            {
                Id = "demo-3", Type = ReportType.Lost, Status = ReportStatus.Active, Species = PetSpecies.Dog,
                PetName = "Max", Breed = "Beagle", When = WhenOption.Yesterday,
                Description = "Blue harness, answers to \"Maxou\". Last seen on the seaside promenade.",
                LocationText = "La Marsa Corniche", ReportedAtUtc = now.AddHours(-26), OwnerUserId = "demo-owner-2",
                OwnerName = "Amel T.", ContactRequests = 0, DistanceKm = 8.9f, MapX = 0.7f, MapY = 0.3f, PhotoKey = "max"
            });
        }

        async Task Delay()
        {
            if (_latencyMs > 0) await Task.Delay(_latencyMs);
        }

        public async Task<IReadOnlyList<PetReport>> GetAllAsync()
        {
            await Delay();
            return _reports.OrderByDescending(r => r.ReportedAtUtc).ToList();
        }

        public async Task<IReadOnlyList<PetReport>> GetMineAsync(string userId)
        {
            await Delay();
            return _reports.Where(r => r.OwnerUserId == userId).OrderByDescending(r => r.ReportedAtUtc).ToList();
        }

        public async Task<PetReport> GetAsync(string id)
        {
            await Delay();
            return _reports.FirstOrDefault(r => r.Id == id);
        }

        public async Task<PetReport> CreateAsync(ReportDraft draft, string userId, string ownerName)
        {
            await Delay();
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            if (draft.Validate().Count > 0) throw new ArgumentException("The report is not valid.");

            var now = DateTime.UtcNow;
            int index = _nextId++;
            var report = new PetReport
            {
                Id = "local-" + index,
                Type = draft.Type,
                Status = ReportStatus.Active,
                Species = draft.Species ?? PetSpecies.Other,
                PetName = draft.PetName?.Trim(),
                Description = draft.Description?.Trim(),
                LocationText = draft.LocationText.Trim(),
                When = draft.When,
                ReportedAtUtc = WhenToTime(draft.When, now),
                OwnerUserId = userId,
                OwnerName = string.IsNullOrWhiteSpace(ownerName) ? "You" : ownerName,
                PhotoBytes = draft.PhotoBytes,
                DistanceKm = 0f,
                MapX = 0.2f + (index * 0.17f) % 0.6f,
                MapY = 0.25f + (index * 0.23f) % 0.5f
            };
            _reports.Add(report);
            return report;
        }

        public async Task<PetReport> MarkReunitedAsync(string id)
        {
            await Delay();
            var report = _reports.FirstOrDefault(r => r.Id == id);
            if (report != null) report.Status = ReportStatus.Reunited;
            return report;
        }

        public async Task<bool> RequestContactAsync(string id, string requesterUserId)
        {
            await Delay();
            var report = _reports.FirstOrDefault(r => r.Id == id);
            if (report == null || report.OwnerUserId == requesterUserId) return false;
            if (!_contacted.Add(id + "|" + requesterUserId)) return false; // already requested
            report.ContactRequests++;
            return true;
        }

        static DateTime WhenToTime(WhenOption when, DateTime now)
        {
            switch (when)
            {
                case WhenOption.Today: return now.AddHours(-3);
                case WhenOption.Yesterday: return now.AddHours(-24);
                case WhenOption.Earlier: return now.AddDays(-3);
                default: return now;
            }
        }
    }

    /// <summary>Holds the repository the UI uses. Swap for a Supabase implementation once tables exist.</summary>
    public static class ReportServices
    {
        public static IReportRepository Repository { get; set; } = new MockReportRepository();
    }
}
