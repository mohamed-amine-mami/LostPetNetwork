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

        [Test]
        public void HeadingFromRotationVector_reads_north_east_and_undefined_when_flat()
        {
            // Phone upright, back camera facing north: a 90 degree turn about the east axis.
            Assert.AreEqual(0f, CompassMath.HeadingFromRotationVector(0.7071f, 0f, 0f, 0.7071f).Value, 0.5f);
            // The same phone turned to face east.
            Assert.AreEqual(90f, CompassMath.HeadingFromRotationVector(0.5f, -0.5f, -0.5f, 0.5f).Value, 0.5f);
            // Lying flat with the screen up, the back camera points down: no heading.
            Assert.IsFalse(CompassMath.HeadingFromRotationVector(0f, 0f, 0f, 1f).HasValue);
        }

        [Test]
        public void PetDrawDistance_keeps_the_pet_between_2_and_8_metres()
        {
            Assert.AreEqual(2f, ArMath.PetDrawDistance(0.5));
            Assert.AreEqual(5f, ArMath.PetDrawDistance(5));
            Assert.AreEqual(8f, ArMath.PetDrawDistance(29));
        }

        [Test]
        public void UserScale_is_kept_between_half_and_double_and_survives_bad_values()
        {
            Assert.AreEqual(0.5f, ArMath.ClampUserScale(0.1f));
            Assert.AreEqual(2f, ArMath.ClampUserScale(9f));
            Assert.AreEqual(1.3f, ArMath.ClampUserScale(1.3f));
            Assert.AreEqual(1f, ArMath.ClampUserScale(float.NaN));
        }

        [Test]
        public void Pinch_scales_with_the_fingers_distance_and_ignores_a_zero_start()
        {
            Assert.AreEqual(1.5f, ArMath.PinchScale(1f, 200f, 300f), 1e-4f);
            Assert.AreEqual(0.5f, ArMath.PinchScale(1f, 400f, 50f), "never smaller than half size");
            Assert.AreEqual(2f, ArMath.PinchScale(1.5f, 100f, 400f), "never bigger than double");
            Assert.AreEqual(1.2f, ArMath.PinchScale(1.2f, 0f, 300f), "no jump when the start distance is unknown");
        }

        [Test]
        public void Twist_is_positive_when_the_fingers_turn_clockwise_on_the_screen()
        {
            var start = new UnityEngine.Vector2(100f, 0f);
            Assert.AreEqual(90f, ArMath.TwistDegrees(start, new UnityEngine.Vector2(0f, -100f)), 0.01f, "right to down is clockwise");
            Assert.AreEqual(-90f, ArMath.TwistDegrees(start, new UnityEngine.Vector2(0f, 100f)), 0.01f);
            Assert.AreEqual(0f, ArMath.TwistDegrees(start, UnityEngine.Vector2.zero));
        }

        [Test]
        public void A_surface_needs_30_centimetres_each_way()
        {
            Assert.IsTrue(ArMath.PlaneBigEnough(new UnityEngine.Vector2(0.8f, 0.5f)));
            Assert.IsFalse(ArMath.PlaneBigEnough(new UnityEngine.Vector2(0.8f, 0.2f)));
            Assert.IsFalse(ArMath.PlaneBigEnough(new UnityEngine.Vector2(0.1f, 0.1f)));
        }

        [Test]
        public void PlacementInfo_says_the_size_and_mentions_resizing_only_when_it_happened()
        {
            Assert.AreEqual("About 60 cm tall (approximate). The ring shows roughly 4 m² of space.", ArMath.PlacementInfo(false, 0.6f, 1f, 4f));
            Assert.AreEqual("About 90 cm tall (resized to 150%). The ring shows roughly 4 m² of space.", ArMath.PlacementInfo(false, 0.6f, 1.5f, 4f));
            Assert.AreEqual("About 170 cm tall.", ArMath.PlacementInfo(true, 1.7f, 1f, 0f));
            Assert.AreEqual("About 85 cm tall (resized to 50%).", ArMath.PlacementInfo(true, 1.7f, 0.5f, 0f));
            Assert.AreEqual("About 60 cm tall (approximate).", ArMath.PlacementInfo(false, 0.6f, 1f, 0f), "no space sentence without a recommendation");
        }

        [Test]
        public void Ring_needs_a_quarter_second_to_turn_green_and_one_bad_frame_does_not_flip_it()
        {
            var r = new ReticleStabilizer();
            Assert.AreEqual(ReticleState.None, r.Feed(ReticleState.Good, 0.1f), "a first sighting is not trusted yet");
            Assert.AreEqual(ReticleState.None, r.Feed(ReticleState.Good, 0.1f));
            Assert.AreEqual(ReticleState.Good, r.Feed(ReticleState.Good, 0.1f), "after 0.3 s it is steady");
            Assert.AreEqual(ReticleState.Good, r.Feed(ReticleState.Rough, 0.016f), "one rough frame is ignored");
            Assert.AreEqual(ReticleState.Good, r.Feed(ReticleState.Good, 0.016f));
            Assert.AreEqual(ReticleState.Good, r.Feed(ReticleState.None, 0.1f), "a short loss keeps the ring");
        }

        [Test]
        public void Ring_goes_away_after_losing_the_surface_for_a_while_and_downgrades_when_rough_lasts()
        {
            var r = new ReticleStabilizer();
            for (int i = 0; i < 5; i++) r.Feed(ReticleState.Good, 0.1f);
            Assert.AreEqual(ReticleState.Good, r.State);
            for (int i = 0; i < 3; i++) r.Feed(ReticleState.Rough, 0.1f);
            Assert.AreEqual(ReticleState.Rough, r.State, "a lasting rough reading downgrades the ring");
            for (int i = 0; i < 3; i++) r.Feed(ReticleState.None, 0.1f);
            Assert.AreEqual(ReticleState.Rough, r.State, "0.3 s without a surface is still within the grace time");
            r.Feed(ReticleState.None, 0.2f);
            Assert.AreEqual(ReticleState.None, r.State);
            r.Feed(ReticleState.Good, 0.3f);
            Assert.AreEqual(ReticleState.Good, r.State);
            r.Reset();
            Assert.AreEqual(ReticleState.None, r.State);
        }

        [TestCase(0, "Straight ahead")]
        [TestCase(-14, "Straight ahead")]
        [TestCase(30, "Slightly right")]
        [TestCase(-30, "Slightly left")]
        [TestCase(90, "Turn right")]
        [TestCase(-120, "Turn left")]
        [TestCase(170, "Turn around")]
        [TestCase(-179, "Turn around")]
        public void GuideInstruction_gives_plain_words_for_the_turn(double relative, string expected) =>
            Assert.AreEqual(expected, CompassMath.GuideInstruction(relative, false));

        [Test]
        public void GuideInstruction_says_you_are_here_once_arrived_whatever_the_angle()
        {
            Assert.AreEqual("You're here", CompassMath.GuideInstruction(120, true));
        }

        [Test]
        public void Alignment_is_aligned_within_15_degrees_near_within_45_and_off_otherwise()
        {
            Assert.AreEqual(CompassMath.Alignment.Aligned, CompassMath.AlignmentOf(-15));
            Assert.AreEqual(CompassMath.Alignment.Near, CompassMath.AlignmentOf(15.5));
            Assert.AreEqual(CompassMath.Alignment.Near, CompassMath.AlignmentOf(-45));
            Assert.AreEqual(CompassMath.Alignment.Off, CompassMath.AlignmentOf(46));
            Assert.AreEqual(CompassMath.Alignment.Off, CompassMath.AlignmentOf(180));
        }

        [TestCase(10, "about 1 min on foot")]
        [TestCase(500, "about 6 min on foot")]
        [TestCase(1100, "about 13 min on foot")]
        [TestCase(10000, "about 2 h on foot")]
        public void WalkingText_uses_a_5_kmh_pace(double meters, string expected) =>
            Assert.AreEqual(expected, CompassMath.WalkingText(meters));
    }
}
