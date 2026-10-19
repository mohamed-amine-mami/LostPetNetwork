using System;
using System.Linq;
using LostPetNetwork.Services.Auth;
using LostPetNetwork.Services.Profile;
using LostPetNetwork.Services.Reports;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>Home: greeting (real profile), report shortcuts and a preview of nearby reports (demo data).</summary>
    public class HomeScreen : MonoBehaviour
    {
        [SerializeField] ScreenManager manager;
        [SerializeField] TMP_Text greeting;
        [SerializeField] Button reportLostButton;
        [SerializeField] Button reportFoundButton;
        [SerializeField] Button seeAllButton;
        [SerializeField] Transform listRoot;
        [SerializeField] PetCardView cardPrefab;
        [SerializeField] GameObject loading;
        [SerializeField] GameObject emptyState;
        [SerializeField] MessageBanner banner;

        void Awake()
        {
            reportLostButton.onClick.AddListener(() => StartReport(ReportType.Lost));
            reportFoundButton.onClick.AddListener(() => StartReport(ReportType.Found));
            seeAllButton.onClick.AddListener(() => manager.Show(ScreenId.Explore));
        }

        void StartReport(ReportType type)
        {
            AppSession.StartDraft(type);
            manager.Push(ScreenId.Report);
        }

        async void OnEnable()
        {
            banner.Hide();
            greeting.text = GreetingFor(ProfileService.Instance.Cached?.display_name);
            LoadReports();

            if (ProfileService.Instance.Cached != null) return;
            try
            {
                var result = await ProfileService.Instance.GetMyProfileAsync();
                if (this == null) return;
                if (result.Ok) greeting.text = GreetingFor(result.Profile.display_name);
                else banner.ShowError(result.Error);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this != null) banner.ShowError("Couldn't load your profile.");
            }
        }

        public static string GreetingFor(string displayName)
        {
            int hour = DateTime.Now.Hour;
            string part = hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening";
            if (string.IsNullOrWhiteSpace(displayName)) return part;
            string first = displayName.Trim().Split(' ')[0];
            return $"{part}, {first}";
        }

        async void LoadReports()
        {
            loading.SetActive(true);
            emptyState.SetActive(false);
            UiUtil.ClearChildren(listRoot);
            try
            {
                var reports = (await ReportServices.Repository.GetAllAsync()).Take(2).ToList();
                if (this == null) return;
                foreach (var report in reports)
                {
                    var card = Instantiate(cardPrefab, listRoot);
                    card.Bind(report, OpenDetails);
                }
                emptyState.SetActive(reports.Count == 0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this != null) banner.ShowError("Couldn't load nearby reports.");
            }
            finally
            {
                if (this != null) loading.SetActive(false);
            }
        }

        void OpenDetails(PetReport report)
        {
            AppSession.SelectedReport = report;
            manager.Push(ScreenId.Details);
        }
    }
}
