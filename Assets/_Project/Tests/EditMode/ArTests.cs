using System.Collections.Generic;
using LostPetNetwork.AR;
using LostPetNetwork.Services.Adoption;
using LostPetNetwork.Services.Pets;
using LostPetNetwork.Services.Reports;
using LostPetNetwork.Services.Sensors;
using LostPetNetwork.UI;
using NUnit.Framework;

namespace LostPetNetwork.Tests
{
    public class CompassMathTests
    {
        [TestCase(0, 60, true)]
        [TestCase(29, 60, true)]
        [TestCase(-30, 60, true)]
        [TestCase(31, 60, false)]
        [TestCase(-120, 60, false)]
        public void InViewFollowsTheCameraField(double rel, double fov, bool expected) =>
            Assert.AreEqual(expected, CompassMath.InView(rel, fov));

        [Test]
        public void ScreenFractionIsCentredAndMonotonic()
        {
            Assert.AreEqual(0.5f, CompassMath.ScreenFraction(0, 60), 1e-5f);
            Assert.AreEqual(1f, CompassMath.ScreenFraction(30, 60), 1e-5f);
            Assert.AreEqual(0f, CompassMath.ScreenFraction(-30, 60), 1e-5f);
            float a = CompassMath.ScreenFraction(10, 60), b = CompassMath.ScreenFraction(20, 60);
            Assert.Greater(a, 0.5f);
            Assert.Greater(b, a);
            Assert.AreEqual(1f - a, CompassMath.ScreenFraction(-10, 60), 1e-5f, "left mirrors right");
        }

        [Test]
        public void OffScreenTargetsStickToTheNearestEdge()
        {
            Assert.AreEqual(1f, CompassMath.ScreenFraction(90, 60));
            Assert.AreEqual(0f, CompassMath.ScreenFraction(-170, 60));
        }

        [Test]
        public void ArrowTurnsTheWayTheTargetIs()
        {
            Assert.AreEqual(0f, CompassMath.ArrowRotation(0));
            Assert.Less(CompassMath.ArrowRotation(90), 0f, "target to the right turns the arrow clockwise (negative z)");
            Assert.Greater(CompassMath.ArrowRotation(-90), 0f);
        }

        [TestCase(0.4, "0 m")]
        [TestCase(7, "7 m")]
        [TestCase(124, "120 m")]
        [TestCase(996, "1000 m")]
        [TestCase(1800, "1.8 km")]
        [TestCase(15400, "15.4 km")]
        public void DistancesAreReadable(double meters, string expected) => Assert.AreEqual(expected, CompassMath.DistanceText(meters));

        [Test]
        public void ArrivalRadius()
        {
            Assert.IsTrue(CompassMath.HasArrived(12));
            Assert.IsFalse(CompassMath.HasArrived(45));
            Assert.IsTrue(CompassMath.HasArrived(45, 50));
        }

        [Test]
        public void RelativeBearingFeedsTheScreenPosition()
        {
            // Facing east, a target to the north is 90° to the left: off screen, pinned to the left edge.
            double bearing = GeoMath.BearingDegrees(36.8, 10.1, 36.9, 10.1);
            double rel = GeoMath.RelativeBearing(bearing, 90);
            Assert.AreEqual(-90, rel, 0.5);
            Assert.AreEqual(0f, CompassMath.ScreenFraction(rel, 60));
        }
    }

    public class ArMathTests
    {
        [Test]
        public void RingRadiusMatchesTheAreaOfACircle()
        {
            Assert.AreEqual(1f, ArMath.RadiusForArea((float)System.Math.PI), 1e-4f);
            Assert.AreEqual(3.785f, ArMath.RadiusForArea(45f), 0.01f);
            Assert.AreEqual(0f, ArMath.RadiusForArea(-5f));
        }

        [Test]
        public void CatsAreScaledSmallerThanDogs() =>
            Assert.Less(ArMath.PetScale(new PetConfig { species = 1 }), ArMath.PetScale(new PetConfig { species = 0 }));

        [Test]
        public void ALifeSizeDogIsAboutAsTallAsARealOne()
        {
            var pet = PetFactory.Build(new PetConfig { species = 0, size = 1 });
            pet.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
            try
            {
                var root = new UnityEngine.GameObject("Root");
                pet.transform.SetParent(root.transform, false);
                root.transform.localScale = UnityEngine.Vector3.one * ArMath.PetScale(new PetConfig());
                var b = new UnityEngine.Bounds(root.transform.position, UnityEngine.Vector3.zero);
                bool first = true;
                foreach (var r in root.GetComponentsInChildren<UnityEngine.Renderer>())
                {
                    if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                }
                int cm = ArMath.ToCentimetres(b.size.y);
                Assert.That(cm, Is.InRange(50, 90), "a medium dog's head is roughly 50-90 cm above the floor");
                UnityEngine.Object.DestroyImmediate(root);
            }
            finally
            {
                if (pet != null) UnityEngine.Object.DestroyImmediate(pet);
            }
        }

        [Test]
        public void CentimetresRoundToWholeNumbers()
        {
            Assert.AreEqual(73, ArMath.ToCentimetres(0.726f));
            Assert.AreEqual(0, ArMath.ToCentimetres(0.004f));
        }
    }

    public class ArLauncherTests
    {
        sealed class FakeAr : IArService
        {
            public bool IsSupported { get; set; }
            public readonly List<ArMode> Opened = new List<ArMode>();
            public void Open(ArMode mode, System.Action<PetReport> onClosed) => Opened.Add(mode);
        }

        FakeAr _fake;
        IArService _before;

        [SetUp]
        public void SetUp()
        {
            _before = ArLauncher.Service;
            _fake = new FakeAr { IsSupported = true };
            ArLauncher.Service = _fake;
        }

        [TearDown]
        public void TearDown() => ArLauncher.Service = _before;

        [Test]
        public void ButtonsAppearOnlyWhenARIsSupported()
        {
            Assert.IsTrue(ArLauncher.CanTryAtHome && ArLauncher.CanShowRadar && ArLauncher.CanGuideToSos && ArLauncher.CanPlaceAvatar);
            _fake.IsSupported = false;
            Assert.IsFalse(ArLauncher.CanTryAtHome || ArLauncher.CanShowRadar || ArLauncher.CanGuideToSos || ArLauncher.CanPlaceAvatar);
            ArLauncher.Service = null;
            Assert.IsFalse(ArLauncher.CanTryAtHome, "no AR module at all");
        }

        [Test]
        public void TryAtHomeCarriesTheLookTheNameAndTheSpaceNeeded()
        {
            var listing = new AdoptionListing
            {
                Name = "Rocky", Species = 0, Size = 2, Energy = Level.High,
                PetAvatarJson = new PetConfig { species = 0, coat = 5, size = 2 }.ToJson()
            };
            ArLauncher.OpenTryAtHome(listing);
            CollectionAssert.AreEqual(new[] { ArMode.PlacePet }, _fake.Opened);
            Assert.AreEqual("Rocky", ArRequest.PetName);
            Assert.AreEqual(5, ArRequest.Pet.coat);
            Assert.AreEqual(AdoptionCompatibility.RecommendedSpaceM2(listing), ArRequest.RecommendedSpaceM2);
        }

        [Test]
        public void ListingsWithoutALookStillGetOneFromTheirSpeciesAndSize()
        {
            ArLauncher.OpenTryAtHome(new AdoptionListing { Name = "Misty", Species = 1, Size = 0 });
            Assert.IsTrue(ArRequest.Pet.IsCat);
            Assert.AreEqual(0, ArRequest.Pet.size);
        }

        [Test]
        public void NothingOpensWhenARIsNotSupported()
        {
            _fake.IsSupported = false;
            ArLauncher.OpenTryAtHome(new AdoptionListing { Name = "x" });
            ArLauncher.OpenRadar(new List<PetReport>(), null);
            ArLauncher.OpenSosGuide(new LostPetNetwork.Services.Sos.SosAlert());
            ArLauncher.OpenAvatar(new LostPetNetwork.Services.Avatar.AvatarConfig());
            Assert.IsEmpty(_fake.Opened);
        }

        [Test]
        public void RadarAndGuideSetTheirRequests()
        {
            var report = new PetReport { Id = "r1", PetName = "Luna", Latitude = 36.8, Longitude = 10.1 };
            ArLauncher.OpenRadar(new[] { report }, null);
            Assert.AreEqual(ArMode.Radar, ArRequest.Mode);
            Assert.AreEqual(1, ArRequest.Reports.Count);
            var alert = new LostPetNetwork.Services.Sos.SosAlert { Id = "s1" };
            ArLauncher.OpenSosGuide(alert);
            Assert.AreEqual(ArMode.Guide, ArRequest.Mode);
            Assert.AreSame(alert, ArRequest.Alert);
            CollectionAssert.AreEqual(new[] { ArMode.Radar, ArMode.Guide }, _fake.Opened);
        }
    }
}
