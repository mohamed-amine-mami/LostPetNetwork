using LostPetNetwork.Services.Avatar;
using LostPetNetwork.UI;
using NUnit.Framework;
using UnityEngine;

namespace LostPetNetwork.Tests
{
    public class AvatarConfigTests
    {
        [Test]
        public void ClampKeepsEveryFieldInRange()
        {
            var c = new AvatarConfig { skin = 99, hairStyle = -5, hairColor = 50, eyes = 9, outfitStyle = 7, outfitColor = -1, accessory = 12 };
            c.Clamp();
            Assert.AreEqual(AvatarConfig.SkinCount - 1, c.skin);
            Assert.AreEqual(0, c.hairStyle);
            Assert.AreEqual(AvatarConfig.HairColorCount - 1, c.hairColor);
            Assert.AreEqual(AvatarConfig.EyeCount - 1, c.eyes);
            Assert.AreEqual(AvatarConfig.OutfitStyleCount - 1, c.outfitStyle);
            Assert.AreEqual(0, c.outfitColor);
            Assert.AreEqual(AvatarConfig.AccessoryCount - 1, c.accessory);
        }

        [Test]
        public void JsonRoundTripKeepsAllChoices()
        {
            var c = new AvatarConfig { skin = 4, hairStyle = 3, hairColor = 5, eyes = 2, outfitStyle = 1, outfitColor = 6, accessory = 4 };
            Assert.IsTrue(AvatarConfig.TryParse(c.ToJson(), out var back));
            Assert.IsTrue(c.SameAs(back));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("not json")]
        [TestCase("{\"skin\":")]
        public void BadJsonIsRejected(string json) => Assert.IsFalse(AvatarConfig.TryParse(json, out _));

        [Test]
        public void TamperedJsonIsClamped()
        {
            Assert.IsTrue(AvatarConfig.TryParse("{\"skin\":500,\"hairStyle\":-9}", out var c));
            Assert.AreEqual(AvatarConfig.SkinCount - 1, c.skin);
            Assert.AreEqual(0, c.hairStyle);
        }

        [Test]
        public void RandomStaysInRange()
        {
            var rng = new System.Random(7);
            for (int i = 0; i < 200; i++)
            {
                var c = AvatarConfig.Random(rng);
                var clamped = c.Clone().Clamp();
                Assert.IsTrue(c.SameAs(clamped));
            }
        }

        [Test]
        public void ColourPalettesMatchOptionCounts()
        {
            Assert.AreEqual(AvatarConfig.SkinCount, AvatarPalette.Skin.Length);
            Assert.AreEqual(AvatarConfig.HairColorCount, AvatarPalette.Hair.Length);
            Assert.AreEqual(AvatarConfig.EyeCount, AvatarPalette.Eyes.Length);
            Assert.AreEqual(AvatarConfig.OutfitColorCount, AvatarPalette.Outfit.Length);
            Assert.AreEqual(AvatarConfig.HairStyleCount, AvatarPalette.HairStyleNames.Length);
            Assert.AreEqual(AvatarConfig.OutfitStyleCount, AvatarPalette.OutfitStyleNames.Length);
            Assert.AreEqual(AvatarConfig.AccessoryCount, AvatarPalette.AccessoryNames.Length);
        }
    }

    public class AvatarStoreTests
    {
        const string UserA = "test-avatar-user-a";
        const string UserB = "test-avatar-user-b";
        readonly PlayerPrefsAvatarStore _store = new PlayerPrefsAvatarStore();

        [TearDown]
        public void TearDown()
        {
            _store.Delete(UserA);
            _store.Delete(UserB);
        }

        [Test]
        public void NothingSavedMeansNull() => Assert.IsNull(_store.Load(UserA));

        [Test]
        public void SavedAvatarComesBackAndStaysPerUser()
        {
            var mine = new AvatarConfig { skin = 5, accessory = 2 };
            _store.Save(UserA, mine);
            Assert.IsTrue(mine.SameAs(_store.Load(UserA)));
            Assert.IsNull(_store.Load(UserB));
        }

        [Test]
        public void DeleteRemovesTheAvatar()
        {
            _store.Save(UserA, AvatarConfig.Default());
            _store.Delete(UserA);
            Assert.IsNull(_store.Load(UserA));
        }
    }

    public class AvatarMeshTests
    {
        static void AssertFacesOutward(Mesh mesh, string name)
        {
            var v = mesh.vertices;
            var n = mesh.normals;
            var t = mesh.triangles;
            Assert.Greater(t.Length, 0, name + " has triangles");
            for (int i = 0; i < t.Length; i += 3)
            {
                var geometric = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                var normal = n[t[i]] + n[t[i + 1]] + n[t[i + 2]];
                Assert.Greater(Vector3.Dot(geometric, normal), 0f, $"{name}: triangle {i / 3} faces inward");
            }
        }

        [Test]
        public void SphereIsAUnitSphereFacingOutwards()
        {
            var m = MeshFactory.Sphere();
            Assert.AreEqual(1f, m.bounds.extents.x, 0.02f);
            Assert.AreEqual(1f, m.bounds.extents.y, 0.001f);
            AssertFacesOutward(m, "sphere");
        }

        [Test]
        public void CapCoversOnlyTheTop()
        {
            var m = MeshFactory.Cap(74f);
            Assert.AreEqual(1f, m.bounds.max.y, 0.001f);
            Assert.Greater(m.bounds.min.y, 0.2f);
            AssertFacesOutward(m, "cap");
        }

        [Test]
        public void CapsuleHasTheRequestedHeight()
        {
            var m = MeshFactory.Capsule(0.2f, 1f);
            Assert.AreEqual(1f, m.bounds.size.y, 0.001f);
            Assert.AreEqual(0.4f, m.bounds.size.x, 0.01f);
            AssertFacesOutward(m, "capsule");
        }

        [Test]
        public void TorusRingAndArcFaceOutwards()
        {
            AssertFacesOutward(MeshFactory.Torus(0.3f, 0.05f), "torus");
            AssertFacesOutward(MeshFactory.Torus(0.3f, 0.05f, 190f, 160f), "arc");
        }

        [Test]
        public void MeshesAreCached() => Assert.AreSame(MeshFactory.Sphere(), MeshFactory.Sphere());
    }

    public class AvatarFactoryTests
    {
        static GameObject Build(AvatarConfig c)
        {
            var go = AvatarFactory.Build(c);
            go.hideFlags = HideFlags.HideAndDontSave;
            return go;
        }

        static void AssertHealthy(AvatarConfig c, string label)
        {
            var go = Build(c);
            try
            {
                var renderers = go.GetComponentsInChildren<MeshRenderer>();
                Assert.GreaterOrEqual(renderers.Length, 15, label + ": body, head and face parts");
                Bounds b = renderers[0].bounds;
                foreach (var r in renderers)
                {
                    Assert.IsNotNull(r.sharedMaterial, label + ": material");
                    Assert.IsNotNull(r.GetComponent<MeshFilter>().sharedMesh, label + ": mesh");
                    b.Encapsulate(r.bounds);
                }
                Assert.Greater(b.size.y, 1.9f, label + ": tall enough");
                Assert.Less(b.size.y, 2.8f, label + ": not too tall");
                Assert.Less(b.size.x, 2.2f, label + ": not too wide");
                Assert.AreEqual(0f, b.min.y, 0.15f, label + ": stands on the ground");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EveryOptionBuildsAHealthyFigure()
        {
            for (int i = 0; i < AvatarConfig.SkinCount; i++) AssertHealthy(new AvatarConfig { skin = i }, "skin " + i);
            for (int i = 0; i < AvatarConfig.HairStyleCount; i++) AssertHealthy(new AvatarConfig { hairStyle = i }, "hair style " + i);
            for (int i = 0; i < AvatarConfig.HairColorCount; i++) AssertHealthy(new AvatarConfig { hairColor = i }, "hair colour " + i);
            for (int i = 0; i < AvatarConfig.EyeCount; i++) AssertHealthy(new AvatarConfig { eyes = i }, "eyes " + i);
            for (int i = 0; i < AvatarConfig.OutfitStyleCount; i++) AssertHealthy(new AvatarConfig { outfitStyle = i }, "outfit " + i);
            for (int i = 0; i < AvatarConfig.OutfitColorCount; i++) AssertHealthy(new AvatarConfig { outfitColor = i }, "outfit colour " + i);
            for (int i = 0; i < AvatarConfig.AccessoryCount; i++) AssertHealthy(new AvatarConfig { accessory = i }, "accessory " + i);
        }

        [Test]
        public void OutOfRangeSettingsStillBuild() =>
            AssertHealthy(new AvatarConfig { skin = 99, hairStyle = 99, accessory = -3 }, "out of range");

        [Test]
        public void NullConfigBuildsTheDefault() =>
            AssertHealthy(null, "null");

        [Test]
        public void DifferentChoicesChangeTheLook()
        {
            var a = Build(new AvatarConfig { skin = 0, outfitColor = 0 });
            var b = Build(new AvatarConfig { skin = 5, outfitColor = 3 });
            try
            {
                var ca = a.GetComponentsInChildren<MeshRenderer>()[0].sharedMaterial.GetColor("_BaseColor");
                var cb = b.GetComponentsInChildren<MeshRenderer>()[0].sharedMaterial.GetColor("_BaseColor");
                Assert.AreNotEqual(ca, cb);
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }
    }
}
