using System;
using System.Collections.Generic;
using System.Linq;
using LostPetNetwork.Services.Reports;
using LostPetNetwork.Services.Sensors;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>Explore: browse and filter lost/found reports (demo data until the backend exists).</summary>
    public class ExploreScreen : MonoBehaviour
    {
        [SerializeField] ScreenManager manager;
        [SerializeField] Button mapButton;
        [SerializeField] Button radarButton;
        [SerializeField] TMP_InputField search;
        [SerializeField] ChipGroup chips;
        [SerializeField] ChipGroup sortChips;
        [SerializeField] TMP_Text countLabel;
        [SerializeField] Transform listRoot;
        [SerializeField] PetCardView cardPrefab;
        [SerializeField] GameObject loading;
        [SerializeField] GameObject emptyState;
        [SerializeField] MessageBanner banner;

        List<PetReport> _all = new List<PetReport>();

        void Awake()
        {
            mapButton.onClick.AddListener(() => manager.Push(ScreenId.Map));
            radarButton.onClick.AddListener(OpenRadar);
            search.onValueChanged.AddListener(_ => Render());
            chips.Changed += _ => Render();
            sortChips.Changed += _ => Render();
        }

        async void OnEnable()
        {
            banner.Hide();
            loading.SetActive(true);
            emptyState.SetActive(false);
            radarButton.gameObject.SetActive(ArLauncher.CanShowRadar);
            SensorHub.Location.StartTracking();
            SensorHub.Location.Updated += OnLocation;
            try
            {
                _all = (await ReportServices.Repository.GetAllAsync()).ToList();
                if (this == null) return;
                Render();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this != null) banner.ShowError(UiUtil.Describe(e, "Couldn't load reports. Please try again."));
            }
            finally
            {
                if (this != null) loading.SetActive(false);
            }
        }

        void OnDisable()
        {
            SensorHub.Location.Updated -= OnLocation;
            SensorHub.Location.StopTracking();
        }

        void OnLocation(GeoFix fix)
        {
            if (sortChips.SelectedIndex > 0 && _all.Count > 0) Render();
        }

        /// <summary>Sort chip order: Newest, Nearest, Within 5 km.</summary>
        public static List<PetReport> Arrange(IEnumerable<PetReport> filtered, int sortIndex, GeoFix? user) =>
            ReportSearch.Order(filtered, sortIndex == 0 ? ReportSort.Newest : ReportSort.Nearest, user, sortIndex == 2 ? 5.0 : (double?)null);

        /// <summary>Pure filtering logic, exposed for tests. Chip order: All, Lost, Found, Dogs, Cats.</summary>
        public static List<PetReport> Filter(IEnumerable<PetReport> reports, int chipIndex, string query)
        {
            string q = query?.Trim().ToLowerInvariant() ?? string.Empty;
            return reports.Where(r =>
            {
                switch (chipIndex)
                {
                    case 1: if (r.Type != ReportType.Lost) return false; break;
                    case 2: if (r.Type != ReportType.Found) return false; break;
                    case 3: if (r.Species != PetSpecies.Dog) return false; break;
                    case 4: if (r.Species != PetSpecies.Cat) return false; break;
                }
                if (q.Length == 0) return true;
                string hay = string.Join(" ", r.PetName, r.Breed, r.LocationText, r.Description, r.Species.ToString()).ToLowerInvariant();
                return hay.Contains(q);
            }).ToList();
        }

        void Render()
        {
            UiUtil.ClearChildren(listRoot);
            var me = SensorHub.Location.Latest;
            var filtered = Arrange(Filter(_all, chips.SelectedIndex, search.text), sortChips.SelectedIndex, me);
            foreach (var report in filtered)
                Instantiate(cardPrefab, listRoot).Bind(report, OpenDetails, ReportSearch.DistanceLabel(report, me));

            if (sortChips.SelectedIndex > 0 && !me.HasValue && !ReportServices.Repository.IsDemo)
                banner.ShowInfo("Turn on location to sort by distance.");
            else banner.Hide();

            string noun = filtered.Count == 1 ? "report" : "reports";
            countLabel.text = ReportServices.Repository.IsDemo ? $"{filtered.Count} {noun} · Demo data" : $"{filtered.Count} {noun}";
            emptyState.SetActive(filtered.Count == 0);
        }

        /// <summary>Shows reports around you as floating pins on the camera view; tapping one opens it afterwards.</summary>
        void OpenRadar()
        {
            var nearby = _all.Where(r => r.HasLocation && r.Status == ReportStatus.Active).ToList();
            ArLauncher.OpenRadar(nearby, report =>
            {
                if (report != null && this != null) OpenDetails(report);
            });
        }

        void OpenDetails(PetReport report)
        {
            AppSession.SelectedReport = report;
            manager.Push(ScreenId.Details);
        }
    }
}
