using LostPetNetwork.Services.Pets;
using LostPetNetwork.Services.Reports;
using LostPetNetwork.UI;
using NUnit.Framework;
using UnityEngine;

namespace LostPetNetwork.Tests
{
    public class PetConfigTests
    {
        [Test]
        public void ClampKeepsEveryFieldInRange()
        {
            var c = new PetConfig { species = 9, coat = 99, pattern = -2, size = 7, ears = 5, collar = 40 };
            c.Clamp();
            Assert.AreEqual(PetConfig.SpeciesCount - 1, c.species);
            Assert.AreEqual(PetConfig.CoatCount - 1, c.coat);
            Assert.AreEqual(0, c.pattern);
            Assert.AreEqual(PetConfig.SizeCount - 1, c.size);
            Assert.AreEqual(PetConfig.EarCount - 1, c.ears);
            Assert.AreEqual(PetConfig.CollarCount - 1, c.collar);
        }

        [Test]
        public void JsonRoundTrip()
        {
            var c = new PetConfig { species = 1, coat = 6, pattern = 2, size = 0, ears = 1, collar = 3 };
            Assert.IsTrue(PetConfig.TryParse(c.ToJson(), out var back));
            Assert.IsTrue(c.SameAs(back));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("nope")]
        public void BadJsonIsRejected(string json) => Assert.IsFalse(PetConfig.TryParse(json, out _));

        [Test]
        public void RandomStaysInRange()
        {
            var rng = new System.Random(3);
            for (int i = 0; i < 200; i++)
            {
                var c = PetConfig.Random(rng);
                Assert.IsTrue(c.SameAs(c.Clone().Clamp()));
            }
        }

        [Test]
        public void PalettesMatchCounts()
        {
            Assert.AreEqual(PetConfig.CoatCount, PetPalette.Coat.Length);
            Assert.AreEqual(PetConfig.CollarCount, PetPalette.Collar.Length);
            Assert.AreEqual(PetConfig.SizeCount, PetPalette.SizeScale.Length);
            Assert.AreEqual(PetConfig.SpeciesCount, PetPalette.SpeciesNames.Length);
            Assert.AreEqual(PetConfig.PatternCount, PetPalette.PatternNames.Length);
        }

        [Test]
        public void DemoReportsCarryValidPetLooks()
        {
            var repo = new MockReportRepository(latencyMs: 0);
            var all = repo.GetAllAsync().Result;
            Assert.IsNotEmpty(all);
            foreach (var r in all) Assert.IsTrue(PetConfig.TryParse(r.PetAvatarJson, out _), r.Id);
        }

        [Test]
        public void ReportCreationKeepsThePetLook()
        {
            var repo = new MockReportRepository(latencyMs: 0, seed: false);
            var draft = new ReportDraft { LocationText = "Tunis centre", PetAvatarJson = new PetConfig { coat = 3 }.ToJson() };
            var report = repo.CreateAsync(draft, "u1", "Me").Result;
            Assert.IsTrue(PetConfig.TryParse(report.PetAvatarJson, out var cfg));
            Assert.AreEqual(3, cfg.coat);
        }
    }

    public class PetFactoryTests
    {
        static void AssertHealthy(PetConfig c, string label)
        {
            var go = PetFactory.Build(c);
            go.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var renderers = go.GetComponentsInChildren<MeshRenderer>();
                Assert.GreaterOrEqual(renderers.Length, 14, label);
                Bounds b = renderers[0].bounds;
                foreach (var r in renderers)
                {
                    Assert.IsNotNull(r.sharedMaterial, label + ": material");
                    b.Encapsulate(r.bounds);
                }
                Assert.AreEqual(0f, b.min.y, 0.12f, label + ": stands on the ground");
                Assert.Greater(b.size.y, 0.6f, label + ": tall enough");
                Assert.Less(b.size.y, 1.9f, label + ": not too tall");
                Assert.Greater(b.size.z, 0.9f, label + ": long enough");
                Assert.Less(b.size.z, 3.2f, label + ": not too long");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EveryOptionBuildsAHealthyPet()
        {
            for (int i = 0; i < PetConfig.SpeciesCount; i++) AssertHealthy(new PetConfig { species = i }, "species " + i);
            for (int i = 0; i < PetConfig.CoatCount; i++) AssertHealthy(new PetConfig { coat = i }, "coat " + i);
            for (int i = 0; i < PetConfig.PatternCount; i++)
            {
                AssertHealthy(new PetConfig { pattern = i }, "dog pattern " + i);
                AssertHealthy(new PetConfig { species = 1, pattern = i }, "cat pattern " + i);
            }
            for (int i = 0; i < PetConfig.SizeCount; i++) AssertHealthy(new PetConfig { size = i }, "size " + i);
            for (int i = 0; i < PetConfig.EarCount; i++) AssertHealthy(new PetConfig { ears = i }, "ears " + i);
            for (int i = 0; i < PetConfig.CollarCount; i++) AssertHealthy(new PetConfig { collar = i }, "collar " + i);
        }

        [Test]
        public void LargerPetsAreReallyLarger()
        {
            float Height(int size)
            {
                var go = PetFactory.Build(new PetConfig { size = size });
                go.hideFlags = HideFlags.HideAndDontSave;
                var b = go.GetComponentsInChildren<MeshRenderer>()[0].bounds;
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) b.Encapsulate(r.bounds);
                Object.DestroyImmediate(go);
                return b.size.y;
            }
            Assert.Less(Height(0), Height(1));
            Assert.Less(Height(1), Height(2));
        }

        [Test]
        public void CollarAppearsOnlyWhenChosen()
        {
            var none = PetFactory.Build(new PetConfig { collar = 0 });
            var red = PetFactory.Build(new PetConfig { collar = 1 });
            none.hideFlags = red.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                Assert.IsNull(none.transform.Find("Body/Collar"));
                Assert.IsNotNull(red.transform.Find("Body/Collar"));
            }
            finally
            {
                Object.DestroyImmediate(none);
                Object.DestroyImmediate(red);
            }
        }

        [Test]
        public void ConeFacesOutwards()
        {
            var m = MeshFactory.Cone(0.1f, 0.3f);
            var v = m.vertices; var n = m.normals; var t = m.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                var g = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                Assert.Greater(Vector3.Dot(g, n[t[i]] + n[t[i + 1]] + n[t[i + 2]]), 0f);
            }
        }
    }
}
