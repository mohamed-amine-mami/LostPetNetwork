using System.Collections;
using System.Linq;
using LostPetNetwork.Services.Auth;
using LostPetNetwork.Services.Profile;
using LostPetNetwork.Services.Reports;
using LostPetNetwork.Services.Adoption;
using LostPetNetwork.Services.Sensors;
using LostPetNetwork.Services.Sos;
using LostPetNetwork.Services.Vets;
using LostPetNetwork.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LostPetNetwork.Tests.PlayMode
{
    /// <summary>Drives the "Vets near you" screen in the real Main scene with demo clinics and a fake position.</summary>
    public class VetWorkflowTests
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

        IEnumerator Boot(string userId = "vet-tester", bool withLocation = true)
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
            VetServices.Repository = new MockVetRepository(latencyMs: 0);
            AppSession.Reset();
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            _manager = Object.FindAnyObjectByType<ScreenManager>();
            SetCachedProfile(new UserProfile { id = userId, display_name = "Vet Tester", city = "Tunis", created_at = "2026-10-18T10:00:00Z" });
            SetSession(new AuthSession
            {
                accessToken = "", refreshToken = "r", userId = userId, email = "vet@example.com",
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
            VetServices.Repository = new MockVetRepository();
            AppSession.Reset();
        }

        [UnityTest]
        public IEnumerator The_rescue_hub_opens_the_vet_list_nearest_first_and_back_returns()
        {
            yield return Boot();
            _manager.Show(ScreenId.Sos);
            yield return Frames(4);
            Click("Sos/Viewport/Content/VetsButton");
            yield return Frames(6);
            Assert.AreEqual(ScreenId.Vets, _manager.Current);
            var rows = Find("Vets/Viewport/Content/List").GetComponentsInChildren<VetRowView>();
            Assert.AreEqual(3, rows.Length);
            // The fake user is in central Tunis: the Ennasr clinic is nearest, the Manouba one is farthest.
            Assert.AreEqual("vet-1", rows[0].Clinic.Id);
            Assert.AreEqual("vet-3", rows[2].Clinic.Id);
            StringAssert.Contains("km away", rows[0].transform.Find("Face/Top/Distance").GetComponent<TMPro.TMP_Text>().text);
            StringAssert.Contains("3 clinics", Label("Vets/Viewport/Content/Count"));
            Assert.IsTrue(rows[1].transform.Find("Face/Top/OpenAllDay").gameObject.activeSelf, "the 24/7 clinic is tagged");
            Assert.IsFalse(rows[0].transform.Find("Face/Top/OpenAllDay").gameObject.activeSelf);
            Assert.IsTrue(rows[0].transform.Find("Face/Actions/CallButton").gameObject.activeSelf);
            Assert.IsFalse(rows[2].transform.Find("Face/Actions/CallButton").gameObject.activeSelf, "no phone number, no Call button");
            Assert.IsTrue(rows[2].transform.Find("Face/Actions/DirectionsButton").gameObject.activeSelf);

            Click("Vets/Viewport/Content/Header/BackButton");
            yield return Frames(3);
            Assert.AreEqual(ScreenId.Sos, _manager.Current);
        }

        [UnityTest]
        public IEnumerator The_open_24_7_filter_keeps_only_clinics_open_all_day()
        {
            yield return Boot();
            _manager.Show(ScreenId.Vets);
            yield return Frames(6);
            Find("Vets/Viewport/Content/Filters").GetChild(1).GetComponent<Button>().onClick.Invoke(); // "Open 24/7"
            yield return Frames(3);
            var rows = Find("Vets/Viewport/Content/List").GetComponentsInChildren<VetRowView>();
            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual("vet-2", rows[0].Clinic.Id);
            Find("Vets/Viewport/Content/Filters").GetChild(0).GetComponent<Button>().onClick.Invoke(); // "All"
            yield return Frames(3);
            Assert.AreEqual(3, Find("Vets/Viewport/Content/List").GetComponentsInChildren<VetRowView>().Length);
        }

        [UnityTest]
        public IEnumerator An_empty_directory_shows_a_friendly_message_and_a_failure_shows_a_banner()
        {
            yield return Boot();
            VetServices.Repository = new MockVetRepository(latencyMs: 0, seed: false);
            _manager.Show(ScreenId.Vets);
            yield return Frames(6);
            Assert.IsTrue(Active("Vets/Viewport/Content/EmptyState"));
            StringAssert.Contains("No vets", Label("Vets/Viewport/Content/EmptyState"));

            VetServices.Repository = new FailingVets();
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("not reachable"));
            _manager.Show(ScreenId.Home);
            yield return Frames(2);
            _manager.Show(ScreenId.Vets);
            yield return Frames(6);
            Assert.IsTrue(Active("Vets/Viewport/Content/Banner"), "the error is shown, not swallowed");
        }

        [UnityTest]
        public IEnumerator Without_a_position_the_list_is_alphabetical_and_says_it_is_waiting_for_the_location()
        {
            yield return Boot(withLocation: false);
            _manager.Show(ScreenId.Vets);
            yield return Frames(6);
            var rows = Find("Vets/Viewport/Content/List").GetComponentsInChildren<VetRowView>();
            Assert.AreEqual(3, rows.Length);
            Assert.IsTrue(Active("Vets/Viewport/Content/LocationNote"));
            Assert.IsFalse(rows[0].transform.Find("Face/Top/Distance").gameObject.activeSelf, "no made-up distances");
        }

        [UnityTest]
        public IEnumerator The_alert_is_live_screen_links_to_the_vet_list()
        {
            yield return Boot();
            _manager.Show(ScreenId.SosCreate);
            yield return Frames(4);
            Assert.IsTrue(Find("SosCreate/Viewport/Content/Sent/FindVetButton") != null);
            _manager.Show(ScreenId.SosCreate);
            Click("SosCreate/Viewport/Content/Sent/FindVetButton");
            yield return Frames(4);
            Assert.AreEqual(ScreenId.Vets, _manager.Current);
        }

        sealed class FailingVets : IVetRepository
        {
            public bool IsDemo => false;
            public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<VetClinic>> GetAllAsync() =>
                throw new RepositoryException("The vet directory is not reachable.");
        }
    }
}
