using LostPetNetwork.Services.Map;
using LostPetNetwork.Services.Sensors;
using NUnit.Framework;
using UnityEngine;

namespace LostPetNetwork.Tests
{
    public class GeoMathTests
    {
        [Test]
        public void OneDegreeOfLatitudeIsAbout111Km()
        {
            double d = GeoMath.DistanceMeters(0, 0, 1, 0);
            Assert.AreEqual(111195, d, 100);
        }

        [Test]
        public void DistanceToSamePointIsZero() =>
            Assert.AreEqual(0, GeoMath.DistanceMeters(36.8, 10.18, 36.8, 10.18), 0.001);

        [TestCase(0, 0, 1, 0, 0)]
        [TestCase(0, 0, 0, 1, 90)]
        [TestCase(0, 0, -1, 0, 180)]
        [TestCase(0, 0, 0, -1, 270)]
        public void BearingFollowsTheCompass(double la1, double lo1, double la2, double lo2, double expected) =>
            Assert.AreEqual(expected, GeoMath.BearingDegrees(la1, lo1, la2, lo2), 0.5);

        [TestCase(10, 350, 20)]
        [TestCase(350, 10, -20)]
        [TestCase(90, 90, 0)]
        [TestCase(170, 0, 170)]
        [TestCase(200, 0, -160)]
        public void RelativeBearingWrapsAround(double target, double heading, double expected)
        {
            double r = GeoMath.RelativeBearing(target, heading);
            Assert.AreEqual(expected, r, 0.001);
        }
    }

    public class MapConfigTests
    {
        [TearDown]
        public void TearDown() => MapConfig.Reload();

        [Test]
        public void PublicTokensAreAccepted() =>
            Assert.IsTrue(MapConfig.IsValidPublicToken("pk.abcdefghijklmnopqrstuvwxyz0123456789"));

        [Test]
        public void SecretAndEmptyTokensAreRefused()
        {
            Assert.IsFalse(MapConfig.IsValidPublicToken("sk.abcdefghijklmnopqrstuvwxyz0123456789"));
            Assert.IsFalse(MapConfig.IsValidPublicToken(""));
            Assert.IsFalse(MapConfig.IsValidPublicToken(null));
            Assert.IsFalse(MapConfig.IsValidPublicToken("pk.short"));
        }

        [Test]
        public void SecretTokenIsNotStored()
        {
            MapConfig.SetForTests("sk.abcdefghijklmnopqrstuvwxyz0123456789");
            Assert.IsFalse(MapConfig.IsConfigured);
        }

        [Test]
        public void LocalFileIsLoadedWhenPresent()
        {
            MapConfig.Reload();
            bool fileExists = Resources.Load<TextAsset>("MapConfig.local") != null;
            Assert.AreEqual(fileExists, MapConfig.IsConfigured);
        }
    }

    public class SensorHubTests
    {
        [TearDown]
        public void TearDown() => SensorHub.Reset();

        [Test]
        public void DefaultsReportUnavailable()
        {
            SensorHub.Reset();
            Assert.IsFalse(SensorHub.Location.IsAvailable);
            Assert.IsNull(SensorHub.Location.Latest);
            Assert.IsNull(SensorHub.Heading.HeadingDegrees);
            Assert.IsFalse(SensorHub.Motion.IsAvailable);
        }

        [Test]
        public void FakeLocationRaisesUpdates()
        {
            var fake = new FakeLocationProvider();
            SensorHub.Location = fake;
            GeoFix? seen = null;
            SensorHub.Location.Updated += f => seen = f;
            fake.Push(new GeoFix(36.8, 10.18, 5f));
            Assert.IsTrue(seen.HasValue);
            Assert.AreEqual(36.8, SensorHub.Location.Latest.Value.Latitude, 1e-9);
        }

        [Test]
        public void FakeMotionAndHeadingAreSettable()
        {
            SensorHub.Heading = new FakeHeadingProvider { HeadingDegrees = 42f };
            SensorHub.Motion = new FakeMotionProvider { Acceleration = new Vector3(0, 0, -2) };
            Assert.AreEqual(42f, SensorHub.Heading.HeadingDegrees.Value);
            Assert.AreEqual(-2f, SensorHub.Motion.Acceleration.z);
        }
    }
}
