using System.Collections;
using System.Linq;
using LostPetNetwork.Services.Auth;
using LostPetNetwork.Services.Profile;
using LostPetNetwork.Services.Reports;
using LostPetNetwork.Services.Adoption;
using LostPetNetwork.Services.Sensors;
using LostPetNetwork.Services.Sos;
using LostPetNetwork.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LostPetNetwork.Tests.PlayMode
{
    /// <summary>Drives the SOS screens in the real Main scene with demo data, a fake position and a fake user.</summary>
    public class SosWorkflowTests
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

        IEnumerator Boot(string userId = "sos-tester", bool withLocation = true)
        {
            SensorHub.Reset();
            if (withLocation)
            {
                var fake = new FakeLocationProvider();
                SensorHub.Location = fake;
                fake.Push(new GeoFix(36.8065, 10.1815));
            }
            PlayerPrefs.DeleteKey("lpn.auth.session.v1");
            SetSession(null);
            ReportServices.Repository = new MockReportRepository(latencyMs: 0);
            AdoptionServices.Repository = new MockAdoptionRepository(latencyMs: 0);
            SosServices.Repository = new MockSosRepository(latencyMs: 0);
            AppSession.Reset();
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            _manager = Object.FindAnyObjectByType<ScreenManager>();
            SetCachedProfile(new UserProfile { id = userId, display_name = "Sos Tester", city = "Tunis", created_at = "2026-10-18T10:00:00Z" });
            SetSession(new AuthSession
            {
                accessToken = "", refreshToken = "r", userId = userId, email = "sos@example.com",
                expiresAtUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600
            });
            _manager.Show(ScreenId.Home);
            yield return Frames(3);
        }

        [TearDown]
        public void TearDown()
        {
            SensorHub.Reset();
            SetSession(null);
            SetCachedProfile(null);
            ReportServices.Repository = new MockReportRepository();
            AdoptionServices.Repository = new MockAdoptionRepository();
            SosServices.Repository = new MockSosRepository();
            AppSession.Reset();
        }

        [UnityTest]
        public IEnumerator Home_has_an_SOS_card_that_opens_the_rescue_hub_with_nearest_open_alert_first()
        {
            yield return Boot();
            Click("Home/Viewport/Content/SosCard");
            yield return Frames(6);
            Assert.AreEqual(ScreenId.Sos, _manager.Current);
            var rows = Find("Sos/Viewport/Content/List").GetComponentsInChildren<SosRowView>();
            Assert.AreEqual(2, rows.Length);
            Assert.AreEqual(SosStatus.Open, rows[0].Alert.Status, "open alerts come before ones already being handled");
            StringAssert.Contains("2 open alerts", Label("Sos/Viewport/Content/Count"));
            StringAssert.Contains("away", rows[0].transform.Find("Face/Meta").GetComponent<TMPro.TMP_Text>().text);
        }

        [UnityTest]
        public IEnumerator Sending_an_SOS_validates_attaches_the_position_and_shows_the_live_alert()
        {
            yield return Boot();
            _manager.Show(ScreenId.Sos);
            yield return Frames(4);
            Click("Sos/Viewport/Content/SosCard/SendButton");
            yield return Frames(6);
            Assert.AreEqual(ScreenId.SosCreate, _manager.Current);
            Assert.IsTrue(Active("SosCreate/Viewport/Content/Form"));
            Assert.IsFalse(Active("SosCreate/Viewport/Content/Sent"));

            Find("SosCreate/Viewport/Content/Form/Note/Box").GetComponent<TMPro.TMP_InputField>().text = "Injured cat under a car";
            Click("SosCreate/Viewport/Content/Form/SendButton");
            yield return Frames(6);

            Assert.IsFalse(Active("SosCreate/Viewport/Content/Form"));
            Assert.IsTrue(Active("SosCreate/Viewport/Content/Sent"));
            Assert.AreEqual("Your alert is live", Label("SosCreate/Viewport/Content/Sent/SentCard/Title"));
            var mine = SosServices.Repository.GetActiveAsync().Result.Single(a => a.ReporterId == "sos-tester");
            Assert.AreEqual("Injured cat under a car", mine.Note);
            Assert.AreEqual(36.8065, mine.Latitude, 1e-6);
            Assert.AreEqual(SosStatus.Open, mine.Status);

            Click("SosCreate/Viewport/Content/Sent/ResolveButton");
            yield return Frames(6);
            Assert.AreEqual(ScreenId.Sos, _manager.Current);
            Assert.AreEqual(0, SosServices.Repository.GetActiveAsync().Result.Count(a => a.ReporterId == "sos-tester"));
            StringAssert.Contains("resolved", Label("Sos/Viewport/Content/Banner"));
        }

        [UnityTest]
        public IEnumerator Opening_the_form_again_shows_the_alert_that_is_already_open()
        {
            yield return Boot();
            SosServices.Repository.CreateAsync(new SosDraft { HasLocation = true, Latitude = 36.8, Longitude = 10.1, Note = "x" }, "sos-tester", "Me").Wait();
            _manager.Show(ScreenId.SosCreate);
            yield return Frames(6);
            Assert.IsTrue(Active("SosCreate/Viewport/Content/Sent"));
            Assert.IsFalse(Active("SosCreate/Viewport/Content/Form"));
        }

        [UnityTest]
        public IEnumerator Without_a_position_the_send_is_refused_outside_the_editor_test_fallback()
        {
            yield return Boot(withLocation: false);
            _manager.Show(ScreenId.SosCreate);
            yield return Frames(8);
            var screen = Object.FindAnyObjectByType<SosCreateScreen>();
#if UNITY_EDITOR
            Assert.IsTrue(screen.Draft.HasLocation, "the Editor uses a clearly labelled test position");
            StringAssert.Contains("Editor", Label("SosCreate/Viewport/Content/Form/LocationCard/Location"));
#else
            Assert.IsFalse(screen.Draft.HasLocation);
#endif
        }

        [UnityTest]
        public IEnumerator A_rescuer_can_claim_an_open_alert_once_then_resolve_it()
        {
            yield return Boot("rescuer");
            AppSession.SelectedAlert = SosServices.Repository.GetActiveAsync().Result.First(a => a.Status == SosStatus.Open);
            _manager.Show(ScreenId.SosDetails);
            yield return Frames(4);
            Assert.AreEqual("I'm on my way", Label("SosDetails/ActionBar/ActionButton"));
            StringAssert.Contains("NEEDS HELP", Label("SosDetails/Scroll/Viewport/Content/Header/Status"));
            Click("SosDetails/ActionBar/ActionButton");
            yield return Frames(4);
            Assert.AreEqual(SosStatus.Responding, AppSession.SelectedAlert.Status);
            Assert.AreEqual("rescuer", AppSession.SelectedAlert.ResponderId);
            Assert.AreEqual("Mark as resolved", Label("SosDetails/ActionBar/ActionButton"));
            Click("SosDetails/ActionBar/ActionButton");
            yield return Frames(4);
            Assert.AreEqual(SosStatus.Resolved, AppSession.SelectedAlert.Status);
            Assert.IsFalse(Active("SosDetails/ActionBar/ActionButton"));
        }

        [UnityTest]
        public IEnumerator A_third_person_cannot_act_on_an_alert_someone_else_is_handling()
        {
            yield return Boot("bystander");
            AppSession.SelectedAlert = SosServices.Repository.GetActiveAsync().Result.First(a => a.Status == SosStatus.Responding);
            _manager.Show(ScreenId.SosDetails);
            yield return Frames(4);
            Assert.IsFalse(Active("SosDetails/ActionBar/ActionButton"));
            StringAssert.Contains("already", Label("SosDetails/ActionBar/Hint"));
        }

        [UnityTest]
        public IEnumerator Shaking_opens_the_SOS_form_only_when_signed_in_and_not_already_there()
        {
            yield return Boot();
            var shake = _manager.GetComponent<ShakeToSos>();
            Assert.IsNotNull(shake, "the shake listener is attached");
            shake.OnShake();
            yield return Frames(3);
            Assert.AreEqual(ScreenId.SosCreate, _manager.Current);
            int depth = _manager.HistoryDepth;
            shake.OnShake();
            yield return Frames(2);
            Assert.AreEqual(ScreenId.SosCreate, _manager.Current);
            Assert.AreEqual(depth, _manager.HistoryDepth, "no second form is pushed");

            _manager.Show(ScreenId.Camera);
            yield return Frames(2);
            shake.OnShake();
            Assert.AreEqual(ScreenId.Camera, _manager.Current, "never interrupts the camera");

            SetSession(null);
            _manager.Show(ScreenId.SignIn);
            yield return Frames(2);
            shake.OnShake();
            Assert.AreEqual(ScreenId.SignIn, _manager.Current, "signed-out users stay on the sign-in screen");
        }
    }
}
