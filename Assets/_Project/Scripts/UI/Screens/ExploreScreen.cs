using System;
using System.Collections.Generic;
using System.Linq;
using LostPetNetwork.Services.Reports;
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
        [SerializeField] TMP_InputField search;
        [SerializeField] ChipGroup chips;
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
            search.onValueChanged.AddListener(_ => Render());
            chips.Changed += _ => Render();
        }

        async void OnEnable()
        {
            banner.Hide();
            loading.SetActive(true);
            emptyState.SetActive(false);
            try
            {
                _all = (await ReportServices.Repository.GetAllAsync()).ToList();
                if (this == null) return;
                Render();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this != null) banner.ShowError("Couldn't load reports. Pull down to try again.");
            }
            finally
            {
                if (this != null) loading.SetActive(false);
            }
        }

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
            var filtered = Filter(_all, chips.SelectedIndex, search.text);
            foreach (var report in filtered)
                Instantiate(cardPrefab, listRoot).Bind(report, OpenDetails);

            string noun = filtered.Count == 1 ? "report" : "reports";
            countLabel.text = $"{filtered.Count} {noun} · Demo data";
            emptyState.SetActive(filtered.Count == 0);
        }

        void OpenDetails(PetReport report)
        {
            AppSession.SelectedReport = report;
            manager.Push(ScreenId.Details);
        }
    }
}
