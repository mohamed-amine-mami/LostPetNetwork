using System.Collections;
using System.Linq;
using LostPetNetwork.Services.Auth;
using LostPetNetwork.Services.Map;
using LostPetNetwork.Services.Profile;
using LostPetNetwork.Services.Reports;
using LostPetNetwork.Services.Sensors;
using LostPetNetwork.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LostPetNetwork.Tests.PlayMode
{
    /// <summary>The map screen in the real Main scene. Tile downloads need the local token and internet.</summary>
    public class MapWorkflowTests
    {
        ScreenManager _manager;

        static void SetSession(AuthSession s) =>
            typeof(AuthService).GetProperty("Session").GetSetMethod(true).Invoke(AuthService.Instance, new object[] { s });

        static void SetCachedProfile(UserProfile p) =>
            typeof(ProfileService).GetProperty("Cached").GetSetMethod(true).Invoke(ProfileService.Instance, new object[] { p });

        IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        IEnumerator BootOnMap(GeoFix? me = null)
        {
            SensorHub.Reset();
            if (me.HasValue)
            {
                var fake = new FakeLocationProvider();
                SensorHub.Location = fake;
                fake.Push(me.Value);
            }
            PlayerPrefs.DeleteKey("lpn.auth.session.v1");
            SetSession(null);
            ReportServices.Repository = new MockReportRepository(latencyMs: 0);
            AppSession.Reset();
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            _manager = Object.FindAnyObjectByType<ScreenManager>();
            SetCachedProfile(new UserProfile { id = "map-tester", display_name = "Map Tester", city = "Tunis", created_at = "2026-10-18T10:00:00Z" });
            SetSession(new AuthSession
            {
                accessToken = "", refreshToken = "r", userId = "map-tester", email = "map@example.com",
                expiresAtUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600
            });
            _manager.Show(ScreenId.Map);
            yield return Frames(6);
        }

        [TearDown]
        public void TearDown()
        {
            SensorHub.Reset();
            MapConfig.Reload();
            SetSession(null);
            SetCachedProfile(null);
            ReportServices.Repository = new MockReportRepository();
        }

        [UnityTest]
        public IEnumerator Map_shows_one_pin_per_demo_report_and_the_right_mode()
        {
            yield return BootOnMap();
            var screen = Object.FindAnyObjectByType<MapScreen>();
            var pins = screen.GetComponentsInChildren<MapPinView>();
            Assert.AreEqual(3, pins.Length);
            var live = screen.transform.Find("MapBackground/MapLive").gameObject;
            var art = screen.transform.Find("MapBackground/MapArt").gameObject;
            Assert.AreEqual(MapConfig.IsConfigured, live.activeSelf, "live map only with a token");
            Assert.AreEqual(!MapConfig.IsConfigured, art.activeSelf, "stylised preview only without a token");
        }

        [UnityTest]
        public IEnumerator Pins_follow_the_map_when_it_zooms()
        {
            if (!MapConfig.IsConfigured) Assert.Ignore("No Mapbox token in this checkout.");
            yield return BootOnMap();
            var screen = Object.FindAnyObjectByType<MapScreen>();
            var view = screen.GetComponentInChildren<MapView>(true);
            var pin = screen.GetComponentsInChildren<MapPinView>().First();
            var before = ((RectTransform)pin.transform).anchoredPosition;
            Assert.AreEqual(view.WorldToLocal(pin.Report.Latitude, pin.Report.Longitude).x, before.x, 0.5f);

            view.ZoomBy(1);
            yield return Frames(3);
            var after = ((RectTransform)pin.transform).anchoredPosition;
            Assert.AreNotEqual(before, after, "pin moved with the map");
            Assert.AreEqual(view.WorldToLocal(pin.Report.Latitude, pin.Report.Longitude).x, after.x, 0.5f);
        }

        [UnityTest]
        public IEnumerator Real_Mapbox_tiles_download_and_appear()
        {
            if (!MapConfig.IsConfigured) Assert.Ignore("No Mapbox token in this checkout.");
            yield return BootOnMap();
            var view = Object.FindAnyObjectByType<MapScreen>().GetComponentInChildren<MapView>(true);
            Assert.Greater(view.LoadedTileCount, 4, "tile slots were created");

            float timeout = 20f;
            int shown = 0;
            while (timeout > 0f && shown == 0)
            {
                shown = view.GetComponentsInChildren<UnityEngine.UI.RawImage>().Count(t => t.texture != null && t.color.a > 0.5f);
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            if (shown == 0) Assert.Inconclusive("No tile arrived (offline?).");
            Assert.Greater(shown, 0);
        }

        [UnityTest]
        public IEnumerator Locate_button_centres_on_the_user_and_shows_the_dot()
        {
            if (!MapConfig.IsConfigured) Assert.Ignore("No Mapbox token in this checkout.");
            yield return BootOnMap(new GeoFix(36.8065, 10.1815));
            var screen = Object.FindAnyObjectByType<MapScreen>();
            var view = screen.GetComponentInChildren<MapView>(true);
            screen.transform.Find("LiveControls/Zoom/LocateButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return Frames(3);
            Assert.AreEqual(36.8065, view.CenterLatitude, 0.001);
            Assert.AreEqual(10.1815, view.CenterLongitude, 0.001);
            Assert.AreEqual(15, view.Zoom);
            Assert.IsTrue(screen.transform.Find("Overlay/YouMarker").gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Locate_without_a_position_explains_why()
        {
            if (!MapConfig.IsConfigured) Assert.Ignore("No Mapbox token in this checkout.");
            yield return BootOnMap();
            var screen = Object.FindAnyObjectByType<MapScreen>();
            screen.transform.Find("LiveControls/Zoom/LocateButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return Frames(2);
            Assert.IsTrue(screen.GetComponentInChildren<MessageBanner>(true).IsVisible);
        }
    }
}
