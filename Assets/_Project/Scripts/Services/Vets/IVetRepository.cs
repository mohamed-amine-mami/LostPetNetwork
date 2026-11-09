using System.Collections.Generic;
using System.Threading.Tasks;

namespace LostPetNetwork.Services.Vets
{
    public interface IVetRepository
    {
        bool IsDemo { get; }
        /// <summary>The whole directory (a few hundred clinics at most); the app sorts it by distance.</summary>
        Task<IReadOnlyList<VetClinic>> GetAllAsync();
    }

    public static class VetServices
    {
        public static IVetRepository Repository { get; set; } = new MockVetRepository();

        public static void UseSupabase() => Repository = new SupabaseVetRepository();

        public static void UseDemo() => Repository = new MockVetRepository();
    }
}
