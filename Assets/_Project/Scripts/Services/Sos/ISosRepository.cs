using System.Collections.Generic;
using System.Threading.Tasks;

namespace LostPetNetwork.Services.Sos
{
    public interface ISosRepository
    {
        bool IsDemo { get; }
        /// <summary>Open and in-progress alerts, newest first.</summary>
        Task<IReadOnlyList<SosAlert>> GetActiveAsync();
        Task<SosAlert> CreateAsync(SosDraft draft, string reporterId, string reporterName);
        /// <summary>Marks the alert as being handled by the current user; null if someone else already took it.</summary>
        Task<SosAlert> ClaimAsync(string id, string responderId);
        Task<SosAlert> ResolveAsync(string id);
        Task<byte[]> GetAudioAsync(SosAlert alert);
    }

    public static class SosServices
    {
        public static ISosRepository Repository { get; set; } = new MockSosRepository();

        public static void UseSupabase() => Repository = new SupabaseSosRepository();

        public static void UseDemo() => Repository = new MockSosRepository();
    }
}
