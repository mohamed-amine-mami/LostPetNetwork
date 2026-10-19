using System;
using LostPetNetwork.Services.Reports;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>A photo pin on the stylised map preview. Lost = terracotta ring, found = forest ring.</summary>
    [RequireComponent(typeof(Button))]
    public class MapPinView : MonoBehaviour
    {
        [SerializeField] Image ring;
        [SerializeField] CoverImage photo;

        public PetReport Report { get; private set; }

        public void Bind(PetReport report, bool selected, Action<PetReport> onClick)
        {
            Report = report;
            photo.Set(PhotoLibrary.ForReport(report));
            ring.color = report.Type == ReportType.Lost ? Theme.Terracotta : Theme.Forest700;
            var rt = (RectTransform)transform;
            float size = selected ? 56f : 42f;
            rt.sizeDelta = new Vector2(size, size);
            rt.anchorMin = rt.anchorMax = new Vector2(report.MapX, 1f - report.MapY);
            rt.anchoredPosition = Vector2.zero;
            var button = GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke(report));
        }
    }
}
