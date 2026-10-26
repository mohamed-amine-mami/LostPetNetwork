using System;
using System.Linq;
using LostPetNetwork.Services.Adoption;
using LostPetNetwork.Services.Pets;
using NUnit.Framework;

namespace LostPetNetwork.Tests
{
    public class AdoptionCompatibilityTests
    {
        static AdoptionListing Dog(int size = 1, Level energy = Level.Medium, bool garden = false, bool kids = true, bool pets = true) =>
            new AdoptionListing { Name = "Rex", Species = 0, Size = size, Energy = energy, NeedsGarden = garden, GoodWithKids = kids, GoodWithPets = pets };

        static AdopterProfile Home(int m2 = 80, Level activity = Level.Medium, bool garden = false, bool kids = false, bool pets = false) =>
            new AdopterProfile { HomeSizeM2 = m2, Activity = activity, HasGarden = garden, HasKids = kids, HasOtherPets = pets };

        [Test]
        public void APerfectFitScoresOneHundred()
        {
            var r = AdoptionCompatibility.Score(Dog(), Home(80));
            Assert.AreEqual(100, r.Score);
            Assert.AreEqual("Great match", r.Verdict);
            Assert.AreEqual(5, r.Factors.Count);
            Assert.AreEqual(100, r.Factors.Sum(f => f.Max));
        }

        [Test]
        public void ScoreIsAlwaysBetweenZeroAndOneHundred()
        {
            foreach (int size in new[] { 0, 1, 2 })
            foreach (Level energy in Enum.GetValues(typeof(Level)))
            foreach (Level activity in Enum.GetValues(typeof(Level)))
            foreach (bool garden in new[] { false, true })
            foreach (bool kids in new[] { false, true })
            foreach (bool pets in new[] { false, true })
            foreach (int m2 in new[] { 10, 30, 60, 200 })
            {
                var r = AdoptionCompatibility.Score(Dog(size, energy, garden, kids, pets),
                    Home(m2, activity, garden, kids, pets));
                Assert.That(r.Score, Is.InRange(0, 100));
                foreach (var f in r.Factors) Assert.That(f.Points, Is.InRange(0, f.Max), f.Label);
            }
        }

        [Test]
        public void ASmallHomeLowersTheSpaceScore()
        {
            var big = AdoptionCompatibility.Score(Dog(2, Level.High), Home(120));
            var small = AdoptionCompatibility.Score(Dog(2, Level.High), Home(30));
            Assert.Greater(big.Factors[0].Points, small.Factors[0].Points);
            Assert.Greater(small.Factors[0].Points, 0);
        }

        [Test]
        public void ALargeEnergeticDogNeedsMoreSpaceThanASmallCalmCat()
        {
            var dog = new AdoptionListing { Species = 0, Size = 2, Energy = Level.High };
            var cat = new AdoptionListing { Species = 1, Size = 0, Energy = Level.Low };
            Assert.AreEqual(80, AdoptionCompatibility.RecommendedSpaceM2(dog));
            Assert.AreEqual(15, AdoptionCompatibility.RecommendedSpaceM2(cat));
        }

        [Test]
        public void MismatchedActivityIsPenalisedByHowFarApartTheyAre()
        {
            int Activity(Level pet, Level home) => AdoptionCompatibility.Score(Dog(1, pet), Home(80, home)).Factors[1].Points;
            Assert.AreEqual(25, Activity(Level.High, Level.High));
            Assert.AreEqual(14, Activity(Level.High, Level.Medium));
            Assert.AreEqual(4, Activity(Level.High, Level.Low));
            Assert.AreEqual(4, Activity(Level.Low, Level.High));
        }

        [Test]
        public void GardenChildrenAndPetsRulesExplainThemselves()
        {
            var r = AdoptionCompatibility.Score(Dog(1, Level.Medium, garden: true, kids: false, pets: false),
                Home(80, Level.Medium, garden: false, kids: true, pets: true));
            Assert.AreEqual(0, r.Factors[2].Points, "needs a garden, none available");
            Assert.AreEqual(0, r.Factors[3].Points, "not good with children, children at home");
            Assert.AreEqual(3, r.Factors[4].Points, "not good with pets, pets at home");
            Assert.AreEqual(58, r.Score);
            Assert.AreEqual("Possible, check the notes", r.Verdict);
            foreach (var f in r.Factors) Assert.IsNotEmpty(f.Note);
        }

        [Test]
        public void VerdictsFollowTheScore()
        {
            string Verdict(int m2, Level activity, bool garden, bool kids, bool pets) =>
                AdoptionCompatibility.Score(Dog(2, Level.High, true, false, false), Home(m2, activity, garden, kids, pets)).Verdict;
            Assert.AreEqual("Great match", Verdict(150, Level.High, true, false, false));
            Assert.AreEqual("Not the best fit", Verdict(15, Level.Low, false, true, true));
        }
    }

    public class AdoptionModelTests
    {
        [Test]
        public void AgeAndSizeTextsAreReadable()
        {
            Assert.AreEqual("1 month", new AdoptionListing { AgeMonths = 1 }.AgeText);
            Assert.AreEqual("8 months", new AdoptionListing { AgeMonths = 8 }.AgeText);
            Assert.AreEqual("1 year", new AdoptionListing { AgeMonths = 12 }.AgeText);
            Assert.AreEqual("3 years", new AdoptionListing { AgeMonths = 40 }.AgeText);
            Assert.AreEqual("Dog · 3 years · Large", new AdoptionListing { Species = 0, AgeMonths = 40, Size = 2 }.Summary);
        }

        [Test]
        public void DraftValidationChecksNameAndLocation()
        {
            var errors = new AdoptionDraft { Name = " ", LocationText = "ab" }.Validate();
            Assert.IsTrue(errors.ContainsKey("name"));
            Assert.IsTrue(errors.ContainsKey("location"));
            Assert.AreEqual(0, new AdoptionDraft { Name = "Rex", LocationText = "Tunis centre" }.Validate().Count);
            Assert.IsTrue(new AdoptionDraft { Name = new string('x', 41), LocationText = "Tunis" }.Validate().ContainsKey("name"));
            Assert.IsTrue(new AdoptionDraft { Name = "Rex", LocationText = "Tunis", Description = new string('x', 501) }.Validate().ContainsKey("description"));
        }
    }

    public class AdoptionMapperTests
    {
        const string RowsJson = "[{\"id\":\"l1\",\"owner_id\":\"u1\",\"owner_name\":\"Nour H.\",\"species\":\"cat\",\"name\":\"Misty\"," +
            "\"age_months\":14,\"size\":\"small\",\"energy\":\"low\",\"good_with_kids\":false,\"good_with_pets\":true,\"needs_garden\":false," +
            "\"description\":null,\"location_text\":\"Lac 1\",\"latitude\":36.83,\"longitude\":10.22,\"status\":\"available\"," +
            "\"created_at\":\"2026-10-26T10:00:00+00:00\",\"pet_avatar_text\":\"{\\\"v\\\": 1, \\\"species\\\": 1}\"}]";

        [Test]
        public void RowsBecomeListings()
        {
            var l = AdoptionMapper.ToListing(AdoptionMapper.ParseRows(RowsJson).Single());
            Assert.AreEqual("l1", l.Id);
            Assert.IsTrue(l.IsCat);
            Assert.AreEqual(0, l.Size);
            Assert.AreEqual(Level.Low, l.Energy);
            Assert.IsFalse(l.GoodWithKids);
            Assert.IsTrue(l.HasLocation);
            Assert.AreEqual(ListingStatus.Available, l.Status);
            Assert.IsFalse(l.IsDemo);
            Assert.IsTrue(PetConfig.TryParse(l.PetAvatarJson, out var cfg));
            Assert.AreEqual(1, cfg.species);
        }

        [Test]
        public void InsertBodyUsesOnlyGrantedColumnsAndADefaultLook()
        {
            var draft = new AdoptionDraft { Name = " Rex ", Species = 0, AgeMonths = 30, Size = 2, Energy = Level.High, LocationText = "Tunis centre", NeedsGarden = true };
            string body = AdoptionMapper.BuildInsertBody(draft, "Nour");
            StringAssert.Contains("\"name\":\"Rex\"", body);
            StringAssert.Contains("\"size\":\"large\"", body);
            StringAssert.Contains("\"energy\":\"high\"", body);
            StringAssert.Contains("\"needs_garden\":true", body);
            StringAssert.Contains("\"pet_avatar\":{", body);
            foreach (var forbidden in new[] { "\"owner_id\"", "\"id\"", "\"status\"", "\"created_at\"" })
                StringAssert.DoesNotContain(forbidden, body);
        }

        [Test]
        public void HomeBodyAndRowRoundTrip()
        {
            var home = new AdopterProfile { HomeType = HomeType.House, HomeSizeM2 = 120, HasGarden = true, HasKids = true, HasOtherPets = false, Activity = Level.High };
            string body = AdoptionMapper.BuildHomeBody(home);
            StringAssert.Contains("\"home_type\":\"house\"", body);
            Assert.IsTrue(AdoptionMapper.TryParseHome(body, out var back));
            Assert.AreEqual(HomeType.House, back.HomeType);
            Assert.AreEqual(120, back.HomeSizeM2);
            Assert.IsTrue(back.HasGarden);
            Assert.AreEqual(Level.High, back.Activity);
            Assert.IsFalse(AdoptionMapper.TryParseHome("", out _));
            Assert.IsFalse(AdoptionMapper.TryParseHome("{}", out _));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("nope")]
        public void UnreadableAnswersGiveNoListings(string json) => Assert.AreEqual(0, AdoptionMapper.ParseRows(json).Count);
    }

    public class MockAdoptionRepositoryTests
    {
        [Test]
        public void SeedHasThreeAvailableListingsWithValidLooks()
        {
            var repo = new MockAdoptionRepository(latencyMs: 0);
            var all = repo.GetAvailableAsync().Result;
            Assert.AreEqual(3, all.Count);
            foreach (var l in all)
            {
                Assert.IsTrue(l.IsDemo);
                Assert.IsTrue(PetConfig.TryParse(l.PetAvatarJson, out _), l.Name);
            }
        }

        [Test]
        public void CreatedListingsAppearAndCanBeMarkedAdopted()
        {
            var repo = new MockAdoptionRepository(latencyMs: 0, seed: false);
            var created = repo.CreateAsync(new AdoptionDraft { Name = "Rex", LocationText = "Tunis centre", Species = 1, Size = 0 }, "u1", "Me").Result;
            Assert.AreEqual(1, repo.GetMineAsync("u1").Result.Count);
            Assert.AreEqual(1, repo.GetAvailableAsync().Result.Count);
            Assert.IsTrue(PetConfig.TryParse(created.PetAvatarJson, out var look));
            Assert.AreEqual(1, look.species);
            repo.MarkAdoptedAsync(created.Id).Wait();
            Assert.AreEqual(0, repo.GetAvailableAsync().Result.Count);
        }

        [Test]
        public void RequestsAreRecordedOnlyOnce()
        {
            var repo = new MockAdoptionRepository(latencyMs: 0);
            Assert.IsTrue(repo.RequestAsync("adopt-1", "Hello").Result);
            Assert.IsFalse(repo.RequestAsync("adopt-1", "Again").Result);
            Assert.IsFalse(repo.RequestAsync("missing", null).Result);
        }

        [Test]
        public void HomeProfileIsSavedAndReturned()
        {
            var repo = new MockAdoptionRepository(latencyMs: 0);
            Assert.IsNull(repo.GetMyHomeAsync().Result);
            repo.SaveMyHomeAsync(new AdopterProfile { HomeSizeM2 = 99 }).Wait();
            Assert.AreEqual(99, repo.GetMyHomeAsync().Result.HomeSizeM2);
        }

        [Test]
        public void InvalidDraftsAreRejected() =>
            Assert.Throws<AggregateException>(() => new MockAdoptionRepository(latencyMs: 0).CreateAsync(new AdoptionDraft(), "u", "n").Wait());

        [Test]
        public void ServicesSwitchBetweenDemoAndSupabase()
        {
            var before = AdoptionServices.Repository;
            try
            {
                AdoptionServices.UseSupabase();
                Assert.IsFalse(AdoptionServices.Repository.IsDemo);
                AdoptionServices.UseDemo();
                Assert.IsTrue(AdoptionServices.Repository.IsDemo);
            }
            finally { AdoptionServices.Repository = before; }
        }
    }
}
