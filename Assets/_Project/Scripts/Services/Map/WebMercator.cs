using System;

namespace LostPetNetwork.Services.Map
{
    /// <summary>Slippy-map maths (Web Mercator, 256 px tiles) shared by the map view and its tests.</summary>
    public static class WebMercator
    {
        public const int TileSize = 256;
        public const double MaxLatitude = 85.05112878;

        public static double MapSize(int zoom) => TileSize * Math.Pow(2, zoom);

        public static double LonToX(double lon, int zoom) => (lon + 180.0) / 360.0 * MapSize(zoom);

        public static double LatToY(double lat, int zoom)
        {
            lat = Math.Max(-MaxLatitude, Math.Min(MaxLatitude, lat));
            double s = Math.Sin(lat * Math.PI / 180.0);
            return (0.5 - Math.Log((1 + s) / (1 - s)) / (4 * Math.PI)) * MapSize(zoom);
        }

        public static double XToLon(double x, int zoom) => x / MapSize(zoom) * 360.0 - 180.0;

        public static double YToLat(double y, int zoom)
        {
            double n = Math.PI - 2 * Math.PI * y / MapSize(zoom);
            return 180.0 / Math.PI * Math.Atan(0.5 * (Math.Exp(n) - Math.Exp(-n)));
        }

        /// <summary>The highest zoom at which the whole box fits in the view (with padding on every side).</summary>
        public static int ZoomToFit(double minLat, double maxLat, double minLon, double maxLon,
            double widthPx, double heightPx, double paddingPx = 60, int minZoom = 3, int maxZoom = 16)
        {
            double w = Math.Max(1, widthPx - 2 * paddingPx);
            double h = Math.Max(1, heightPx - 2 * paddingPx);
            for (int z = maxZoom; z > minZoom; z--)
            {
                double spanX = Math.Abs(LonToX(maxLon, z) - LonToX(minLon, z));
                double spanY = Math.Abs(LatToY(minLat, z) - LatToY(maxLat, z));
                if (spanX <= w && spanY <= h) return z;
            }
            return minZoom;
        }
    }
}
