using System.Collections;
using System.Linq;
using LostPetNetwork.AR;
using LostPetNetwork.Services.Adoption;
using LostPetNetwork.Services.Reports;
using LostPetNetwork.Services.Sensors;
using LostPetNetwork.Services.Sos;
using LostPetNetwork.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Management;

namespace LostPetNetwork.Tests.PlayMode
{
    /// <summary>
    /// Runs the real AR scenes on AR Foundation's XR Simulation (the Editor has no AR camera). This proves the
    /// scenes load, start and stop the AR session, place and measure the pet, and put pins in the right places.
    /// Real tracking, GPS and compass quality can only be judged on a phone.
    /// </summary>
    public class ArSimulationTests
    {
        IEnumerator Boot()
        {
            SensorHub.Reset();
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            float waited = 0f;
            while (!(ArLauncher.Service != null && ArLauncher.Service.IsSupported) && waited < 20f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!(ArLauncher.Service != null && ArLauncher.Service.IsSupported)) Assert.Ignore("XR Simulation is not available here.");
        }

        static IEnumerator WaitFor(System.Func<bool> condition, float timeout = 15f)
        {
            float t = 0f;
            while (!condition() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(condition(), "timed out waiting for the AR scene");
        }

        [TearDown]
        public void TearDown()
        {
            if (ArService.IsOpen)
            {
                ArService.Close(ArService.PlaceScene);
                ArService.Close(ArService.CompassScene);
            }
            SensorHub.Reset();
            ScreenManager.InputBlocked = false;
        }

        [UnityTest]
        public IEnumerator Try_at_home_opens_places_a_life_size_pet_and_hands_the_app_back()
        {
            yield return Boot();
            var listing = new MockAdoptionRepository(latencyMs: 0).GetAvailableAsync().Result.First(l => l.Name == "Rocky");
            ArLauncher.OpenTryAtHome(listing);
            Assert.IsTrue(ArService.IsOpen);
            Assert.IsTrue(ScreenManager.InputBlocked, "the app's Back button and shake belong to AR meanwhile");

            yield return WaitFor(() => SceneManager.GetSceneByName(ArService.PlaceScene).isLoaded);
            yield return null;
            yield return null;
            Assert.IsNotNull(XRGeneralSettings.Instance.Manager.activeLoader, "the AR session is running");
            var controller = Object.FindAnyObjectByType<ArPlacementController>();
            Assert.IsNotNull(controller);
            Assert.IsFalse(controller.HasPlaced);
            Assert.IsFalse(GameObject.Find("Canvas") != null && GameObject.Find("Canvas").GetComponent<Canvas>().enabled && SceneManager.GetSceneByName("Main").GetRootGameObjects().Any(g => g.name == "Canvas" && g.GetComponent<Canvas>().enabled),
                "the main UI is hidden while AR is open");

            var cam = Camera.main;
            controller.Place(cam.transform.position + cam.transform.forward * 1.8f + Vector3.down * 0.8f);
            yield return null;
            Assert.IsTrue(controller.HasPlaced);
            string info = GameObject.Find("InfoPanel").GetComponentInChildren<TMPro.TMP_Text>().text;
            StringAssert.Contains("cm tall", info);
            StringAssert.Contains($"{AdoptionCompatibility.RecommendedSpaceM2(listing)} m²", info);

            var placed = GameObject.Find("PlacedPet");
            Assert.IsNotNull(placed);
            var renderers = placed.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            Assert.That(bounds.size.y, Is.InRange(0.5f, 1.2f), "a large dog stands roughly 50 cm to 1.2 m tall");

            ArService.Close(ArService.PlaceScene);
            yield return WaitFor(() => !ArService.IsOpen);
            yield return null;
            Assert.IsFalse(ScreenManager.InputBlocked);
            Assert.IsFalse(SceneManager.GetSceneByName(ArService.PlaceScene).isLoaded);
            Assert.IsNull(XRGeneralSettings.Instance.Manager.activeLoader, "the AR session is stopped again");
            var mainCanvas = SceneManager.GetSceneByName("Main").GetRootGameObjects().First(g => g.name == "Canvas");
            Assert.IsTrue(mainCanvas.GetComponent<Canvas>().enabled, "the app's screens are back");
        }

        [UnityTest]
        public IEnumerator Radar_puts_a_pin_for_each_report_and_tapping_one_returns_it()
        {
            yield return Boot();
            var fakeLoc = new FakeLocationProvider();
            SensorHub.Location = fakeLoc;
            SensorHub.Heading = new FakeHeadingProvider { HeadingDegrees = 341f };
            fakeLoc.Push(new GeoFix(36.8065, 10.1815));

            var reports = new MockReportRepository(latencyMs: 0).GetAllAsync().Result;
            PetReport returned = null;
            bool closed = false;
            ArLauncher.OpenRadar(reports, r => { returned = r; closed = true; });
            yield return WaitFor(() => SceneManager.GetSceneByName(ArService.CompassScene).isLoaded);
            yield return WaitFor(() => Object.FindObjectsByType<ArMarkerView>().Length == reports.Count);
            yield return null;
            yield return null;

            var markers = Object.FindObjectsByType<ArMarkerView>();
            Assert.AreEqual(3, markers.Length);
            var strip = markers[0].Rect.parent as RectTransform;
            foreach (var m in markers)
            {
                Assert.IsTrue(m.gameObject.activeSelf);
                Assert.LessOrEqual(Mathf.Abs(m.Rect.anchoredPosition.x), strip.rect.width / 2f, "pins stay on screen");
                StringAssert.Contains("m", m.transform.Find("Distance").GetComponent<TMPro.TMP_Text>().text);
            }

            // Facing north-north-west, Biscuit (about 1.8 km that way) is straight ahead; Luna (north-east) is off to the side.
            var biscuit = markers.First(m => ((PetReport)m.Tag).PetName == "Biscuit");
            Assert.Less(Mathf.Abs(biscuit.Rect.anchoredPosition.x), 80f, "the pin in front is near the middle of the screen");
            var luna = markers.First(m => ((PetReport)m.Tag).PetName == "Luna");
            Assert.Greater(Mathf.Abs(luna.Rect.anchoredPosition.x), 100f);

            biscuit.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return WaitFor(() => closed);
            Assert.IsNotNull(returned);
            Assert.AreEqual("Biscuit", returned.PetName, "the tapped report is handed back so the app can open it");
            Assert.IsFalse(ArService.IsOpen);
        }

        [UnityTest]
        public IEnumerator Guide_arrow_points_toward_the_alert_and_shows_the_distance()
        {
            yield return Boot();
            var fakeLoc = new FakeLocationProvider();
            SensorHub.Location = fakeLoc;
            SensorHub.Heading = new FakeHeadingProvider { HeadingDegrees = 90f }; // facing east
            fakeLoc.Push(new GeoFix(36.8000, 10.1800));

            // The alert is due north (about 1.1 km), so it is 90° to the left of where the phone points.
            var alert = new SosAlert { Id = "a", ReporterName = "Ines", Latitude = 36.8100, Longitude = 10.1800, Status = SosStatus.Open };
            ArLauncher.OpenSosGuide(alert);
            yield return WaitFor(() => SceneManager.GetSceneByName(ArService.CompassScene).isLoaded);
            yield return WaitFor(() => Object.FindAnyObjectByType<ArCompassController>() != null);
            for (int i = 0; i < 20; i++) yield return null; // let the compass smoothing settle

            var arrow = GameObject.Find("Arrow").transform;
            float z = arrow.localEulerAngles.z;
            Assert.AreEqual(90f, z, 5f, "the arrow turns left (counter-clockwise) toward the target");
            string distance = GameObject.Find("DistancePanel").GetComponentInChildren<TMPro.TMP_Text>().text;
            StringAssert.Contains("km away", distance);
            Assert.AreEqual(1, Object.FindObjectsByType<ArMarkerView>().Length);
        }

        [UnityTest]
        public IEnumerator Without_gps_or_compass_the_radar_says_what_is_missing_instead_of_guessing()
        {
            yield return Boot();
            ArLauncher.OpenRadar(new MockReportRepository(latencyMs: 0).GetAllAsync().Result, null);
            yield return WaitFor(() => SceneManager.GetSceneByName(ArService.CompassScene).isLoaded);
            for (int i = 0; i < 5; i++) yield return null;
            string status = GameObject.Find("StatusPanel").GetComponentInChildren<TMPro.TMP_Text>().text;
            StringAssert.Contains("GPS", status);
            foreach (var m in Object.FindObjectsByType<ArMarkerView>(FindObjectsInactive.Include))
                Assert.IsFalse(m.gameObject.activeSelf, "no pins are drawn without a position");
        }
    }
}
