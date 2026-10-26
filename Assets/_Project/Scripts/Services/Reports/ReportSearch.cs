using System.Collections.Generic;
using System.Linq;
using LostPetNetwork.Services.Sensors;

namespace LostPetNetwork.Services.Reports
{
    public enum ReportSort { Newest, Nearest }

    /// <summary>Sorting and distance filtering for the report lists (pure logic, covered by tests).</summary>
    public static class ReportSearch
    {
        /// <summary>The distance shown for a report: real when both positions are known, otherwise the sample value.</summary>
        public static float DistanceKm(PetReport report, GeoFix? user) => report.DistanceFromKm(user, out _);

        /// <summary>
        /// Applies the distance limit and the sort order to an already filtered list. Reports with no usable
        /// distance (no position, no sample value) are kept at the end when sorting and kept when limiting,
        /// so a missing position never hides a report.
        /// </summary>
        public static List<PetReport> Order(IEnumerable<PetReport> reports, ReportSort sort, GeoFix? user, double? maxKm = null)
        {
            var list = reports.ToList();
            bool HasDistance(PetReport r) => user.HasValue ? r.HasLocation : r.DistanceKm > 0f;

            if (maxKm.HasValue)
                list = list.Where(r => !HasDistance(r) || DistanceKm(r, user) <= maxKm.Value).ToList();

            if (sort == ReportSort.Nearest)
                return list.OrderBy(r => HasDistance(r) ? 0 : 1)
                           .ThenBy(r => HasDistance(r) ? DistanceKm(r, user) : float.MaxValue)
                           .ThenByDescending(r => r.ReportedAtUtc)
                           .ToList();
            return list.OrderByDescending(r => r.ReportedAtUtc).ToList();
        }

        /// <summary>A short "1.8 km" / "320 m" label, or null when the distance is unknown.</summary>
        public static string DistanceLabel(PetReport report, GeoFix? user)
        {
            bool known = user.HasValue ? report.HasLocation : report.DistanceKm > 0f;
            if (!known) return null;
            float km = DistanceKm(report, user);
            return km < 1f ? $"{(int)(km * 1000f / 10f) * 10} m" : $"{km:0.#} km";
        }
    }
}
