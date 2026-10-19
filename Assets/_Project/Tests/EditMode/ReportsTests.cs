using System;
using System.Linq;
using LostPetNetwork.Services.Reports;
using LostPetNetwork.UI;
using NUnit.Framework;
using UnityEngine;

namespace LostPetNetwork.Tests
{
    public class ReportDraftTests
    {
        [Test]
        public void Valid_draft_has_no_errors() =>
            Assert.AreEqual(0, new ReportDraft { LocationText = "Parc du Belvédère" }.Validate().Count);

        [Test]
        public void Location_is_required()
        {
            var errors = new ReportDraft { LocationText = "  " }.Validate();
            Assert.IsTrue(errors.ContainsKey(ReportDraft.LocationKey));
        }

        [Test]
        public void Species_is_required()
        {
            var errors = new ReportDraft { LocationText = "Lac 2", Species = null }.Validate();
            Assert.IsTrue(errors.ContainsKey(ReportDraft.SpeciesKey));
        }

        [Test]
        public void Length_limits()
        {
            var errors = new ReportDraft
            {
                LocationText = new string('x', 121), PetName = new string('n', 41), Description = new string('d', 501)
            }.Validate();
            Assert.IsTrue(errors.ContainsKey(ReportDraft.LocationKey));
            Assert.IsTrue(errors.ContainsKey(ReportDraft.NameKey));
            Assert.IsTrue(errors.ContainsKey(ReportDraft.DescriptionKey));
        }

        [Test]
        public void Photo_flag() => Assert.IsTrue(new ReportDraft { PhotoBytes = new byte[] { 1 } }.HasPhoto);
    }

    public class MockRepositoryTests
    {
        static MockReportRepository NewRepo() => new MockReportRepository(latencyMs: 0);
        static T Run<T>(System.Threading.Tasks.Task<T> t) => t.GetAwaiter().GetResult();

        [Test]
        public void Seeded_reports_are_marked_demo()
        {
            var all = Run(NewRepo().GetAllAsync());
            Assert.AreEqual(3, all.Count);
            Assert.IsTrue(all.All(r => r.IsDemo));
            Assert.IsTrue(NewRepo().IsDemo);
        }

        [Test]
        public void Mine_is_empty_until_a_report_is_created()
        {
            var repo = NewRepo();
            Assert.AreEqual(0, Run(repo.GetMineAsync("me")).Count);
            var created = Run(repo.CreateAsync(new ReportDraft { LocationText = "Menzah 6", PetName = "Coco" }, "me", "Mo"));
            var mine = Run(repo.GetMineAsync("me"));
            Assert.AreEqual(1, mine.Count);
            Assert.AreEqual(created.Id, mine[0].Id);
            Assert.AreEqual(ReportStatus.Active, mine[0].Status);
            Assert.AreEqual(4, Run(repo.GetAllAsync()).Count);
        }

        [Test]
        public void Other_users_do_not_see_my_reports()
        {
            var repo = NewRepo();
            Run(repo.CreateAsync(new ReportDraft { LocationText = "Menzah 6" }, "me", "Mo"));
            Assert.AreEqual(0, Run(repo.GetMineAsync("someone-else")).Count);
        }

        [Test]
        public void Invalid_draft_is_rejected()
        {
            var repo = NewRepo();
            Assert.Throws<ArgumentException>(() => Run(repo.CreateAsync(new ReportDraft { LocationText = "" }, "me", "Mo")));
            Assert.Throws<ArgumentNullException>(() => Run(repo.CreateAsync(null, "me", "Mo")));
        }

        [Test]
        public void Mark_reunited_changes_status()
        {
            var repo = NewRepo();
            var r = Run(repo.CreateAsync(new ReportDraft { LocationText = "Lac 2" }, "me", "Mo"));
            Run(repo.MarkReunitedAsync(r.Id));
            Assert.AreEqual(ReportStatus.Reunited, Run(repo.GetAsync(r.Id)).Status);
        }

        [Test]
        public void Contact_request_rules()
        {
            var repo = NewRepo();
            Assert.IsTrue(Run(repo.RequestContactAsync("demo-1", "visitor")));
            Assert.IsFalse(Run(repo.RequestContactAsync("demo-1", "visitor")), "duplicate request");
            Assert.IsFalse(Run(repo.RequestContactAsync("demo-1", "demo-owner")), "owner cannot contact self");
            Assert.IsFalse(Run(repo.RequestContactAsync("does-not-exist", "visitor")));
            Assert.AreEqual(3, Run(repo.GetAsync("demo-1")).ContactRequests);
        }

        [Test]
        public void Display_helpers()
        {
            var unnamed = new PetReport { Type = ReportType.Found, Species = PetSpecies.Cat };
            Assert.AreEqual("Found cat", unnamed.DisplayName);
            var lost = new PetReport { PetName = " Max ", Breed = "Beagle", Species = PetSpecies.Dog, ReportedAtUtc = DateTime.UtcNow.AddHours(-5) };
            Assert.AreEqual("Max", lost.DisplayName);
            Assert.AreEqual("Dog · Beagle", lost.Summary);
            Assert.AreEqual("5h ago", lost.TimeAgo(DateTime.UtcNow));
        }
    }

    public class ScreenLogicTests
    {
        static PetReport R(string name, ReportType type, PetSpecies species, ReportStatus status = ReportStatus.Active, string place = "Tunis") =>
            new PetReport { PetName = name, Type = type, Species = species, Status = status, LocationText = place, Breed = "", Description = "" };

        [Test]
        public void Explore_filter_by_chip_and_text()
        {
            var list = new[]
            {
                R("Biscuit", ReportType.Lost, PetSpecies.Dog, place: "Belvédère"),
                R("Luna", ReportType.Found, PetSpecies.Cat, place: "Lac 2"),
                R("Max", ReportType.Lost, PetSpecies.Dog, place: "La Marsa")
            };
            Assert.AreEqual(3, ExploreScreen.Filter(list, 0, "").Count);
            Assert.AreEqual(2, ExploreScreen.Filter(list, 1, "").Count, "Lost");
            Assert.AreEqual(1, ExploreScreen.Filter(list, 2, "").Count, "Found");
            Assert.AreEqual(2, ExploreScreen.Filter(list, 3, "").Count, "Dogs");
            Assert.AreEqual(1, ExploreScreen.Filter(list, 4, "").Count, "Cats");
            Assert.AreEqual("Max", ExploreScreen.Filter(list, 0, "marsa").Single().PetName);
            Assert.AreEqual(0, ExploreScreen.Filter(list, 0, "zzz").Count);
            Assert.AreEqual(0, ExploreScreen.Filter(list, 4, "biscuit").Count, "chip and text combine");
        }

        [Test]
        public void Map_filter()
        {
            var list = new[] { R("A", ReportType.Lost, PetSpecies.Dog), R("B", ReportType.Found, PetSpecies.Cat) };
            Assert.AreEqual(1, MapScreen.Filter(list, 1, "").Count);
            Assert.AreEqual(1, MapScreen.Filter(list, 2, "").Count);
            Assert.AreEqual(2, MapScreen.Filter(list, 0, "").Count);
        }

        [Test]
        public void MyReports_filter()
        {
            var list = new[]
            {
                R("A", ReportType.Lost, PetSpecies.Dog),
                R("B", ReportType.Lost, PetSpecies.Dog, ReportStatus.Reunited)
            };
            Assert.AreEqual(1, MyReportsScreen.Filter(list, 0).Count);
            Assert.AreEqual(1, MyReportsScreen.Filter(list, 1).Count);
            Assert.AreEqual(2, MyReportsScreen.Filter(list, 2).Count);
        }

        [Test]
        public void Details_next_action()
        {
            var mine = new PetReport { OwnerUserId = "me", Status = ReportStatus.Active };
            var theirs = new PetReport { OwnerUserId = "other", Status = ReportStatus.Active };
            var done = new PetReport { OwnerUserId = "other", Status = ReportStatus.Reunited };
            Assert.AreEqual(DetailsScreen.NextAction.MarkReunited, DetailsScreen.ActionFor(mine, "me"));
            Assert.AreEqual(DetailsScreen.NextAction.Contact, DetailsScreen.ActionFor(theirs, "me"));
            Assert.AreEqual(DetailsScreen.NextAction.None, DetailsScreen.ActionFor(done, "me"));
            Assert.AreEqual(DetailsScreen.NextAction.None, DetailsScreen.ActionFor(null, "me"));
        }

        [Test]
        public void Review_summary_mentions_key_fields()
        {
            var summary = ReportScreen.BuildSummary(new ReportDraft
            {
                Type = ReportType.Found, Species = PetSpecies.Cat, LocationText = " Lac 2 ", PetName = "Luna", When = WhenOption.Yesterday
            });
            StringAssert.Contains("Found cat", summary);
            StringAssert.Contains("Lac 2", summary);
            StringAssert.Contains("Yesterday", summary);
            StringAssert.Contains("Luna", summary);
            StringAssert.Contains("Photo: none", summary);
        }

        [Test]
        public void Text_helpers()
        {
            Assert.AreEqual("MT", ProfileScreen.InitialsOf("Mohamed Test"));
            Assert.AreEqual("M", ProfileScreen.InitialsOf("Mohamed"));
            Assert.AreEqual("?", ProfileScreen.InitialsOf(""));
            StringAssert.EndsWith(", Mohamed", HomeScreen.GreetingFor("Mohamed Amine"));
            Assert.IsFalse(HomeScreen.GreetingFor(null).Contains(","));
        }
    }

    public class CameraMathTests
    {
        [Test]
        public void Orient_rotates_90_degrees_clockwise()
        {
            // 2 wide x 3 high; mark the bottom-left pixel (0,0) red.
            var src = new Texture2D(2, 3, TextureFormat.RGBA32, false);
            var px = new Color32[6];
            for (int i = 0; i < 6; i++) px[i] = new Color32(0, 0, 0, 255);
            px[0] = new Color32(255, 0, 0, 255);
            src.SetPixels32(px); src.Apply();

            var rotated = CameraScreen.Orient(src, 90, false);
            Assert.AreEqual(3, rotated.width);
            Assert.AreEqual(2, rotated.height);
            // (x=0,y=0) maps to (nx = y = 0, ny = w-1-x = 1)
            Assert.AreEqual(255, rotated.GetPixel(0, 1).r * 255f, 0.5f);
            UnityEngine.Object.DestroyImmediate(src);
            UnityEngine.Object.DestroyImmediate(rotated);
        }

        [Test]
        public void Orient_zero_without_mirror_returns_same_texture()
        {
            var src = new Texture2D(2, 2);
            Assert.AreSame(src, CameraScreen.Orient(src, 0, false));
            Assert.AreSame(src, CameraScreen.Orient(src, 360, false));
            UnityEngine.Object.DestroyImmediate(src);
        }

        [Test]
        public void EncodeJpeg_downscales_large_photos()
        {
            var src = new Texture2D(2048, 1024, TextureFormat.RGBA32, false);
            var bytes = CameraScreen.EncodeJpeg(src, 1024);
            Assert.AreEqual(0xFF, bytes[0]);
            Assert.AreEqual(0xD8, bytes[1], "JPEG signature");
            var check = new Texture2D(2, 2);
            Assert.IsTrue(check.LoadImage(bytes));
            Assert.AreEqual(1024, check.width);
            Assert.AreEqual(512, check.height);
            UnityEngine.Object.DestroyImmediate(src);
            UnityEngine.Object.DestroyImmediate(check);
        }
    }
}
