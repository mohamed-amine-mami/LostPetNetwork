using System.Collections.Generic;
using System.Linq;
using LostPetNetwork.Services.Sensors;

namespace LostPetNetwork.Services.Vets
{
    /// <summary>A clinic with its distance from the user (null when the position is unknown).</summary>
    public readonly struct NearbyVet
    {
        public readonly VetClinic Clinic;
        public readonly double? Meters;

        public NearbyVet(VetClinic clinic, double? meters)
        {
            Clinic = clinic;
            Meters = meters;
        }

        public string DistanceLabel =>
            !Meters.HasValue ? null : Meters.Value < 1000 ? $"{(int)(Meters.Value / 10) * 10} m away" : $"{Meters.Value / 1000.0:0.#} km away";
    }

    /// <summary>Sorts and filters the directory for the "Vets near you" list. Pure logic, covered by tests.</summary>
    public static class VetFinder
    {
        /// <summary>Nearest first when the position is known, otherwise alphabetical. Clinics without coordinates are skipped.</summary>
        public static List<NearbyVet> Nearest(IEnumerable<VetClinic> clinics, GeoFix? me, bool only24h = false, int max = 30)
        {
            var list = clinics
                .Where(c => c != null && !(c.Latitude == 0 && c.Longitude == 0) && (!only24h || c.Is24h))
                .Select(c => new NearbyVet(c, me.HasValue ? GeoMath.DistanceMeters(me.Value.Latitude, me.Value.Longitude, c.Latitude, c.Longitude) : (double?)null));
            list = me.HasValue
                ? list.OrderBy(v => v.Meters.Value)
                : list.OrderBy(v => v.Clinic.Name, System.StringComparer.CurrentCultureIgnoreCase);
            return list.Take(max).ToList();
        }
    }
}
