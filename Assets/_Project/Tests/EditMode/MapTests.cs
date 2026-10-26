using LostPetNetwork.Services.Map;
using LostPetNetwork.Services.Reports;
using LostPetNetwork.Services.Sensors;
using NUnit.Framework;

namespace LostPetNetwork.Tests
{
    public class WebMercatorTests
    {
        [Test]
        public void TheWholeWorldIsOneTileAtZoomZero()
        {
            Assert.AreEqual(256, WebMercator.MapSize(0), 1e-9);
            Assert.AreEqual(128, WebMercator.LonToX(0, 0), 1e-9);
            Assert.AreEqual(128, WebMercator.LatToY(0, 0), 1e-9);
            Assert.AreEqual(0, WebMercator.LonToX(-180, 0), 1e-9);
            Assert.AreEqual(256, WebMercator.LonToX(180, 0), 1e-9);
        }

        [Test]
        public void EachZoomDoublesTheMap() =>
            Assert.AreEqual(2 * WebMercator.MapSize(10), WebMercator.MapSize(11), 1e-6);

        [Test]
        public void PolesAreClampedInsteadOfBreaking()
        {
            Assert.AreEqual(0, WebMercator.LatToY(89.9, 3), 0.5);
            Assert.AreEqual(WebMercator.MapSize(3), WebMercator.LatToY(-89.9, 3), 0.5);
        }

        [TestCase(36.8065, 10.1815, 13)]
        [TestCase(48.8566, 2.3522, 10)]
        [TestCase(-33.8688, 151.2093, 16)]
        public void PixelsConvertBackToTheSamePlace(double lat, double lon, int zoom)
        {
            double x = WebMercator.LonToX(lon, zoom), y = WebMercator.LatToY(lat, zoom);
            Assert.AreEqual(lon, WebMercator.XToLon(x, zoom), 1e-7);
            Assert.AreEqual(lat, WebMercator.YToLat(y, zoom), 1e-7);
        }

        [Test]
        public void KnownTileNumbers()
        {
            // Paris at zoom 10 and Tunis at zoom 13 (checked against the standard slippy-map formula).
            Assert.AreEqual(518, (int)(WebMercator.LonToX(2.3522, 10) / 256));
            Assert.AreEqual(352, (int)(WebMercator.LatToY(48.8566, 10) / 256));
            Assert.AreEqual(4327, (int)(WebMercator.LonToX(10.1815, 13) / 256));
            Assert.AreEqual(3194, (int)(WebMercator.LatToY(36.8065, 13) / 256));
        }

        [Test]
        public void ZoomToFitShowsWholeBoxAndZoomsInForSmallAreas()
        {
            int city = WebMercator.ZoomToFit(36.80, 36.88, 10.17, 10.33, 390, 700, 60);
            int street = WebMercator.ZoomToFit(36.8060, 36.8070, 10.1810, 10.1820, 390, 700, 60);
            int country = WebMercator.ZoomToFit(30, 37, 8, 11, 390, 700, 60);
            Assert.Greater(street, city);
            Assert.Greater(city, country);
            double spanX = WebMercator.LonToX(10.33, city) - WebMercator.LonToX(10.17, city);
            Assert.LessOrEqual(spanX, 390 - 120);
        }
    }

    public class MapboxApiTests
    {
        [Test]
        public void TileUrlHasZoomXYAndEscapedToken()
        {
            string url = MapboxApi.TileUrl("pk.a b", 13, 4327, 3207);
            StringAssert.Contains("/tiles/256/13/4327/3207@2x", url);
            StringAssert.Contains("access_token=pk.a%20b", url);
            StringAssert.StartsWith("https://api.mapbox.com/", url);
        }

        [Test]
        public void ReverseGeocodeUrlUsesLongitudeFirstAndInvariantNumbers()
        {
            var old = System.Threading.Thread.CurrentThread.CurrentCulture;
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
            try
            {
                string url = MapboxApi.ReverseGeocodeUrl("pk.token", 36.8065, 10.1815);
                StringAssert.Contains("/mapbox.places/10.1815,36.8065.json", url);
            }
            finally { System.Threading.Thread.CurrentThread.CurrentCulture = old; }
        }

        [Test]
        public void PlaceNameIsReadFromGeocodingAnswer()
        {
            const string json = "{\"type\":\"FeatureCollection\",\"features\":[{\"id\":\"locality.1\",\"text\":\"Bab El Khadhra\",\"place_name\":\"Bab El Khadhra, Tunis, Tunisia\",\"center\":[10.17,36.8]}]}";
            Assert.AreEqual("Bab El Khadhra, Tunis, Tunisia", MapboxApi.ParsePlaceName(json));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("{\"features\":[]}")]
        [TestCase("not json")]
        [TestCase("{\"features\":[{\"place_name\":\"  \"}]}")]
        public void MissingPlaceNameGivesNull(string json) => Assert.IsNull(MapboxApi.ParsePlaceName(json));
    }

    public class ReportLocationTests
    {
        [Test]
        public void DistanceIsRealWhenBothPositionsAreKnown()
        {
            var report = new PetReport { Latitude = 36.8219, Longitude = 10.1748, DistanceKm = 99f };
            float km = report.DistanceFromKm(new GeoFix(36.8065, 10.1815), out bool real);
            Assert.IsTrue(real);
            Assert.AreEqual(1.8f, km, 0.2f);
        }

        [Test]
        public void SampleDistanceIsUsedWithoutAUserPosition()
        {
            var report = new PetReport { Latitude = 36.8, Longitude = 10.1, DistanceKm = 4.2f };
            Assert.AreEqual(4.2f, report.DistanceFromKm(null, out bool real), 1e-6);
            Assert.IsFalse(real);
        }

        [Test]
        public void SampleDistanceIsUsedWhenTheReportHasNoPosition()
        {
            var report = new PetReport { DistanceKm = 2f };
            Assert.IsFalse(report.HasLocation);
            report.DistanceFromKm(new GeoFix(36.8, 10.1), out bool real);
            Assert.IsFalse(real);
        }

        [Test]
        public void NewReportsKeepTheChosenCoordinates()
        {
            var repo = new MockReportRepository(latencyMs: 0, seed: false);
            var draft = new ReportDraft { LocationText = "Somewhere", Latitude = 35.5, Longitude = 10.6 };
            var report = repo.CreateAsync(draft, "u", "Me").Result;
            Assert.AreEqual(35.5, report.Latitude, 1e-9);
            Assert.AreEqual(10.6, report.Longitude, 1e-9);
        }

        [Test]
        public void NewReportsWithoutCoordinatesStillGetAPlaceOnTheMap()
        {
            var repo = new MockReportRepository(latencyMs: 0, seed: false);
            var report = repo.CreateAsync(new ReportDraft { LocationText = "Somewhere" }, "u", "Me").Result;
            Assert.IsTrue(report.HasLocation);
        }

        [Test]
        public void DemoReportsAreAllOnTheMap()
        {
            foreach (var r in new MockReportRepository(latencyMs: 0).GetAllAsync().Result) Assert.IsTrue(r.HasLocation, r.Id);
        }
    }
}
