using System;

namespace LostPetNetwork.Services.Sensors
{
    /// <summary>Distance and direction maths used by the map, search and AR radar.</summary>
    public static class GeoMath
    {
        const double EarthRadiusMeters = 6371000.0;

        public static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            double p1 = ToRad(lat1), p2 = ToRad(lat2);
            double dp = p2 - p1, dl = ToRad(lon2 - lon1);
            double a = Math.Sin(dp / 2) * Math.Sin(dp / 2) +
                       Math.Cos(p1) * Math.Cos(p2) * Math.Sin(dl / 2) * Math.Sin(dl / 2);
            return 2 * EarthRadiusMeters * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        /// <summary>Initial compass bearing from point 1 to point 2, 0..360 clockwise from north.</summary>
        public static double BearingDegrees(double lat1, double lon1, double lat2, double lon2)
        {
            double p1 = ToRad(lat1), p2 = ToRad(lat2), dl = ToRad(lon2 - lon1);
            double y = Math.Sin(dl) * Math.Cos(p2);
            double x = Math.Cos(p1) * Math.Sin(p2) - Math.Sin(p1) * Math.Cos(p2) * Math.Cos(dl);
            return (ToDeg(Math.Atan2(y, x)) + 360.0) % 360.0;
        }

        /// <summary>Angle to turn from the way the phone faces to the target, -180..180 (positive = to the right).</summary>
        public static double RelativeBearing(double targetBearing, double headingDegrees)
        {
            double d = (targetBearing - headingDegrees + 540.0) % 360.0 - 180.0;
            return d;
        }

        static double ToRad(double deg) => deg * Math.PI / 180.0;
        static double ToDeg(double rad) => rad * 180.0 / Math.PI;
    }
}
