using LostPetNetwork.Services.Reports;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>LOST / FOUND / REUNITED pill. Status is always shown as text, never colour alone.</summary>
    public class StatusBadge : MonoBehaviour
    {
        [SerializeField] Image background;
        [SerializeField] TMP_Text label;

        public string Text => label.text;

        public void Set(PetReport report)
        {
            if (report.Status == ReportStatus.Reunited) Apply("REUNITED", Theme.GoldSoft, Theme.GoldInk);
            else if (report.Type == ReportType.Lost) Apply("LOST", Theme.TerracottaSoft, Theme.TerracottaInk);
            else Apply("FOUND", Theme.Forest100, Theme.Forest900);
        }

        void Apply(string text, Color back, Color fore)
        {
            label.text = text;
            label.color = fore;
            background.color = back;
        }
    }
}
