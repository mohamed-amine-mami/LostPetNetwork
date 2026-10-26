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

        /// <summary>Stylised preview: placed by the report's 0..1 sample position.</summary>
        public void Bind(PetReport report, bool selected, Action<PetReport> onClick)
        {
            Apply(report, selected, onClick);
            var rt = (RectTransform)transform;
            rt.anchorMin = rt.anchorMax = new Vector2(report.MapX, 1f - report.MapY);
            rt.anchoredPosition = Vector2.zero;
        }

        /// <summary>Real map: centred in the map view and moved with <see cref="SetMapPosition"/>.</summary>
        public void BindOnMap(PetReport report, bool selected, Action<PetReport> onClick, Vector2 localPosition)
        {
            Apply(report, selected, onClick);
            var rt = (RectTransform)transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = localPosition;
        }

        public void SetMapPosition(Vector2 localPosition) => ((RectTransform)transform).anchoredPosition = localPosition;

        void Apply(PetReport report, bool selected, Action<PetReport> onClick)
        {
            Report = report;
            photo.Set(PhotoLibrary.ForReport(report));
            ring.color = report.Type == ReportType.Lost ? Theme.Terracotta : Theme.Forest700;
            float size = selected ? 56f : 42f;
            ((RectTransform)transform).sizeDelta = new Vector2(size, size);
            var button = GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke(report));
        }
    }
}
