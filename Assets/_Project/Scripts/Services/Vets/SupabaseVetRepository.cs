using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LostPetNetwork.Services.Auth;
using LostPetNetwork.Services.Reports;
using LostPetNetwork.Services.Supabase;

namespace LostPetNetwork.Services.Vets
{
    /// <summary>The vet directory in Supabase. Read-only for the app: rows are loaded by the team.</summary>
    public sealed class SupabaseVetRepository : IVetRepository
    {
        public bool IsDemo => false;

        public async Task<IReadOnlyList<VetClinic>> GetAllAsync()
        {
            string token = await AuthService.Instance.GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token)) throw new RepositoryException("Please sign in again.");
            var resp = await SupabaseHttp.SendAsync("GET",
                $"{SupabaseConfig.RestUrl}/vet_clinics?select={VetMapper.Select}&order=name.asc&limit=1000", accessToken: token);
            if (!resp.IsSuccess) throw new RepositoryException(SupabaseHttp.FriendlyError(resp));
            return VetMapper.ParseRows(resp.Body).Select(VetMapper.ToClinic).ToList();
        }
    }
}
