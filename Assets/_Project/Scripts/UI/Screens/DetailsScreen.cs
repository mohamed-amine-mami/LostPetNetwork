using System;
using LostPetNetwork.Services.Auth;
using LostPetNetwork.Services.Reports;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>Pet report details with the next action: contact (visitors) or mark reunited (owner). Demo data.</summary>
    public class DetailsScreen : MonoBehaviour
    {
        [SerializeField] ScreenManager manager;
        [SerializeField] Button backButton;
        [SerializeField] CoverImage photo;
        [SerializeField] StatusBadge badge;
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text summaryLabel;
        [SerializeField] TMP_Text locationValue;
        [SerializeField] TMP_Text whenValue;
        [SerializeField] TMP_Text distanceValue;
        [SerializeField] TMP_Text descriptionLabel;
        [SerializeField] TMP_Text reporterName;
        [SerializeField] TMP_Text reporterMeta;
        [SerializeField] TMP_Text reporterInitials;
        [SerializeField] LoadingButton actionButton;
        [SerializeField] TMP_Text actionHint;
        [SerializeField] MessageBanner banner;

        PetReport _report;
        bool _busy;

        public enum NextAction { None, Contact, MarkReunited }

        /// <summary>Which primary action applies to the current user (pure logic, covered by tests).</summary>
        public static NextAction ActionFor(PetReport report, string currentUserId)
        {
            if (report == null || report.Status == ReportStatus.Reunited) return NextAction.None;
            return report.OwnerUserId == currentUserId ? NextAction.MarkReunited : NextAction.Contact;
        }

        void Awake()
        {
            backButton.onClick.AddListener(() => { if (!manager.Back()) manager.Show(ScreenId.Explore); });
            actionButton.Button.onClick.AddListener(OnActionClicked);
        }

        void OnEnable()
        {
            _busy = false;
            _report = AppSession.SelectedReport;
            if (_report == null)
            {
                if (!manager.Back()) manager.Show(ScreenId.Explore);
                return;
            }
            banner.Hide();
            Bind();
        }

        void Bind()
        {
            var r = _report;
            photo.Set(PhotoLibrary.ForReport(r));
            badge.Set(r);
            nameLabel.text = r.DisplayName;
            summaryLabel.text = r.Summary;
            locationValue.text = r.LocationText;
            whenValue.text = r.TimeAgo(DateTime.UtcNow);
            distanceValue.text = r.DistanceKm > 0 ? $"{r.DistanceKm:0.#} km from you (sample value)" : "Your area";
            descriptionLabel.text = string.IsNullOrWhiteSpace(r.Description) ? "No extra details were added." : r.Description;
            reporterName.text = $"Reported by {r.OwnerName}";
            reporterMeta.text = $"{r.ContactRequests} contact request{(r.ContactRequests == 1 ? "" : "s")}";
            reporterInitials.text = Initials(r.OwnerName);

            string me = AuthService.Instance.UserId;
            switch (ActionFor(r, me))
            {
                case NextAction.Contact:
                    actionButton.gameObject.SetActive(true);
                    actionButton.SetIdleText(r.Type == ReportType.Lost ? $"I've seen {r.DisplayName}" : "This is my pet");
                    actionHint.text = "Demo: contact requests are only recorded on this device for now.";
                    break;
                case NextAction.MarkReunited:
                    actionButton.gameObject.SetActive(true);
                    actionButton.SetIdleText("Mark as reunited");
                    actionHint.text = "This is your report. Mark it once the pet is home.";
                    break;
                default:
                    actionButton.gameObject.SetActive(false);
                    actionHint.text = r.Status == ReportStatus.Reunited ? "This pet is back home. Thank you, neighbours!" : string.Empty;
                    break;
            }
        }

        static string Initials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Trim().Split(' ');
            string first = parts[0].Substring(0, 1);
            string second = parts.Length > 1 ? parts[parts.Length - 1].Substring(0, 1) : string.Empty;
            return (first + second).ToUpperInvariant();
        }

        async void OnActionClicked()
        {
            if (_busy || _report == null) return;
            _busy = true;
            actionButton.SetLoading(true);
            banner.Hide();
            try
            {
                switch (ActionFor(_report, AuthService.Instance.UserId))
                {
                    case NextAction.MarkReunited:
                        var updated = await ReportServices.Repository.MarkReunitedAsync(_report.Id);
                        if (this == null) return;
                        if (updated != null) { _report = updated; AppSession.SelectedReport = updated; }
                        Bind();
                        banner.ShowInfo("Marked as reunited. Welcome home! (demo data)");
                        break;
                    case NextAction.Contact:
                        bool sent = await ReportServices.Repository.RequestContactAsync(_report.Id, AuthService.Instance.UserId);
                        if (this == null) return;
                        Bind();
                        if (sent) banner.ShowInfo("Request recorded on this device (demo). Sending to the owner needs the backend.");
                        else banner.ShowInfo("You've already sent a request for this report.");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this != null) banner.ShowError("Something went wrong. Please try again.");
            }
            finally
            {
                _busy = false;
                if (this != null) actionButton.SetLoading(false);
            }
        }
    }
}
