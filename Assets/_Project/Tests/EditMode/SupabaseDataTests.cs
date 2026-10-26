using System.Linq;
using System;
using LostPetNetwork.Services.Auth;
using LostPetNetwork.Services.Avatar;
using LostPetNetwork.Services.Reports;
using LostPetNetwork.Services.Supabase;
using NUnit.Framework;
using UnityEngine;

namespace LostPetNetwork.Tests
{
    public class JsonBodyTests
    {
        [Test]
        public void QuoteEscapesQuotesNewlinesAndBackslashes() =>
            Assert.AreEqual("\"a\\\"b\\\\c\\nd\\te\"", JsonBody.Quote("a\"b\\c\nd\te"));

        [Test]
        public void ControlCharactersAreEscaped() => Assert.AreEqual("\"\\u0001\"", JsonBody.Quote("\u0001"));

        [Test]
        public void BodyHasStringsNumbersNullsAndRawObjects()
        {
            string json = new JsonBody().Str("a", "x").Str("b", null).Num("c", 1.5).NumOrNull("d", null)
                .Bool("e", true).Raw("f", "{\"k\":1}").ToString();
            Assert.AreEqual("{\"a\":\"x\",\"b\":null,\"c\":1.5,\"d\":null,\"e\":true,\"f\":{\"k\":1}}", json);
        }

        [Test]
        public void NumbersUseADotEvenInAFrenchCulture()
        {
            var old = System.Threading.Thread.CurrentThread.CurrentCulture;
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
            try { StringAssert.Contains("\"n\":36.8219", new JsonBody().Num("n", 36.8219).ToString()); }
            finally { System.Threading.Thread.CurrentThread.CurrentCulture = old; }
        }
    }

    public class ReportMapperTests
    {
        const string RowsJson = "[{\"id\":\"r1\",\"owner_id\":\"u1\",\"owner_name\":\"Sarah K.\",\"type\":\"found\",\"status\":\"active\"," +
            "\"species\":\"cat\",\"pet_name\":\"Luna\",\"breed\":null,\"description\":\"Friendly\",\"location_text\":\"Lac 2, Tunis\"," +
            "\"latitude\":36.8368,\"longitude\":10.2373,\"occurred_at\":\"2026-10-26T15:00:00+00:00\",\"created_at\":\"2026-10-26T15:01:00.5+00:00\"," +
            "\"photo_path\":\"u1/r1.jpg\",\"pet_avatar_text\":\"{\\\"v\\\": 1, \\\"coat\\\": 7, \\\"species\\\": 1}\"}," +
            "{\"id\":\"r2\",\"owner_id\":\"u2\",\"owner_name\":\"Amel\",\"type\":\"lost\",\"status\":\"reunited\",\"species\":\"bird\"," +
            "\"pet_name\":null,\"breed\":null,\"description\":null,\"location_text\":\"La Marsa\",\"latitude\":null,\"longitude\":null," +
            "\"occurred_at\":\"2026-10-20T10:00:00+00:00\",\"created_at\":\"2026-10-20T10:00:00+00:00\",\"photo_path\":null,\"pet_avatar_text\":null}]";

        [Test]
        public void RowsFromTheDatabaseBecomeReports()
        {
            var now = new DateTime(2026, 10, 26, 16, 0, 0, DateTimeKind.Utc);
            var rows = ReportMapper.ParseRows(RowsJson);
            Assert.AreEqual(2, rows.Count);

            var a = ReportMapper.ToReport(rows[0], now);
            Assert.AreEqual("r1", a.Id);
            Assert.AreEqual(ReportType.Found, a.Type);
            Assert.AreEqual(ReportStatus.Active, a.Status);
            Assert.AreEqual(PetSpecies.Cat, a.Species);
            Assert.AreEqual("Luna", a.PetName);
            Assert.AreEqual("u1", a.OwnerUserId);
            Assert.IsTrue(a.HasLocation);
            Assert.AreEqual(36.8368, a.Latitude, 1e-9);
            Assert.AreEqual("u1/r1.jpg", a.PhotoPath);
            Assert.IsFalse(a.IsDemo);
            StringAssert.Contains("\"coat\": 7", a.PetAvatarJson);
            Assert.AreEqual(WhenOption.Today, a.When);
            Assert.AreEqual(new DateTime(2026, 10, 26, 15, 0, 0, DateTimeKind.Utc), a.ReportedAtUtc);
        }

        [Test]
        public void MissingValuesAreHandled()
        {
            var b = ReportMapper.ToReport(ReportMapper.ParseRows(RowsJson)[1], new DateTime(2026, 10, 26, 16, 0, 0, DateTimeKind.Utc));
            Assert.AreEqual(ReportStatus.Reunited, b.Status);
            Assert.AreEqual(PetSpecies.Bird, b.Species);
            Assert.IsFalse(b.HasLocation);
            Assert.IsNull(b.PetAvatarJson);
            Assert.IsNull(b.PhotoPath);
            Assert.AreEqual(WhenOption.Earlier, b.When);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not json")]
        [TestCase("[]")]
        public void UnreadableAnswersGiveNoRows(string json) => Assert.AreEqual(0, ReportMapper.ParseRows(json).Count);

        [Test]
        public void SingleRowParsingNeedsAnId()
        {
            Assert.IsTrue(ReportMapper.TryParseRow("{\"id\":\"x\",\"type\":\"lost\"}", out var row));
            Assert.AreEqual("x", row.id);
            Assert.IsFalse(ReportMapper.TryParseRow("{\"type\":\"lost\"}", out _));
            Assert.IsFalse(ReportMapper.TryParseRow("", out _));
        }

        [Test]
        public void InsertBodyContainsOnlyAllowedColumnsAndEscapesText()
        {
            var draft = new ReportDraft
            {
                Type = ReportType.Found, Species = PetSpecies.Cat, PetName = " Mimi \"the cat\" ", Description = "",
                LocationText = " Parc ", Latitude = 36.8, Longitude = 10.1, When = WhenOption.JustNow,
                PetAvatarJson = new LostPetNetwork.Services.Pets.PetConfig { coat = 2 }.ToJson()
            };
            string body = ReportMapper.BuildInsertBody(draft, "  Sarah ", new DateTime(2026, 10, 26, 15, 0, 0, DateTimeKind.Utc));
            StringAssert.Contains("\"owner_name\":\"Sarah\"", body);
            StringAssert.Contains("\"type\":\"found\"", body);
            StringAssert.Contains("\"species\":\"cat\"", body);
            StringAssert.Contains("\"pet_name\":\"Mimi \\\"the cat\\\"\"", body);
            StringAssert.Contains("\"description\":null", body);
            StringAssert.Contains("\"location_text\":\"Parc\"", body);
            StringAssert.Contains("\"latitude\":36.8", body);
            StringAssert.Contains("\"pet_avatar\":{", body);
            StringAssert.Contains("\"occurred_at\":\"2026-10-26T15:00:00", body);
            foreach (var forbidden in new[] { "\"owner_id\"", "\"id\"", "\"status\"", "\"created_at\"" })
                StringAssert.DoesNotContain(forbidden, body);
        }

        [Test]
        public void InsertBodyWithoutCoordinatesOrLookSendsNulls()
        {
            var draft = new ReportDraft { LocationText = "Tunis centre" };
            string body = ReportMapper.BuildInsertBody(draft, null, DateTime.UtcNow);
            StringAssert.Contains("\"latitude\":null", body);
            StringAssert.Contains("\"owner_name\":\"Neighbour\"", body);
            StringAssert.DoesNotContain("pet_avatar", body);
        }

        [Test]
        public void StylisedMapPositionStaysInsideTheMap()
        {
            var inside = new PetReport { Latitude = 36.84, Longitude = 10.22 };
            ReportMapper.ApplyMapPosition(inside);
            Assert.That(inside.MapX, Is.InRange(0.06f, 0.94f));
            Assert.That(inside.MapY, Is.InRange(0.1f, 0.9f));
            var far = new PetReport { Latitude = 50, Longitude = 80 };
            ReportMapper.ApplyMapPosition(far);
            Assert.AreEqual(0.94f, far.MapX, 1e-6);
            var none = new PetReport();
            ReportMapper.ApplyMapPosition(none);
            Assert.AreEqual(0.5f, none.MapX, 1e-6);
        }

        [Test]
        public void SpeciesNamesMatchTheDatabaseCheckConstraint()
        {
            foreach (PetSpecies s in Enum.GetValues(typeof(PetSpecies)))
                Assert.Contains(ReportMapper.SpeciesToDb(s), new[] { "dog", "cat", "bird", "other" });
        }

        [Test]
        public void ReportServicesSwitchBetweenDemoAndSupabase()
        {
            var before = ReportServices.Repository;
            try
            {
                ReportServices.UseSupabase();
                Assert.IsFalse(ReportServices.Repository.IsDemo);
                ReportServices.UseDemo();
                Assert.IsTrue(ReportServices.Repository.IsDemo);
            }
            finally { ReportServices.Repository = before; }
        }
    }

    public class AvatarSyncTests
    {
        static void SetSession(AuthSession s) =>
            typeof(AuthService).GetProperty("Session").GetSetMethod(true).Invoke(AuthService.Instance, new object[] { s });

        [TearDown]
        public void TearDown() => SetSession(null);

        [Test]
        public void SignedOutOrTokenlessSessionsSkipTheServer()
        {
            SetSession(null);
            Assert.IsTrue(AvatarSync.PushAsync(AvatarConfig.Default()).Result.Skipped);
            Assert.IsTrue(AvatarSync.FetchAsync().Result.Skipped);

            SetSession(new AuthSession
            {
                accessToken = "", refreshToken = "r", userId = "u", email = "a@b.c",
                expiresAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600
            });
            var result = AvatarSync.PushAsync(AvatarConfig.Default()).Result;
            Assert.IsTrue(result.Ok);
            Assert.IsTrue(result.Skipped);
        }
    }
}

namespace LostPetNetwork.Tests
{
    public class ReportSearchTests
    {
        static LostPetNetwork.Services.Reports.PetReport R(string id, double lat, double lon, int hoursAgo, float sample = 0f) =>
            new LostPetNetwork.Services.Reports.PetReport
            {
                Id = id, Latitude = lat, Longitude = lon, DistanceKm = sample,
                ReportedAtUtc = System.DateTime.UtcNow.AddHours(-hoursAgo)
            };

        static readonly LostPetNetwork.Services.Sensors.GeoFix Me = new LostPetNetwork.Services.Sensors.GeoFix(36.8065, 10.1815);

        [Test]
        public void NewestPutsTheFreshestReportFirst()
        {
            var list = LostPetNetwork.Services.Reports.ReportSearch.Order(
                new[] { R("old", 36.8, 10.1, 30), R("new", 36.8, 10.1, 1) }, LostPetNetwork.Services.Reports.ReportSort.Newest, Me);
            Assert.AreEqual("new", list[0].Id);
        }

        [Test]
        public void NearestUsesRealDistancesWhenThePositionIsKnown()
        {
            var far = R("far", 36.8782, 10.3247, 1);     // La Marsa, about 17 km
            var near = R("near", 36.8219, 10.1748, 5);    // Belvedere, about 1.7 km
            var list = LostPetNetwork.Services.Reports.ReportSearch.Order(
                new[] { far, near }, LostPetNetwork.Services.Reports.ReportSort.Nearest, Me);
            Assert.AreEqual("near", list[0].Id);
        }

        [Test]
        public void ReportsWithoutAPositionGoLastAndAreNeverHiddenByTheRadius()
        {
            var nowhere = R("nowhere", 0, 0, 1);
            var near = R("near", 36.8219, 10.1748, 5);
            var far = R("far", 36.8782, 10.3247, 1);
            var list = LostPetNetwork.Services.Reports.ReportSearch.Order(
                new[] { nowhere, far, near }, LostPetNetwork.Services.Reports.ReportSort.Nearest, Me, 5.0);
            CollectionAssert.AreEqual(new[] { "near", "nowhere" }, list.Select(r => r.Id).ToArray());
        }

        [Test]
        public void WithoutAUserPositionTheSampleDistancesAreUsed()
        {
            var a = R("a", 36.8, 10.1, 1, sample: 8.9f);
            var b = R("b", 36.8, 10.1, 2, sample: 1.2f);
            var list = LostPetNetwork.Services.Reports.ReportSearch.Order(
                new[] { a, b }, LostPetNetwork.Services.Reports.ReportSort.Nearest, null);
            Assert.AreEqual("b", list[0].Id);
        }

        [Test]
        public void DistanceLabelsAreShortAndReadable()
        {
            var report = R("x", 36.8219, 10.1748, 1);
            StringAssert.EndsWith("km", LostPetNetwork.Services.Reports.ReportSearch.DistanceLabel(report, Me));
            Assert.IsNull(LostPetNetwork.Services.Reports.ReportSearch.DistanceLabel(R("y", 0, 0, 1), Me));
            var close = R("z", 36.8070, 10.1818, 1);
            StringAssert.EndsWith(" m", LostPetNetwork.Services.Reports.ReportSearch.DistanceLabel(close, Me));
        }

        [Test]
        public void ExploreSortChipsMapToTheRightOrdering()
        {
            var near = R("near", 36.8219, 10.1748, 5);
            var far = R("far", 36.8782, 10.3247, 1);
            Assert.AreEqual("far", LostPetNetwork.UI.ExploreScreen.Arrange(new[] { near, far }, 0, Me)[0].Id);
            Assert.AreEqual("near", LostPetNetwork.UI.ExploreScreen.Arrange(new[] { far, near }, 1, Me)[0].Id);
            var within = LostPetNetwork.UI.ExploreScreen.Arrange(new[] { far, near }, 2, Me);
            CollectionAssert.AreEqual(new[] { "near" }, within.Select(r => r.Id).ToArray());
        }
    }
}
