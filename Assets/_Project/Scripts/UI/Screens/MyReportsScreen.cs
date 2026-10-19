using System;
using System.Collections.Generic;
using System.Linq;
using LostPetNetwork.Services.Auth;
using LostPetNetwork.Services.Reports;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>The signed-in user's reports and their statuses (demo repository, kept in memory).</summary>
    public class MyReportsScreen : MonoBehaviour
    {
        [SerializeField] ScreenManager manager;
        [SerializeField] ChipGroup chips;
        [SerializeField] Transform listRoot;
        [SerializeField] ReportRowView rowPrefab;
        [SerializeField] GameObject loading;
        [SerializeField] GameObject emptyState;
        [SerializeField] TMP_Text emptyLabel;
        [SerializeField] Button emptyAction;
        [SerializeField] TMP_Text demoNote;
        [SerializeField] MessageBanner banner;

        List<PetReport> _mine = new List<PetReport>();

        void Awake()
        {
            chips.Changed += _ => Render();
            emptyAction.onClick.AddListener(() =>
            {
                AppSession.StartDraft(ReportType.Lost);
                manager.Push(ScreenId.Report);
            });
        }

        async void OnEnable()
        {
            banner.Hide();
            string flash = AppSession.ConsumeFlash();
            if (!string.IsNullOrEmpty(flash)) banner.ShowInfo(flash);

            loading.SetActive(true);
            emptyState.SetActive(false);
            try
            {
                _mine = (await ReportServices.Repository.GetMineAsync(AuthService.Instance.UserId)).ToList();
                if (this == null) return;
                Render();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this != null) banner.ShowError("Couldn't load your reports.");
            }
            finally
            {
                if (this != null) loading.SetActive(false);
            }
        }

        /// <summary>Chip order: Active, Reunited, All.</summary>
        public static List<PetReport> Filter(IEnumerable<PetReport> reports, int chipIndex)
        {
            return reports.Where(r =>
                chipIndex == 2 ||
                (chipIndex == 0 && r.Status == ReportStatus.Active) ||
                (chipIndex == 1 && r.Status == ReportStatus.Reunited)).ToList();
        }

        void Render()
        {
            UiUtil.ClearChildren(listRoot);
            var shown = Filter(_mine, chips.SelectedIndex);
            foreach (var report in shown)
                Instantiate(rowPrefab, listRoot).Bind(report, OpenDetails);

            emptyState.SetActive(shown.Count == 0);
            if (shown.Count == 0)
            {
                bool anyReports = _mine.Count > 0;
                emptyLabel.text = anyReports
                    ? "Nothing here yet in this filter."
                    : "You haven't reported a pet yet. If a pet is missing or you found one, report it in two minutes.";
                emptyAction.gameObject.SetActive(!anyReports);
            }
            demoNote.gameObject.SetActive(_mine.Count > 0);
        }

        void OpenDetails(PetReport report)
        {
            AppSession.SelectedReport = report;
            manager.Push(ScreenId.Details);
        }
    }
}
