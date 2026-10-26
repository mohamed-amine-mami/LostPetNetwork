using System;
using LostPetNetwork.Services.Sos;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>One alert in the rescue list.</summary>
    [RequireComponent(typeof(Button))]
    public class SosRowView : MonoBehaviour
    {
        [SerializeField] Image statusBackground;
        [SerializeField] TMP_Text statusLabel;
        [SerializeField] TMP_Text noteLabel;
        [SerializeField] TMP_Text metaLabel;
        [SerializeField] GameObject demoTag;

        public SosAlert Alert { get; private set; }

        public void Bind(SosAlert alert, string distance, Action<SosAlert> onClick)
        {
            Alert = alert;
            bool open = alert.Status == SosStatus.Open;
            statusBackground.color = open ? Theme.TerracottaSoft : Theme.Forest100;
            statusLabel.color = open ? Theme.TerracottaInk : Theme.Forest900;
            statusLabel.text = alert.StatusText.ToUpperInvariant();
            noteLabel.text = string.IsNullOrWhiteSpace(alert.Note) ? "No description. Check the location." : alert.Note;
            string meta = alert.TimeAgo(DateTime.UtcNow) + " · " + alert.ReporterName;
            metaLabel.text = string.IsNullOrEmpty(distance) ? meta : distance + " · " + meta;
            if (demoTag != null) demoTag.SetActive(alert.IsDemo);
            var button = GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke(alert));
        }
    }
}
