using System;
using LostPetNetwork.Services.Reports;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>Feed card (Figma "Pet Card"). Instantiated from a prefab for each report.</summary>
    [RequireComponent(typeof(Button))]
    public class PetCardView : MonoBehaviour
    {
        [SerializeField] CoverImage photo;
        [SerializeField] StatusBadge badge;
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text detailsLabel;
        [SerializeField] TMP_Text locationLabel;
        [SerializeField] GameObject demoTag;

        public PetReport Report { get; private set; }

        public void Bind(PetReport report, Action<PetReport> onClick)
        {
            Report = report;
            photo.Set(PhotoLibrary.ForReport(report));
            badge.Set(report);
            nameLabel.text = report.DisplayName;
            detailsLabel.text = string.IsNullOrWhiteSpace(report.Description) ? report.Summary : report.Summary + " · " + Shorten(report.Description, 48);
            locationLabel.text = $"{report.LocationText} · {report.TimeAgo(DateTime.UtcNow)}";
            if (demoTag != null) demoTag.SetActive(report.IsDemo);
            var button = GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke(report));
        }

        static string Shorten(string text, int max)
        {
            text = text.Trim();
            return text.Length <= max ? text : text.Substring(0, max - 1).TrimEnd() + "…";
        }
    }
}
