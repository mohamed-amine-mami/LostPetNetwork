using System;
using System.Globalization;
using LostPetNetwork.Services.Auth;
using LostPetNetwork.Services.Reports;
using LostPetNetwork.Services.Sensors;
using LostPetNetwork.Services.Sos;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>One alert: where it is, what was said, and the rescuer's next step.</summary>
    public class SosDetailsScreen : MonoBehaviour
    {
        [SerializeField] ScreenManager manager;
        [SerializeField] Button backButton;
        [SerializeField] Image statusBackground;
        [SerializeField] TMP_Text statusLabel;
        [SerializeField] TMP_Text noteLabel;
        [SerializeField] TMP_Text reporterValue;
        [SerializeField] TMP_Text whenValue;
        [SerializeField] TMP_Text distanceValue;
        [SerializeField] Button mapsButton;
        [SerializeField] Button playButton;
        [SerializeField] Button guideButton;
        [SerializeField] LoadingButton actionButton;
        [SerializeField] TMP_Text hint;
        [SerializeField] MessageBanner banner;

        SosAlert _alert;
        bool _busy;

        public enum NextAction { None, Claim, Resolve }

        public static NextAction ActionFor(SosAlert alert, string userId)
        {
            if (alert == null || alert.Status == SosStatus.Resolved) return NextAction.None;
            if (alert.ReporterId == userId || alert.ResponderId == userId) return NextAction.Resolve;
            return alert.Status == SosStatus.Open ? NextAction.Claim : NextAction.None;
        }

        public static string DirectionsUrl(SosAlert alert) =>
            "https://www.google.com/maps/dir/?api=1&destination=" +
            alert.Latitude.ToString("0.#####", CultureInfo.InvariantCulture) + "," +
            alert.Longitude.ToString("0.#####", CultureInfo.InvariantCulture);

        void Awake()
        {
            backButton.onClick.AddListener(() => { if (!manager.Back()) manager.Show(ScreenId.Sos); });
            mapsButton.onClick.AddListener(() => { if (_alert != null) Application.OpenURL(DirectionsUrl(_alert)); });
            playButton.onClick.AddListener(PlayVoice);
            guideButton.onClick.AddListener(() => { if (_alert != null) ArLauncher.OpenSosGuide(_alert); });
            actionButton.Button.onClick.AddListener(OnActionClicked);
        }

        void OnEnable()
        {
            _busy = false;
            _alert = AppSession.SelectedAlert;
            if (_alert == null)
            {
                if (!manager.Back()) manager.Show(ScreenId.Sos);
                return;
            }
            banner.Hide();
            SensorHub.Location.StartTracking();
            Bind();
        }

        void OnDisable()
        {
            VoicePlayback.Stop();
            SensorHub.Location.StopTracking();
        }

        void Bind()
        {
            var a = _alert;
            bool open = a.Status == SosStatus.Open;
            statusBackground.color = a.Status == SosStatus.Resolved ? Theme.Forest100 : open ? Theme.TerracottaSoft : Theme.GoldSoft;
            statusLabel.color = a.Status == SosStatus.Resolved ? Theme.Forest900 : open ? Theme.TerracottaInk : Theme.GoldInk;
            statusLabel.text = a.StatusText.ToUpperInvariant();
            noteLabel.text = string.IsNullOrWhiteSpace(a.Note) ? "No description was added. Use the location to find the animal." : a.Note;
            reporterValue.text = a.ReporterName;
            whenValue.text = a.TimeAgo(DateTime.UtcNow);
            distanceValue.text = SosHubScreen.DistanceLabel(a, SensorHub.Location.Latest) ?? "Turn on location to see the distance";
            playButton.gameObject.SetActive(!string.IsNullOrEmpty(a.AudioPath) || (a.AudioBytes != null && a.AudioBytes.Length > 0));
            guideButton.gameObject.SetActive(ArLauncher.CanGuideToSos && a.IsActive);

            switch (ActionFor(a, AuthService.Instance.UserId))
            {
                case NextAction.Claim:
                    actionButton.gameObject.SetActive(true);
                    actionButton.SetIdleText("I'm on my way");
                    hint.text = "Tell the sender help is coming. Only one responder can take an alert.";
                    break;
                case NextAction.Resolve:
                    actionButton.gameObject.SetActive(true);
                    actionButton.SetIdleText("Mark as resolved");
                    hint.text = a.ReporterId == AuthService.Instance.UserId
                        ? "This is your alert. Resolve it once the animal is safe."
                        : "You're helping with this alert. Resolve it once the animal is safe.";
                    break;
                default:
                    actionButton.gameObject.SetActive(false);
                    hint.text = a.Status == SosStatus.Resolved ? "This alert has been resolved." : "Someone is already on their way.";
                    break;
            }
        }

        async void PlayVoice()
        {
            try
            {
                var bytes = _alert.AudioBytes ?? await SosServices.Repository.GetAudioAsync(_alert);
                if (this == null) return;
                if (bytes == null || !VoicePlayback.Play(bytes)) banner.ShowError("Couldn't play the voice note.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this != null) banner.ShowError(UiUtil.Describe(e, "Couldn't play the voice note."));
            }
        }

        async void OnActionClicked()
        {
            if (_busy || _alert == null) return;
            _busy = true;
            actionButton.SetLoading(true);
            banner.Hide();
            try
            {
                string me = AuthService.Instance.UserId;
                switch (ActionFor(_alert, me))
                {
                    case NextAction.Claim:
                        var claimed = await SosServices.Repository.ClaimAsync(_alert.Id, me);
                        if (this == null) return;
                        if (claimed == null) { banner.ShowInfo("Someone else already took this alert."); break; }
                        _alert = claimed; AppSession.SelectedAlert = claimed;
                        Bind();
                        banner.ShowInfo("Thank you. The sender can see that help is on the way.");
                        break;
                    case NextAction.Resolve:
                        var resolved = await SosServices.Repository.ResolveAsync(_alert.Id);
                        if (this == null) return;
                        if (resolved != null) { _alert = resolved; AppSession.SelectedAlert = resolved; }
                        Bind();
                        banner.ShowInfo("Marked as resolved. Thank you for helping.");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this != null) banner.ShowError(UiUtil.Describe(e, "Something went wrong. Please try again."));
            }
            finally
            {
                _busy = false;
                if (this != null) actionButton.SetLoading(false);
            }
        }
    }
}
