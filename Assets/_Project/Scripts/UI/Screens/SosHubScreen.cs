using System;
using System.Collections.Generic;
using System.Linq;
using LostPetNetwork.Services.Reports;
using LostPetNetwork.Services.Sensors;
using LostPetNetwork.Services.Sos;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>Rescue hub: send an SOS, or see open alerts (nearest first) and help.</summary>
    public class SosHubScreen : MonoBehaviour
    {
        const float RefreshSeconds = 10f;

        [SerializeField] ScreenManager manager;
        [SerializeField] Button backButton;
        [SerializeField] Button sendButton;
        [SerializeField] TMP_Text countLabel;
        [SerializeField] Transform listRoot;
        [SerializeField] SosRowView rowPrefab;
        [SerializeField] GameObject loading;
        [SerializeField] GameObject emptyState;
        [SerializeField] MessageBanner banner;

        List<SosAlert> _alerts = new List<SosAlert>();
        bool _visible;
        int _pollId;

        void Awake()
        {
            backButton.onClick.AddListener(() => { if (!manager.Back()) manager.Show(ScreenId.Home); });
            sendButton.onClick.AddListener(() => manager.Push(ScreenId.SosCreate));
        }

        void OnEnable()
        {
            _visible = true;
            banner.Hide();
            SensorHub.Location.StartTracking();
            string flash = AppSession.ConsumeFlash();
            if (flash != null) banner.ShowInfo(flash);
            Poll(++_pollId);
        }

        void OnDisable()
        {
            _visible = false;
            SensorHub.Location.StopTracking();
        }

        /// <summary>Loads alerts now and again every few seconds while this screen is open.</summary>
        async void Poll(int id)
        {
            bool first = true;
            while (_visible && this != null && id == _pollId)
            {
                if (first) loading.SetActive(true);
                try
                {
                    _alerts = (await SosServices.Repository.GetActiveAsync()).ToList();
                    if (this == null || !_visible || id != _pollId) return;
                    Render();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    if (this != null) banner.ShowError(UiUtil.Describe(e, "Couldn't load alerts. Retrying…"));
                }
                finally
                {
                    if (this != null) { loading.SetActive(false); first = false; }
                }
                await Awaitable.WaitForSecondsAsync(RefreshSeconds);
            }
        }

        /// <summary>Open alerts first, then in-progress ones; nearest first when the position is known.</summary>
        public static List<SosAlert> Arrange(IEnumerable<SosAlert> alerts, GeoFix? me) =>
            alerts.OrderBy(a => a.Status == SosStatus.Open ? 0 : 1)
                  .ThenBy(a => me.HasValue ? GeoMath.DistanceMeters(me.Value.Latitude, me.Value.Longitude, a.Latitude, a.Longitude) : 0.0)
                  .ThenByDescending(a => a.CreatedAtUtc)
                  .ToList();

        public static string DistanceLabel(SosAlert alert, GeoFix? me)
        {
            if (!me.HasValue) return null;
            double km = GeoMath.DistanceMeters(me.Value.Latitude, me.Value.Longitude, alert.Latitude, alert.Longitude) / 1000.0;
            return km < 1 ? $"{(int)(km * 100) * 10} m away" : $"{km:0.#} km away";
        }

        void Render()
        {
            UiUtil.ClearChildren(listRoot);
            var me = SensorHub.Location.Latest;
            var shown = Arrange(_alerts, me);
            foreach (var alert in shown)
                Instantiate(rowPrefab, listRoot).Bind(alert, DistanceLabel(alert, me), Open);
            string noun = shown.Count == 1 ? "alert" : "alerts";
            countLabel.text = SosServices.Repository.IsDemo ? $"{shown.Count} open {noun} · Demo data" : $"{shown.Count} open {noun}";
            emptyState.SetActive(shown.Count == 0);
        }

        void Open(SosAlert alert)
        {
            AppSession.SelectedAlert = alert;
            manager.Push(ScreenId.SosDetails);
        }
    }
}
