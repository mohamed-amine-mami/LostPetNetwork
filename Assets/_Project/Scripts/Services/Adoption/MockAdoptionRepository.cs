using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LostPetNetwork.Services.Pets;

namespace LostPetNetwork.Services.Adoption
{
    /// <summary>In-memory demo listings (shown with a "Demo data" label). Used signed out and in tests.</summary>
    public sealed class MockAdoptionRepository : IAdoptionRepository
    {
        readonly List<AdoptionListing> _listings = new List<AdoptionListing>();
        readonly HashSet<string> _requests = new HashSet<string>();
        readonly int _latencyMs;
        AdopterProfile _home;
        int _next = 1;

        public bool IsDemo => true;

        public MockAdoptionRepository(int latencyMs = 150, bool seed = true)
        {
            _latencyMs = latencyMs;
            if (!seed) return;
            var now = DateTime.UtcNow;
            _listings.Add(new AdoptionListing
            {
                Id = "adopt-1", OwnerUserId = "demo-shelter", OwnerName = "Tunis Paws Shelter", Species = 0, Name = "Rocky", AgeMonths = 30,
                Size = 2, Energy = Level.High, GoodWithKids = true, GoodWithPets = true, NeedsGarden = true,
                Description = "Big, joyful and loves long walks. Needs space to run and a family that stays active.",
                LocationText = "Ariana, Tunis", Latitude = 36.8625, Longitude = 10.1956, CreatedAtUtc = now.AddDays(-2), IsDemo = true,
                PetAvatarJson = new PetConfig { species = 0, coat = 2, pattern = 2, size = 2, ears = 0, collar = 3 }.ToJson()
            });
            _listings.Add(new AdoptionListing
            {
                Id = "adopt-2", OwnerUserId = "demo-shelter", OwnerName = "Tunis Paws Shelter", Species = 1, Name = "Misty", AgeMonths = 14,
                Size = 0, Energy = Level.Low, GoodWithKids = false, GoodWithPets = true, NeedsGarden = false,
                Description = "A gentle, quiet cat who loves sunny windows. Prefers a calm home without small children.",
                LocationText = "Lac 1, Tunis", Latitude = 36.8335, Longitude = 10.2290, CreatedAtUtc = now.AddDays(-1), IsDemo = true,
                PetAvatarJson = new PetConfig { species = 1, coat = 4, pattern = 0, size = 0, collar = 5 }.ToJson()
            });
            _listings.Add(new AdoptionListing
            {
                Id = "adopt-3", OwnerUserId = "demo-owner", OwnerName = "Nour H.", Species = 0, Name = "Pixel", AgeMonths = 8,
                Size = 1, Energy = Level.Medium, GoodWithKids = true, GoodWithPets = false, NeedsGarden = false,
                Description = "A playful young dog, already house-trained. Better as the only pet.",
                LocationText = "La Marsa", Latitude = 36.8782, Longitude = 10.3247, CreatedAtUtc = now.AddHours(-9), IsDemo = true,
                PetAvatarJson = new PetConfig { species = 0, coat = 1, pattern = 1, size = 1, ears = 1, collar = 1 }.ToJson()
            });
        }

        async Task Delay()
        {
            if (_latencyMs > 0) await Task.Delay(_latencyMs);
        }

        public async Task<IReadOnlyList<AdoptionListing>> GetAvailableAsync()
        {
            await Delay();
            return _listings.Where(l => l.Status == ListingStatus.Available).OrderByDescending(l => l.CreatedAtUtc).ToList();
        }

        public async Task<IReadOnlyList<AdoptionListing>> GetMineAsync(string userId)
        {
            await Delay();
            return _listings.Where(l => l.OwnerUserId == userId).OrderByDescending(l => l.CreatedAtUtc).ToList();
        }

        public async Task<AdoptionListing> CreateAsync(AdoptionDraft draft, string userId, string ownerName)
        {
            await Delay();
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            if (draft.Validate().Count > 0) throw new ArgumentException("The listing is not valid.");
            var listing = AdoptionMapper.FromDraft(draft, userId, ownerName, "local-adopt-" + _next++);
            listing.IsDemo = true;
            _listings.Add(listing);
            return listing;
        }

        public async Task<AdoptionListing> MarkAdoptedAsync(string id)
        {
            await Delay();
            var listing = _listings.FirstOrDefault(l => l.Id == id);
            if (listing != null) listing.Status = ListingStatus.Adopted;
            return listing;
        }

        public async Task<bool> RequestAsync(string listingId, string message)
        {
            await Delay();
            var listing = _listings.FirstOrDefault(l => l.Id == listingId);
            if (listing == null || listing.Status != ListingStatus.Available) return false;
            return _requests.Add(listingId);
        }

        public async Task<AdopterProfile> GetMyHomeAsync()
        {
            await Delay();
            return _home;
        }

        public async Task SaveMyHomeAsync(AdopterProfile home)
        {
            await Delay();
            _home = home;
        }
    }
}
