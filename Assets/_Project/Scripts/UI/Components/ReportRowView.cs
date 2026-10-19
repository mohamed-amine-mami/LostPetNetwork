using System;
using LostPetNetwork.Services.Reports;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>Compact list row (Figma "Report Row") used by My reports and the Map preview card.</summary>
    [RequireComponent(typeof(Button))]
    public class ReportRowView : MonoBehaviour
    {
        [SerializeField] CoverImage thumb;
        [SerializeField] StatusBadge badge;
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text metaLabel;

        public PetReport Report { get; private set; }

        public void Bind(PetReport report, Action<PetReport> onClick, string metaOverride = null)
        {
            Report = report;
            thumb.Set(PhotoLibrary.ForReport(report));
            badge.Set(report);
            nameLabel.text = report.DisplayName;
            if (metaOverride != null) metaLabel.text = metaOverride;
            else
            {
                string requests = report.ContactRequests == 1 ? "1 contact request" : $"{report.ContactRequests} contact requests";
                metaLabel.text = $"Posted {report.TimeAgo(DateTime.UtcNow)} · {requests}";
            }
            var button = GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke(report));
        }
    }
}
