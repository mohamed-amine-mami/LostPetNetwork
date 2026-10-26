using System;
using System.Linq;
using LostPetNetwork.Services.Ai;
using LostPetNetwork.Services.Auth;
using LostPetNetwork.Services.Pets;
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
        [SerializeField] GameObject petCard;
        [SerializeField] Preview3D petPreview;
        [SerializeField] TMP_Text petCaption;
        [SerializeField] GameObject matchSection;
        [SerializeField] LoadingButton findMatchesButton;
        [SerializeField] TMP_Text matchNote;
        [SerializeField] Transform matchResults;
        [SerializeField] ReportRowView matchRowPrefab;

        PetReport _report;
        bool _busy;

        public enum NextAction { None, Contact, MarkReunited }

        /// <summary>The AI helper is offered to the owner of an active report that has a photo, on real (non-demo) data.</summary>
        public static bool CanFindMatches(PetReport report, string currentUserId, bool demoData) =>
            report != null && !demoData && report.Status == ReportStatus.Active && report.OwnerUserId == currentUserId &&
            (!string.IsNullOrEmpty(report.PhotoPath) || (report.PhotoBytes != null && report.PhotoBytes.Length > 0));

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
            findMatchesButton.Button.onClick.AddListener(OnFindMatches);
            petPreview.DefaultYaw = PetFactory.DefaultYaw;
            petPreview.TiltToRotate = true;
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

        async void OnFindMatches()
        {
            if (_busy || _report == null) return;
            _busy = true;
            findMatchesButton.SetLoading(true);
            banner.Hide();
            UiUtil.ClearChildren(matchResults);
            matchNote.text = string.Empty;
            try
            {
                var result = await AiMatchService.FindAsync(_report.Id);
                if (this == null) return;
                if (!result.Ok) { banner.ShowInfo(result.Message); return; }
                if (result.Matches.Count == 0) { matchNote.text = "No similar animals were found nearby yet. We'll keep your report visible to neighbours."; return; }

                foreach (var match in result.Matches.Take(5))
                {
                    var other = await ReportServices.Repository.GetAsync(match.ReportId);
                    if (this == null) return;
                    if (other == null) continue;
                    Instantiate(matchRowPrefab, matchResults).Bind(other, OpenOther, $"{match.Score}% similar · {match.Reason}");
                }
                matchNote.text = "Suggestions only. Check the photos and talk to the person before you decide.";
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this != null) banner.ShowError(UiUtil.Describe(e, "Couldn't search for matches. Please try again."));
            }
            finally
            {
                _busy = false;
                if (this != null) findMatchesButton.SetLoading(false);
            }
        }

        void OpenOther(PetReport other)
        {
            _report = other;
            AppSession.SelectedReport = other;
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
            float km = r.DistanceFromKm(LostPetNetwork.Services.Sensors.SensorHub.Location.Latest, out bool realDistance);
            distanceValue.text = km > 0 ? $"{km:0.#} km from you{(realDistance ? "" : " (sample value)")}" : "Your area";
            BindPet3D(r);
            bool offerMatches = CanFindMatches(r, AuthService.Instance.UserId, ReportServices.Repository.IsDemo);
            matchSection.SetActive(offerMatches);
            UiUtil.ClearChildren(matchResults);
            matchNote.text = string.Empty;
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
                    actionHint.text = ReportServices.Repository.IsDemo
                        ? "Demo: contact requests are only recorded on this device for now."
                        : "The owner will see that you asked about this report.";
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

        void BindPet3D(PetReport r)
        {
            bool has = PetConfig.TryParse(r.PetAvatarJson, out var config);
            petCard.SetActive(has);
            if (!has) return;
            petPreview.ResetView();
            petPreview.SetContent(PetFactory.Build(config), PetFactory.FrameCenter, PetFactory.FrameRadius);
            petCaption.text = r.IsDemo && r.OwnerUserId != AuthService.Instance.UserId
                ? "Illustrative 3D look · drag or tilt your phone to turn"
                : "3D look · drag or tilt your phone to turn";
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
                        banner.ShowInfo(ReportServices.Repository.IsDemo ? "Marked as reunited. Welcome home! (demo data)" : "Marked as reunited. Welcome home!");
                        break;
                    case NextAction.Contact:
                        bool sent = await ReportServices.Repository.RequestContactAsync(_report.Id, AuthService.Instance.UserId);
                        if (this == null) return;
                        Bind();
                        if (sent) banner.ShowInfo(ReportServices.Repository.IsDemo ? "Request recorded on this device (demo). Sending to the owner needs the backend." : "Request sent. The owner can see it in their report.");
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
