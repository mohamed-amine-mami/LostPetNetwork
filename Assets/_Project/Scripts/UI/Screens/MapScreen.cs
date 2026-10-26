using System;
using System.Collections.Generic;
using System.Linq;
using LostPetNetwork.Services.Map;
using LostPetNetwork.Services.Reports;
using LostPetNetwork.Services.Sensors;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>
    /// Map of lost and found reports. With a Mapbox token it is a real street map with photo pins at each
    /// report's coordinates; without one it falls back to a stylised preview. Report data is still demo data.
    /// </summary>
    public class MapScreen : MonoBehaviour
    {
        [SerializeField] ScreenManager manager;
        [SerializeField] Button closeButton;
        [SerializeField] TMP_InputField search;
        [SerializeField] ChipGroup chips;
        [SerializeField] RectTransform pinLayer;
        [SerializeField] MapPinView pinPrefab;
        [SerializeField] ReportRowView selectedCard;
        [SerializeField] GameObject emptyState;
        [SerializeField] GameObject loading;
        [SerializeField] MessageBanner banner;
        [SerializeField] MapView mapView;
        [SerializeField] GameObject liveMapRoot;
        [SerializeField] GameObject liveControls;
        [SerializeField] GameObject stylisedArt;
        [SerializeField] TMP_Text noteLabel;
        [SerializeField] Button zoomInButton;
        [SerializeField] Button zoomOutButton;
        [SerializeField] Button locateButton;
        [SerializeField] RectTransform youMarker;

        List<PetReport> _all = new List<PetReport>();
        PetReport _selected;
        bool _fitted;

        public bool IsLive => MapConfig.IsConfigured;

        void Awake()
        {
            closeButton.onClick.AddListener(() => { if (!manager.Back()) manager.Show(ScreenId.Explore); });
            search.onValueChanged.AddListener(_ => Render());
            chips.Changed += _ => Render();
            zoomInButton.onClick.AddListener(() => mapView.ZoomBy(1));
            zoomOutButton.onClick.AddListener(() => mapView.ZoomBy(-1));
            locateButton.onClick.AddListener(CenterOnMe);
            mapView.ViewChanged += RepositionPins;
        }

        async void OnEnable()
        {
            banner.Hide();
            bool live = IsLive;
            liveMapRoot.SetActive(live);
            liveControls.SetActive(live);
            stylisedArt.SetActive(!live);
            noteLabel.text = live ? (ReportServices.Repository.IsDemo ? "Live map · pins are demo reports" : "Live map") : "Stylised map preview · add a Mapbox token for the live map";
            _fitted = false;
            if (live)
            {
                SensorHub.Location.StartTracking();
                SensorHub.Location.Updated += OnLocation;
            }

            loading.SetActive(true);
            try
            {
                _all = (await ReportServices.Repository.GetAllAsync()).ToList();
                if (this == null) return;
                _selected = _all.FirstOrDefault();
                Render();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this != null) banner.ShowError(UiUtil.Describe(e, "Couldn't load reports for the map."));
            }
            finally
            {
                if (this != null) loading.SetActive(false);
            }
        }

        void OnDisable()
        {
            SensorHub.Location.Updated -= OnLocation;
            if (IsLive) SensorHub.Location.StopTracking();
        }

        void OnLocation(GeoFix fix) => RepositionPins();

        /// <summary>Chip order: All, Lost, Found.</summary>
        public static List<PetReport> Filter(IEnumerable<PetReport> reports, int chipIndex, string query)
        {
            string q = query?.Trim().ToLowerInvariant() ?? string.Empty;
            return reports.Where(r =>
            {
                if (chipIndex == 1 && r.Type != ReportType.Lost) return false;
                if (chipIndex == 2 && r.Type != ReportType.Found) return false;
                if (q.Length == 0) return true;
                return string.Join(" ", r.PetName, r.Breed, r.LocationText).ToLowerInvariant().Contains(q);
            }).ToList();
        }

        void Render()
        {
            UiUtil.ClearChildren(pinLayer);
            var filtered = Filter(_all, chips.SelectedIndex, search.text);
            if (_selected == null || !filtered.Contains(_selected)) _selected = filtered.FirstOrDefault();
            bool live = IsLive;

            if (live && !_fitted)
            {
                var points = filtered.Where(r => r.HasLocation).Select(r => (r.Latitude, r.Longitude)).ToList();
                if (SensorHub.Location.Latest.HasValue) points.Add((SensorHub.Location.Latest.Value.Latitude, SensorHub.Location.Latest.Value.Longitude));
                if (points.Count > 0) { mapView.FitTo(points); _fitted = true; }
            }

            foreach (var report in filtered.Where(r => r != _selected)) AddPin(report, false, live);
            if (_selected != null) AddPin(_selected, true, live); // drawn last = on top

            emptyState.SetActive(filtered.Count == 0);
            selectedCard.gameObject.SetActive(_selected != null);
            if (_selected != null)
            {
                float km = _selected.DistanceFromKm(SensorHub.Location.Latest, out bool real);
                selectedCard.Bind(_selected, OpenDetails,
                    $"{_selected.Summary} · {km:0.#} km away{(real ? "" : " (sample)")} · {_selected.TimeAgo(DateTime.UtcNow)}");
            }
            RepositionPins();
        }

        void AddPin(PetReport report, bool selected, bool live)
        {
            var pin = Instantiate(pinPrefab, pinLayer);
            if (!live) { pin.Bind(report, selected, Select); return; }
            if (!report.HasLocation) { Destroy(pin.gameObject); return; }
            pin.BindOnMap(report, selected, Select, mapView.WorldToLocal(report.Latitude, report.Longitude));
        }

        /// <summary>Moves pins and the "you are here" dot after the map was dragged or zoomed.</summary>
        void RepositionPins()
        {
            if (!IsLive) { youMarker.gameObject.SetActive(false); return; }
            foreach (Transform child in pinLayer)
            {
                var pin = child.GetComponent<MapPinView>();
                if (pin != null && pin.Report != null && pin.Report.HasLocation)
                    pin.SetMapPosition(mapView.WorldToLocal(pin.Report.Latitude, pin.Report.Longitude));
            }
            var me = SensorHub.Location.Latest;
            youMarker.gameObject.SetActive(me.HasValue);
            if (me.HasValue) youMarker.anchoredPosition = mapView.WorldToLocal(me.Value.Latitude, me.Value.Longitude);
        }

        void CenterOnMe()
        {
            var me = SensorHub.Location.Latest;
            if (!me.HasValue)
            {
                banner.ShowInfo("Your location isn't available yet. Allow location access and try again.");
                SensorHub.Location.StartTracking();
                return;
            }
            banner.Hide();
            mapView.SetView(me.Value.Latitude, me.Value.Longitude, 15);
        }

        void Select(PetReport report)
        {
            _selected = report;
            Render();
        }

        void OpenDetails(PetReport report)
        {
            AppSession.SelectedReport = report;
            manager.Push(ScreenId.Details);
        }
    }
}
