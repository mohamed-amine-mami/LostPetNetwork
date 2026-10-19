using System.Collections.Generic;
using System.Threading.Tasks;

namespace LostPetNetwork.Services.Reports
{
    /// <summary>
    /// Data source for pet reports. Today only <see cref="MockReportRepository"/> exists because the
    /// Supabase `reports` tables have not been created. A Supabase-backed implementation can replace it
    /// without touching the UI.
    /// </summary>
    public interface IReportRepository
    {
        /// <summary>True when data is fake/in-memory. The UI shows a "Demo data" tag when this is true.</summary>
        bool IsDemo { get; }

        Task<IReadOnlyList<PetReport>> GetAllAsync();
        Task<IReadOnlyList<PetReport>> GetMineAsync(string userId);
        Task<PetReport> GetAsync(string id);
        Task<PetReport> CreateAsync(ReportDraft draft, string userId, string ownerName);
        Task<PetReport> MarkReunitedAsync(string id);
        Task<bool> RequestContactAsync(string id, string requesterUserId);
    }
}
