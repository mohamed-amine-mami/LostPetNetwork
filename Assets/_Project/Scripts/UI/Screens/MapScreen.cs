using System;
using System.Collections.Generic;
using System.Linq;
using LostPetNetwork.Services.Reports;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>
    /// Map preview. This is a STYLISED map (no map SDK or GPS yet): pins are placed from sample coordinates of
    /// the demo reports. It demonstrates the designed interaction, not real geography.
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

        List<PetReport> _all = new List<PetReport>();
        PetReport _selected;

        void Awake()
        {
            closeButton.onClick.AddListener(() => { if (!manager.Back()) manager.Show(ScreenId.Explore); });
            search.onValueChanged.AddListener(_ => Render());
            chips.Changed += _ => Render();
        }

        async void OnEnable()
        {
            banner.Hide();
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
                if (this != null) banner.ShowError("Couldn't load reports for the map.");
            }
            finally
            {
                if (this != null) loading.SetActive(false);
            }
        }

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

            foreach (var report in filtered.Where(r => r != _selected))
                Instantiate(pinPrefab, pinLayer).Bind(report, false, Select);
            if (_selected != null)
                Instantiate(pinPrefab, pinLayer).Bind(_selected, true, Select); // drawn last = on top

            emptyState.SetActive(filtered.Count == 0);
            selectedCard.gameObject.SetActive(_selected != null);
            if (_selected != null)
                selectedCard.Bind(_selected, OpenDetails,
                    $"{_selected.Summary} · {_selected.DistanceKm:0.#} km away · {_selected.TimeAgo(DateTime.UtcNow)}");
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
