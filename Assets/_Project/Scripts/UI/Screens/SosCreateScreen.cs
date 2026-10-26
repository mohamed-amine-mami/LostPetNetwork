using System;
using System.Linq;
using LostPetNetwork.Services.Auth;
using LostPetNetwork.Services.Profile;
using LostPetNetwork.Services.Sensors;
using LostPetNetwork.Services.Sos;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>
    /// Raise an emergency alert: the phone's position is attached automatically, with an optional note and a
    /// voice note. After sending, the same screen shows the live alert and lets the sender resolve it.
    /// </summary>
    public class SosCreateScreen : MonoBehaviour
    {
        [SerializeField] ScreenManager manager;
        [SerializeField] Button backButton;
        [SerializeField] GameObject formRoot;
        [SerializeField] GameObject sentRoot;
        [SerializeField] TMP_Text locationLabel;
        [SerializeField] Button refreshLocationButton;
        [SerializeField] TextFieldView noteField;
        [SerializeField] Button recordButton;
        [SerializeField] TMP_Text recordLabel;
        [SerializeField] TMP_Text recordHint;
        [SerializeField] Button playButton;
        [SerializeField] Button removeVoiceButton;
        [SerializeField] LoadingButton sendButton;
        [SerializeField] TMP_Text sentTitle;
        [SerializeField] TMP_Text sentText;
        [SerializeField] LoadingButton resolveButton;
        [SerializeField] Button seeAlertsButton;
        [SerializeField] MessageBanner banner;

        SosDraft _draft = new SosDraft();
        SosAlert _sent;
        bool _busy;
        bool _recording;
        int _visitId;

        public SosDraft Draft => _draft;

        void Awake()
        {
            backButton.onClick.AddListener(Leave);
            refreshLocationButton.onClick.AddListener(() => FindLocation());
            recordButton.onClick.AddListener(ToggleRecording);
            playButton.onClick.AddListener(() => { if (!VoicePlayback.Play(_draft.AudioWav)) banner.ShowError("Couldn't play the voice note."); });
            removeVoiceButton.onClick.AddListener(() => { _draft.AudioWav = null; RefreshVoice(); });
            sendButton.Button.onClick.AddListener(Send);
            resolveButton.Button.onClick.AddListener(Resolve);
            seeAlertsButton.onClick.AddListener(() => manager.Show(ScreenId.Sos));
        }

        async void OnEnable()
        {
            int visit = ++_visitId;
            _busy = false;
            _draft = new SosDraft();
            _sent = null;
            banner.Hide();
            noteField.ClearError();
            noteField.Input.SetTextWithoutNotify(string.Empty);
            formRoot.SetActive(true);
            sentRoot.SetActive(false);
            RefreshVoice();
            SetRecording(false);
            FindLocation();

            // Someone with an alert already open sees it instead of a blank form.
            try
            {
                string me = AuthService.Instance.UserId;
                var active = await SosServices.Repository.GetActiveAsync();
                if (this == null || visit != _visitId) return;
                var mine = active.FirstOrDefault(a => a.ReporterId == me);
                if (mine != null) ShowSent(mine);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        void OnDisable()
        {
            _visitId++;
            if (_recording) MicRecorder.Cancel();
            VoicePlayback.Stop();
            _recording = false;
        }

        void Update()
        {
            if (!_recording) return;
            if (MicRecorder.Elapsed >= MicRecorder.MaxSeconds) StopRecording();
            else recordLabel.text = $"Stop · {MicRecorder.Elapsed:0}s";
        }

        async void FindLocation()
        {
            int visit = _visitId;
            locationLabel.text = "Finding your location…";
            var found = await CurrentPlace.FindFixAsync();
            if (this == null || visit != _visitId) return;
            if (found.Ok)
            {
                // The position counts as soon as we have it; the street name follows when the lookup answers.
                _draft.HasLocation = true;
                _draft.Latitude = found.Fix.Latitude;
                _draft.Longitude = found.Fix.Longitude;
                locationLabel.text = $"Location found ({found.Fix.Latitude:0.0000}, {found.Fix.Longitude:0.0000})";
                string name = await CurrentPlace.NameAsync(found.Fix);
                if (this != null && visit == _visitId) locationLabel.text = name;
                return;
            }
#if UNITY_EDITOR
            // Computers have no GPS: use a clearly labelled test position so the flow can be tried in the Editor.
            _draft.HasLocation = true;
            _draft.Latitude = 36.8065;
            _draft.Longitude = 10.1815;
            locationLabel.text = "Test position (Editor only): Tunis centre";
#else
            _draft.HasLocation = false;
            locationLabel.text = found.Error;
#endif
        }

        void ToggleRecording()
        {
            if (_recording) StopRecording();
            else StartRecording();
        }

        async void StartRecording()
        {
            int visit = _visitId;
            string error = await MicRecorder.StartAsync();
            if (this == null || visit != _visitId) return;
            if (error != null) { banner.ShowError(error); return; }
            SetRecording(true);
        }

        void StopRecording()
        {
            _draft.AudioWav = MicRecorder.Stop();
            SetRecording(false);
            RefreshVoice();
        }

        void SetRecording(bool recording)
        {
            _recording = recording;
            recordLabel.text = recording ? "Stop · 0s" : _draft.AudioWav != null ? "Record again" : "Record a voice note";
        }

        void RefreshVoice()
        {
            bool has = _draft.AudioWav != null && _draft.AudioWav.Length > 0;
            playButton.gameObject.SetActive(has);
            removeVoiceButton.gameObject.SetActive(has);
            recordHint.text = has
                ? $"Voice note ready ({Math.Max(1, (_draft.AudioWav.Length - 44) / (MicRecorder.SampleRate * 2))} s)"
                : $"Optional · up to {MicRecorder.MaxSeconds} seconds";
            if (!_recording) recordLabel.text = has ? "Record again" : "Record a voice note";
        }

        async void Send()
        {
            if (_busy) return;
            banner.Hide();
            noteField.ClearError();
            _draft.Note = noteField.Text;
            string error = _draft.Validate();
            if (error != null)
            {
                if (!_draft.HasLocation) banner.ShowError(error);
                else noteField.SetError(error);
                return;
            }

            _busy = true;
            sendButton.SetLoading(true);
            try
            {
                string name = ProfileService.Instance.Cached?.display_name ?? "You";
                var alert = await SosServices.Repository.CreateAsync(_draft, AuthService.Instance.UserId ?? "unknown", name);
                if (this == null) return;
                ShowSent(alert);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this != null) banner.ShowError(UiUtil.Describe(e, "Couldn't send the alert. Please try again."));
            }
            finally
            {
                _busy = false;
                if (this != null) sendButton.SetLoading(false);
            }
        }

        void ShowSent(SosAlert alert)
        {
            _sent = alert;
            formRoot.SetActive(false);
            sentRoot.SetActive(true);
            sentTitle.text = alert.Status == SosStatus.Responding ? "Help is on the way" : "Your alert is live";
            sentText.text = alert.Status == SosStatus.Responding
                ? "A neighbour has answered your alert. Stay safe and keep your phone with you."
                : "Rescuers and neighbours nearby can see it now. Stay close to the animal if it is safe to do so.";
        }

        async void Resolve()
        {
            if (_busy || _sent == null) return;
            _busy = true;
            resolveButton.SetLoading(true);
            try
            {
                await SosServices.Repository.ResolveAsync(_sent.Id);
                if (this == null) return;
                AppSession.FlashMessage = "Alert resolved. Thank you for helping.";
                manager.Show(ScreenId.Sos);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this != null) banner.ShowError(UiUtil.Describe(e, "Couldn't update the alert. Please try again."));
            }
            finally
            {
                _busy = false;
                if (this != null) resolveButton.SetLoading(false);
            }
        }

        void Leave()
        {
            if (!manager.Back()) manager.Show(ScreenId.Sos);
        }
    }
}
