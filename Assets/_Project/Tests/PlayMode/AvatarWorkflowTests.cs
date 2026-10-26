using System.Collections;
using LostPetNetwork.Services.Auth;
using LostPetNetwork.Services.Avatar;
using LostPetNetwork.Services.Profile;
using LostPetNetwork.Services.Reports;
using LostPetNetwork.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LostPetNetwork.Tests.PlayMode
{
    /// <summary>Drives the avatar creator in the real Main scene with a fake signed-in user.</summary>
    public class AvatarWorkflowTests
    {
        const string UserId = "test-avatar-flow-user";
        const string Profile = "Profile/Viewport/Content/View/";
        const string Creator = "AvatarCreator/Viewport/Content/";

        ScreenManager _manager;

        static Transform Find(string path)
        {
            var canvas = GameObject.Find("Canvas");
            Assert.IsNotNull(canvas, "Canvas missing");
            var t = canvas.transform.Find("SafeArea/" + path);
            Assert.IsNotNull(t, "UI path not found: " + path);
            return t;
        }

        static void Click(string path) => Find(path).GetComponent<Button>().onClick.Invoke();
        static bool Active(string path) => Find(path).gameObject.activeInHierarchy;
        static string Label(string path) => Find(path).GetComponentInChildren<TMPro.TMP_Text>(true).text;

        static void SetSession(AuthSession s) =>
            typeof(AuthService).GetProperty("Session").GetSetMethod(true).Invoke(AuthService.Instance, new object[] { s });

        static void SetCachedProfile(UserProfile p) =>
            typeof(ProfileService).GetProperty("Cached").GetSetMethod(true).Invoke(ProfileService.Instance, new object[] { p });

        IEnumerator Frames(int n = 3) { for (int i = 0; i < n; i++) yield return null; }

        IEnumerator BootOnProfile()
        {
            AvatarServices.Store.Delete(UserId);
            PlayerPrefs.DeleteKey("lpn.auth.session.v1");
            SetSession(null);
            ReportServices.Repository = new MockReportRepository(latencyMs: 0);
            AppSession.Reset();
            CameraScreen.SkipHardware = true;
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            _manager = Object.FindAnyObjectByType<ScreenManager>();
            SetCachedProfile(new UserProfile { id = UserId, display_name = "Avatar Tester", city = "Tunis", created_at = "2026-10-18T10:00:00Z" });
            SetSession(new AuthSession
            {
                accessToken = "", refreshToken = "r", userId = UserId, email = "avatar@example.com",
                expiresAtUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600
            });
            _manager.Show(ScreenId.Profile);
            yield return Frames(4);
        }

        [TearDown]
        public void TearDown()
        {
            AvatarServices.Store.Delete(UserId);
            SetSession(null);
            SetCachedProfile(null);
            CameraScreen.SkipHardware = false;
            ReportServices.Repository = new MockReportRepository();
        }

        [UnityTest]
        public IEnumerator Profile_offers_to_create_an_avatar_and_opens_the_creator()
        {
            yield return BootOnProfile();
            Assert.AreEqual("Create my 3D avatar", Label(Profile + "AvatarButton"));
            Click(Profile + "AvatarButton");
            yield return Frames(3);
            Assert.AreEqual(ScreenId.AvatarCreator, _manager.Current);
            Assert.IsFalse(Active("TabBar"), "tab bar hidden while designing");
            var screen = Object.FindAnyObjectByType<AvatarCreatorScreen>();
            Assert.IsNotNull(screen);
            var preview = screen.GetComponentInChildren<Preview3D>();
            Assert.IsNotNull(preview.Content, "a 3D figure is on the stage");
        }

        [UnityTest]
        public IEnumerator The_figure_is_really_drawn_in_the_preview()
        {
            yield return BootOnProfile();
            Click(Profile + "AvatarButton");
            yield return Frames(4);
            var preview = Object.FindAnyObjectByType<AvatarCreatorScreen>().GetComponentInChildren<Preview3D>();
            var shot = preview.Capture();
            int opaque = 0;
            foreach (var p in shot.GetPixels32()) if (p.a > 200) opaque++;
            int total = shot.width * shot.height;
            Object.Destroy(shot);
            Assert.Greater(opaque, total / 12, "the avatar fills a good part of the picture");
            Assert.Less(opaque, total * 3 / 4, "the background stays transparent");
        }

        [UnityTest]
        public IEnumerator Choosing_options_changes_the_avatar_and_the_figure()
        {
            yield return BootOnProfile();
            Click(Profile + "AvatarButton");
            yield return Frames(3);
            var screen = Object.FindAnyObjectByType<AvatarCreatorScreen>();
            var before = screen.Config;
            var figureBefore = screen.GetComponentInChildren<Preview3D>().Content;

            Click(Creator + "Panel-Skin/Option-Skin/Swatches/Swatch-5");
            Assert.AreEqual(5, screen.Config.skin);
            Assert.AreNotSame(figureBefore, screen.GetComponentInChildren<Preview3D>().Content, "figure rebuilt");

            // Other categories show their own options.
            Click("AvatarCreator/Viewport/Content/Categories/Chip-Hair");
            yield return Frames(2);
            Assert.IsTrue(Active(Creator + "Panel-Hair"));
            Assert.IsFalse(Active(Creator + "Panel-Skin"));
            Click(Creator + "Panel-Hair/Option-HairStyle/Chips/Chip-Curly");
            Click(Creator + "Panel-Hair/Option-HairColor/Swatches/Swatch-3");
            Assert.AreEqual(4, screen.Config.hairStyle);
            Assert.AreEqual(3, screen.Config.hairColor);

            Click("AvatarCreator/Viewport/Content/Categories/Chip-Extras");
            yield return Frames(2);
            Click(Creator + "Panel-Extras/Option-Accessory/Chips/Chip-Glasses");
            Assert.AreEqual(1, screen.Config.accessory);
            Assert.IsFalse(before.SameAs(screen.Config));
        }

        [UnityTest]
        public IEnumerator Surprise_me_picks_a_new_look()
        {
            yield return BootOnProfile();
            Click(Profile + "AvatarButton");
            yield return Frames(3);
            var screen = Object.FindAnyObjectByType<AvatarCreatorScreen>();
            var start = screen.Config;
            bool changed = false;
            for (int i = 0; i < 8 && !changed; i++)
            {
                Click(Creator + "Actions/RandomButton");
                changed = !start.SameAs(screen.Config);
            }
            Assert.IsTrue(changed);
        }

        [UnityTest]
        public IEnumerator Save_stores_the_avatar_and_the_profile_shows_it()
        {
            yield return BootOnProfile();
            Click(Profile + "AvatarButton");
            yield return Frames(3);
            Click(Creator + "Panel-Skin/Option-Skin/Swatches/Swatch-4");
            Click(Creator + "SaveButton");
            yield return Frames(3);

            Assert.AreEqual(ScreenId.Profile, _manager.Current);
            var saved = AvatarServices.Store.Load(UserId);
            Assert.IsNotNull(saved);
            Assert.AreEqual(4, saved.skin);
            Assert.AreEqual("Avatar saved.", Label("Profile/Viewport/Content/Banner"));
            Assert.AreEqual("Edit my 3D avatar", Label(Profile + "AvatarButton"));

            yield return Frames(6); // portrait renders asynchronously
            Assert.IsTrue(Active(Profile + "Head/Avatar/Portrait"), "portrait replaces the initials");
            Assert.IsFalse(Active(Profile + "Head/Avatar/Initials"));
        }

        [UnityTest]
        public IEnumerator Cancel_leaves_nothing_saved()
        {
            yield return BootOnProfile();
            Click(Profile + "AvatarButton");
            yield return Frames(3);
            Click(Creator + "Panel-Skin/Option-Skin/Swatches/Swatch-1");
            Click(Creator + "CancelButton");
            yield return Frames(3);
            Assert.AreEqual(ScreenId.Profile, _manager.Current);
            Assert.IsNull(AvatarServices.Store.Load(UserId));
            Assert.IsTrue(Active("TabBar"));
        }

        [UnityTest]
        public IEnumerator Reopening_the_creator_starts_from_the_saved_avatar()
        {
            yield return BootOnProfile();
            AvatarServices.Store.Save(UserId, new AvatarConfig { skin = 3, hairStyle = 2, accessory = 3 });
            Click(Profile + "AvatarButton");
            yield return Frames(3);
            var screen = Object.FindAnyObjectByType<AvatarCreatorScreen>();
            Assert.AreEqual(3, screen.Config.skin);
            Assert.AreEqual(2, screen.Config.hairStyle);
            Assert.AreEqual(3, screen.Config.accessory);
        }
    }
}
