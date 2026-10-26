using System.Collections;
using LostPetNetwork.Services.Auth;
using LostPetNetwork.Services.Pets;
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
    /// <summary>Drives the 3D pet look flow (report form, creator, details) in the real Main scene.</summary>
    public class PetWorkflowTests
    {
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

        IEnumerator Boot()
        {
            PlayerPrefs.DeleteKey("lpn.auth.session.v1");
            SetSession(null);
            ReportServices.Repository = new MockReportRepository(latencyMs: 0);
            AppSession.Reset();
            CameraScreen.SkipHardware = true;
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            _manager = Object.FindAnyObjectByType<ScreenManager>();
            SetCachedProfile(new UserProfile { id = "pet-tester", display_name = "Pet Tester", city = "Tunis", created_at = "2026-10-18T10:00:00Z" });
            SetSession(new AuthSession
            {
                accessToken = "", refreshToken = "r", userId = "pet-tester", email = "pet@example.com",
                expiresAtUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600
            });
            yield return Frames(2);
        }

        [TearDown]
        public void TearDown()
        {
            SetSession(null);
            SetCachedProfile(null);
            CameraScreen.SkipHardware = false;
            ReportServices.Repository = new MockReportRepository();
            AppSession.Reset();
        }

        [UnityTest]
        public IEnumerator Report_form_opens_the_pet_creator_and_keeps_typed_text()
        {
            yield return Boot();
            AppSession.StartDraft(ReportType.Lost);
            _manager.Show(ScreenId.Report);
            yield return Frames(3);
            Find("Report/Form/Viewport/Content/Location/Box").GetComponent<TMPro.TMP_InputField>().text = "Parc du Belvedere";
            Click("Report/Form/Viewport/Content/Pet3DBox");
            yield return Frames(3);
            Assert.AreEqual(ScreenId.PetCreator, _manager.Current);
            var screen = Object.FindAnyObjectByType<PetCreatorScreen>();
            Assert.IsNotNull(screen.GetComponentInChildren<Preview3D>().Content, "pet is on the stage");

            Click("PetCreator/Viewport/Content/Categories/Chip-Coat");
            yield return Frames(2);
            Click("PetCreator/Viewport/Content/Panel-Coat/Option-PetCoat/Swatches/Swatch-3");
            Click("PetCreator/Viewport/Content/Panel-Coat/Option-PetPattern/Chips/Chip-Socks");
            Assert.AreEqual(3, screen.Config.coat);
            Assert.AreEqual(2, screen.Config.pattern);

            Click("PetCreator/SaveButton".Replace("PetCreator/", "PetCreator/Viewport/Content/"));
            yield return Frames(3);
            Assert.AreEqual(ScreenId.Report, _manager.Current);
            Assert.IsTrue(PetConfig.TryParse(AppSession.Draft.PetAvatarJson, out var saved));
            Assert.AreEqual(3, saved.coat);
            Assert.AreEqual("Parc du Belvedere", AppSession.Draft.LocationText, "typed text was kept");
            Assert.AreEqual("3D look added — tap to edit", Label("Report/Form/Viewport/Content/Pet3DBox/Text/Caption"));
        }

        [UnityTest]
        public IEnumerator Choosing_a_cat_hides_the_ear_options()
        {
            yield return Boot();
            AppSession.StartDraft(ReportType.Found);
            _manager.Show(ScreenId.PetCreator);
            yield return Frames(3);
            var screen = Object.FindAnyObjectByType<PetCreatorScreen>();
            Click("PetCreator/Viewport/Content/Categories/Chip-Extras");
            yield return Frames(2);
            Assert.IsTrue(Active("PetCreator/Viewport/Content/Panel-Extras/Option-PetEars"));
            Click("PetCreator/Viewport/Content/Categories/Chip-Pet");
            yield return Frames(2);
            Click("PetCreator/Viewport/Content/Panel-Pet/Option-PetSpecies/Chips/Chip-Cat");
            Assert.IsTrue(screen.Config.IsCat);
            Click("PetCreator/Viewport/Content/Categories/Chip-Extras");
            yield return Frames(2);
            Assert.IsFalse(Active("PetCreator/Viewport/Content/Panel-Extras/Option-PetEars"));
        }

        [UnityTest]
        public IEnumerator Details_of_a_demo_report_shows_its_3D_pet()
        {
            yield return Boot();
            var all = ReportServices.Repository.GetAllAsync().Result;
            AppSession.SelectedReport = all[0];
            _manager.Show(ScreenId.Details);
            yield return Frames(4);
            Assert.IsTrue(Active("Details/Scroll/Viewport/Content/Body/Pet3DCard"));
            var preview = Find("Details/Scroll/Viewport/Content/Body/Pet3DCard/Preview").GetComponent<Preview3D>();
            Assert.IsNotNull(preview.Content);
            var shot = preview.Capture();
            int opaque = 0;
            foreach (var p in shot.GetPixels32()) if (p.a > 200) opaque++;
            int total = shot.width * shot.height;
            Object.Destroy(shot);
            Assert.Greater(opaque, total / 30, "the pet is drawn");
        }

        [UnityTest]
        public IEnumerator Details_without_a_3D_look_hides_the_card()
        {
            yield return Boot();
            var report = ReportServices.Repository.GetAllAsync().Result[0];
            report.PetAvatarJson = null;
            AppSession.SelectedReport = report;
            _manager.Show(ScreenId.Details);
            yield return Frames(3);
            Assert.IsFalse(Active("Details/Scroll/Viewport/Content/Body/Pet3DCard"));
        }

        [UnityTest]
        public IEnumerator Removing_the_3D_look_clears_it_from_the_draft()
        {
            yield return Boot();
            AppSession.StartDraft(ReportType.Lost);
            AppSession.Draft.PetAvatarJson = new PetConfig().ToJson();
            _manager.Show(ScreenId.Report);
            yield return Frames(2);
            _manager.Push(ScreenId.PetCreator);
            yield return Frames(3);
            Click("PetCreator/Viewport/Content/RemoveButton");
            yield return Frames(3);
            Assert.IsNull(AppSession.Draft.PetAvatarJson);
            Assert.AreEqual(ScreenId.Report, _manager.Current);
        }
    }
}
