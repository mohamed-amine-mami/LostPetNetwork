using System.Collections.Generic;
using System.Threading.Tasks;

namespace LostPetNetwork.Services.Vets
{
    /// <summary>A few made-up clinics (labelled "Demo data"). Used signed out and in tests.</summary>
    public sealed class MockVetRepository : IVetRepository
    {
        readonly List<VetClinic> _clinics;
        readonly int _latencyMs;

        public bool IsDemo => true;

        public MockVetRepository(int latencyMs = 150, bool seed = true)
        {
            _latencyMs = latencyMs;
            _clinics = new List<VetClinic>();
            if (!seed) return;
            _clinics.Add(new VetClinic
            {
                Id = "vet-1", Name = "Clinique Vétérinaire Ennasr", Address = "Avenue Hédi Nouira", City = "Ennasr", Governorate = "Ariana",
                Phone = "+21671000001", Latitude = 36.8625, Longitude = 10.1650, OpeningHours = "Mon-Sat 09:00-19:00", IsDemo = true
            });
            _clinics.Add(new VetClinic
            {
                Id = "vet-2", Name = "Urgences Vétérinaires 24/7", Address = "Rue de la Liberté", City = "Ariana", Governorate = "Ariana",
                Phone = "+21671000002", Latitude = 36.8665, Longitude = 10.1647, OpeningHours = "Open 24 hours", Is24h = true, IsDemo = true
            });
            _clinics.Add(new VetClinic
            {
                Id = "vet-3", Name = "Cabinet Vétérinaire Manouba", Address = "Route de Bizerte", City = "Manouba", Governorate = "Manouba",
                Latitude = 36.8089, Longitude = 10.0863, IsDemo = true
            });
        }

        public async Task<IReadOnlyList<VetClinic>> GetAllAsync()
        {
            if (_latencyMs > 0) await Task.Delay(_latencyMs);
            return _clinics.ToArray();
        }
    }
}
