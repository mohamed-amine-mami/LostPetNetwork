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
        public IEnumerator Placed_model_can_be_resized_moved_and_turned_and_real_size_restores_it()
        {
            yield return Boot();
            var listing = new MockAdoptionRepository(latencyMs: 0).GetAvailableAsync().Result.First(l => l.Name == "Rocky");
            ArLauncher.OpenTryAtHome(listing);
            yield return WaitFor(() => SceneManager.GetSceneByName(ArService.PlaceScene).isLoaded);
            yield return null;
            yield return null;
            var controller = Object.FindAnyObjectByType<ArPlacementController>();
            Assert.IsFalse(controller.HasPlaced);
            Assert.IsFalse(GameObject.Find("PlaceButton") != null && GameObject.Find("PlaceButton").activeInHierarchy, "no surface found yet, so no Place here button");
            Assert.IsFalse(GameObject.Find("Controls") != null && GameObject.Find("Controls").activeInHierarchy, "resize and move controls wait until it is placed");

            var cam = Camera.main;
            var spot = cam.transform.position + cam.transform.forward * 1.8f + Vector3.down * 0.8f;
            controller.Place(spot);
            yield return null;
            yield return null;
            var info = GameObject.Find("InfoPanel").GetComponentInChildren<TMPro.TMP_Text>();
            StringAssert.DoesNotContain("resized", info.text);
            var placed = GameObject.Find("PlacedPet");
            float realScale = placed.transform.localScale.x;
            Assert.IsTrue(GameObject.Find("Controls").activeInHierarchy, "the move and resize controls appear after placing");
            Assert.IsFalse(GameObject.Find("RealSizeButton") != null && GameObject.Find("RealSizeButton").activeInHierarchy, "nothing to restore at real size");

            controller.SetUserScale(1.5f);
            yield return null;
            Assert.AreEqual(realScale * 1.5f, placed.transform.localScale.x, 1e-4f);
            StringAssert.Contains("resized to 150%", info.text);
            Assert.IsTrue(GameObject.Find("RealSizeButton").activeInHierarchy, "Real size shows once it is resized");

            controller.SetUserScale(10f);
            Assert.AreEqual(2f, controller.UserScale, "capped at double size");
            controller.SetUserScale(0f);
            Assert.AreEqual(0.5f, controller.UserScale, "capped at half size");

            var moved = spot + new Vector3(0.4f, 0f, -0.3f);
            controller.MoveTo(moved);
            controller.SetYaw(120f);
            Assert.AreEqual(moved, placed.transform.position);
            Assert.AreEqual(120f, controller.ContentYaw, 0.01f);
            var ring = GameObject.Find("SpaceRing");
            if (ring != null) Assert.AreEqual(moved.x, ring.transform.position.x, 1e-4f, "the space ring moves with the pet");

            GameObject.Find("RealSizeButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            Assert.AreEqual(1f, controller.UserScale);
            Assert.AreEqual(realScale, placed.transform.localScale.x, 1e-4f);
            StringAssert.DoesNotContain("resized", info.text);

            Assert.IsTrue(controller.MoveMode);
            GameObject.Find("ModeButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.IsFalse(controller.MoveMode);
            StringAssert.Contains("Turn", GameObject.Find("ModeButton").GetComponentInChildren<TMPro.TMP_Text>().text);

            GameObject.Find("ResetButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            Assert.IsFalse(controller.HasPlaced);
            Assert.AreEqual(1f, controller.UserScale);

            ArService.Close(ArService.PlaceScene);
            yield return WaitFor(() => !ArService.IsOpen);
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
        public IEnumerator Radar_stands_a_3D_pet_in_front_of_you_only_for_a_nearby_report_with_a_pet_look()
        {
            yield return Boot();
            var fakeLoc = new FakeLocationProvider();
            SensorHub.Location = fakeLoc;
            SensorHub.Heading = new FakeHeadingProvider { HeadingDegrees = 0f }; // facing north
            fakeLoc.Push(new GeoFix(36.8000, 10.1800));

            string look = "{\"v\":1,\"species\":0,\"coat\":1,\"pattern\":0,\"size\":1,\"ears\":0,\"collar\":1}";
            var near = new PetReport { Id = "near", PetName = "Nearby", Latitude = 36.80009, Longitude = 10.1800, PetAvatarJson = look }; // about 10 m north
            var far = new PetReport { Id = "far", PetName = "Far", Latitude = 36.8100, Longitude = 10.1800, PetAvatarJson = look };      // about 1.1 km
            var noLook = new PetReport { Id = "nolook", PetName = "Bird", Latitude = 36.80009, Longitude = 10.1801 };

            ArLauncher.OpenRadar(new[] { near, far, noLook }, null);
            yield return WaitFor(() => SceneManager.GetSceneByName(ArService.CompassScene).isLoaded);
            yield return WaitFor(() => Object.FindAnyObjectByType<ArCompassController>() != null);
            var controller = Object.FindAnyObjectByType<ArCompassController>();
            yield return WaitFor(() => controller.ShownPetCount == 1);

            Assert.AreEqual(2, controller.PetCount, "only reports with a pet look get a 3D model");
            Assert.AreEqual(1, controller.ShownPetCount, "only the report within 30 m is shown in 3D");
            var pet = GameObject.Find("Pet Nearby");
            Assert.IsNotNull(pet);
            Assert.IsTrue(pet.activeSelf);
            Assert.IsNull(GameObject.Find("Pet Far"), "the far pet stays hidden (Find only returns active objects)");

            yield return new WaitForSecondsRealtime(2f); // let the pet settle (it eases toward its spot while the simulated camera moves)
            var cam = Camera.allCameras.First(c => c.targetTexture == null && c.gameObject.scene.name == ArService.CompassScene);
            var toPet = pet.transform.position - cam.transform.position;
            toPet.y = 0f;
            Assert.Greater(Vector3.Dot(toPet.normalized, Quaternion.Euler(0f, ArMath.CameraYaw(cam.transform), 0f) * Vector3.forward), 0.8f,
                "facing north, a pet to the north stands in front of the camera");
            Assert.AreEqual(8f, toPet.magnitude, 1.5f, "10 m away is drawn 8 m ahead, the farthest comfortable distance");
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
            StringAssert.Contains("km", distance);
            StringAssert.Contains("on foot", GameObject.Find("WalkPanel").GetComponentInChildren<TMPro.TMP_Text>().text);
            Assert.AreEqual("Turn left", GameObject.Find("InstructionPanel").GetComponentInChildren<TMPro.TMP_Text>().text, "the alert is 90° to the left");
            Assert.AreEqual(90f, GameObject.Find("Dial").transform.localEulerAngles.z, 5f, "facing east, north is to the left on the compass ring");
            var letterN = GameObject.Find("LetterN").transform;
            Assert.AreEqual(0f, Mathf.DeltaAngle(0f, letterN.localEulerAngles.z + GameObject.Find("Dial").transform.localEulerAngles.z), 5f, "the letters stay upright");
            Assert.AreEqual(1, Object.FindObjectsByType<ArMarkerView>().Length);

            // Turn toward the alert: the words change and the arrow turns green.
            ((FakeHeadingProvider)SensorHub.Heading).HeadingDegrees = 0f; // facing north, the alert is straight ahead
            var words = GameObject.Find("InstructionPanel").GetComponentInChildren<TMPro.TMP_Text>();
            yield return WaitFor(() => words.text == "Straight ahead");
            float settle = 0f;
            while (settle < 1.2f) { settle += Time.unscaledDeltaTime; yield return null; } // the colour eases in as well
            var disc = GameObject.Find("Arrow").transform.Find("Disc").GetComponent<UnityEngine.UI.Image>().color;
            Assert.Greater(disc.g, disc.r + 0.25f, "green when you face the target");
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
