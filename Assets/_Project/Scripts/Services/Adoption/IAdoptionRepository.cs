using System.Collections.Generic;
using System.Threading.Tasks;

namespace LostPetNetwork.Services.Adoption
{
    public interface IAdoptionRepository
    {
        bool IsDemo { get; }
        Task<IReadOnlyList<AdoptionListing>> GetAvailableAsync();
        Task<IReadOnlyList<AdoptionListing>> GetMineAsync(string userId);
        Task<AdoptionListing> CreateAsync(AdoptionDraft draft, string userId, string ownerName);
        Task<AdoptionListing> MarkAdoptedAsync(string id);
        /// <summary>True when the request was recorded; false when it already existed or is not allowed.</summary>
        Task<bool> RequestAsync(string listingId, string message);
        Task<AdopterProfile> GetMyHomeAsync();
        Task SaveMyHomeAsync(AdopterProfile home);
    }

    public static class AdoptionServices
    {
        public static IAdoptionRepository Repository { get; set; } = new MockAdoptionRepository();

        public static void UseSupabase() => Repository = new SupabaseAdoptionRepository();

        public static void UseDemo() => Repository = new MockAdoptionRepository();
    }
}
