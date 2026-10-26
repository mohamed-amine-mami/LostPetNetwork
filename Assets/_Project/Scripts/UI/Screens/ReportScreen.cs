using System;
using System.Collections.Generic;
using LostPetNetwork.Services.Auth;
using LostPetNetwork.Services.Profile;
using LostPetNetwork.Services.Reports;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>
    /// Report a lost or found pet: form → review → post. The report is stored by the DEMO repository
    /// (in memory) because no reports table exists in Supabase yet.
    /// </summary>
    public class ReportScreen : MonoBehaviour
    {
        [SerializeField] ScreenManager manager;
        [SerializeField] GameObject formRoot;
        [SerializeField] Button backButton;
        [SerializeField] Button lostToggle;
        [SerializeField] Image lostToggleBackground;
        [SerializeField] TMP_Text lostToggleLabel;
        [SerializeField] Button foundToggle;
        [SerializeField] Image foundToggleBackground;
        [SerializeField] TMP_Text foundToggleLabel;
        [SerializeField] Button photoBox;
        [SerializeField] CoverImage photoPreview;
        [SerializeField] GameObject photoPlaceholder;
        [SerializeField] ChipGroup speciesChips;
        [SerializeField] TextFieldView locationField;
        [SerializeField] Button useLocationLink;
        [SerializeField] ChipGroup whenChips;
        [SerializeField] Button moreToggle;
        [SerializeField] GameObject moreSection;
        [SerializeField] TextFieldView nameField;
        [SerializeField] TextFieldView descriptionField;
        [SerializeField] LoadingButton reviewButton;
        [SerializeField] GameObject demoNote;
        [SerializeField] Button pet3dButton;
        [SerializeField] TMP_Text pet3dCaption;
        [SerializeField] MessageBanner banner;
        [SerializeField] GameObject reviewRoot;
        [SerializeField] TMP_Text reviewSummary;
        [SerializeField] CoverImage reviewPhoto;
        [SerializeField] LoadingButton postButton;
        [SerializeField] Button editButton;
        [SerializeField] MessageBanner reviewBanner;

        bool _busy;

        public bool IsReviewing => reviewRoot.activeSelf;

        void Awake()
        {
            backButton.onClick.AddListener(OnBack);
            lostToggle.onClick.AddListener(() => SetType(ReportType.Lost));
            foundToggle.onClick.AddListener(() => SetType(ReportType.Found));
            photoBox.onClick.AddListener(OpenCamera);
            pet3dButton.onClick.AddListener(OpenPetCreator);
            useLocationLink.onClick.AddListener(UseCurrentLocation);
            moreToggle.onClick.AddListener(() => moreSection.SetActive(!moreSection.activeSelf));
            reviewButton.Button.onClick.AddListener(OnReviewClicked);
            editButton.onClick.AddListener(() => ShowReview(false));
            postButton.Button.onClick.AddListener(OnPostClicked);
        }

        void OnEnable()
        {
            _busy = false;
            ShowReview(false);
            ApplyDraftToForm();
        }

        // ------------------------------------------------------------------ draft <-> form

        void ApplyDraftToForm()
        {
            var d = AppSession.Draft;
            banner.Hide();
            ClearErrors();
            SetTypeVisuals(d.Type);
            speciesChips.Select(d.Species.HasValue ? (int)d.Species.Value : 0, false);
            whenChips.Select((int)d.When, false);
            locationField.Input.SetTextWithoutNotify(d.LocationText ?? string.Empty);
            nameField.Input.SetTextWithoutNotify(d.PetName ?? string.Empty);
            descriptionField.Input.SetTextWithoutNotify(d.Description ?? string.Empty);
            moreSection.SetActive(!string.IsNullOrEmpty(d.PetName) || !string.IsNullOrEmpty(d.Description));
            RefreshPhoto();
            pet3dCaption.text = string.IsNullOrEmpty(d.PetAvatarJson) ? "Optional — helps neighbours recognise your pet" : "3D look added — tap to edit";
        }

        public void SyncFormToDraft()
        {
            var d = AppSession.Draft;
            if (d.GeoLabel != null && locationField.Text != d.GeoLabel) { d.Latitude = null; d.Longitude = null; d.GeoLabel = null; }
            d.Species = (PetSpecies)speciesChips.SelectedIndex;
            d.When = (WhenOption)whenChips.SelectedIndex;
            d.LocationText = locationField.Text;
            d.PetName = nameField.Text;
            d.Description = descriptionField.Text;
        }

        void ClearErrors()
        {
            locationField.ClearError();
            nameField.ClearError();
            descriptionField.ClearError();
        }

        void RefreshPhoto()
        {
            var d = AppSession.Draft;
            bool has = d.HasPhoto;
            photoPlaceholder.SetActive(!has);
            photoPreview.gameObject.SetActive(has);
            if (has) photoPreview.Set(PhotoLibrary.FromJpeg(d.PhotoBytes));
        }

        void SetType(ReportType type)
        {
            AppSession.Draft.Type = type;
            SetTypeVisuals(type);
        }

        void SetTypeVisuals(ReportType type)
        {
            bool lost = type == ReportType.Lost;
            lostToggleBackground.color = lost ? Theme.Terracotta : Color.clear;
            lostToggleLabel.color = lost ? Theme.OnBrand : Theme.TextSecondary;
            foundToggleBackground.color = lost ? Color.clear : Theme.Forest700;
            foundToggleLabel.color = lost ? Theme.TextSecondary : Theme.OnBrand;
            reviewButton.SetIdleText(lost ? "Review lost report" : "Review found report");
        }

        // ------------------------------------------------------------------ actions

        void OnBack()
        {
            if (IsReviewing) { ShowReview(false); return; }
            AppSession.Draft = new ReportDraft();
            if (!manager.Back()) manager.Show(ScreenId.Home);
        }

        /// <summary>Fills the location with the phone's position and, when possible, its street or area name.</summary>
        async void UseCurrentLocation()
        {
            banner.Hide();
            banner.ShowInfo("Finding your location…");
            var found = await CurrentPlace.FindAsync();
            if (this == null) return;
            if (!found.Ok) { banner.ShowError(found.Error); return; }

            var d = AppSession.Draft;
            d.Latitude = found.Fix.Latitude;
            d.Longitude = found.Fix.Longitude;
            d.GeoLabel = found.Label;
            d.LocationText = found.Label;
            locationField.Input.SetTextWithoutNotify(found.Label);
            locationField.ClearError();
            banner.ShowInfo("Location added. You can edit the text if you like.");
        }

        void OpenPetCreator()
        {
            SyncFormToDraft();
            AppSession.PetLookTarget = PetLookTarget.Report;
            manager.Push(ScreenId.PetCreator);
        }

        void OpenCamera()
        {
            SyncFormToDraft();
            manager.Push(ScreenId.Camera);
        }

        void OnReviewClicked()
        {
            banner.Hide();
            ClearErrors();
            SyncFormToDraft();
            var errors = AppSession.Draft.Validate();
            if (errors.Count > 0)
            {
                if (errors.TryGetValue(ReportDraft.LocationKey, out var loc)) locationField.SetError(loc);
                if (errors.TryGetValue(ReportDraft.NameKey, out var nm)) { nameField.SetError(nm); moreSection.SetActive(true); }
                if (errors.TryGetValue(ReportDraft.DescriptionKey, out var ds)) { descriptionField.SetError(ds); moreSection.SetActive(true); }
                if (errors.ContainsKey(ReportDraft.SpeciesKey)) banner.ShowError(errors[ReportDraft.SpeciesKey]);
                return;
            }
            reviewSummary.text = BuildSummary(AppSession.Draft);
            reviewPhoto.transform.parent.gameObject.SetActive(AppSession.Draft.HasPhoto);
            if (AppSession.Draft.HasPhoto) reviewPhoto.Set(PhotoLibrary.FromJpeg(AppSession.Draft.PhotoBytes));
            reviewBanner.Hide();
            ShowReview(true);
        }

        public static string BuildSummary(ReportDraft d)
        {
            var lines = new List<string>
            {
                $"<b>{(d.Type == ReportType.Lost ? "Lost" : "Found")} {d.Species?.ToString().ToLowerInvariant() ?? "pet"}</b>",
                $"Last seen: {d.LocationText.Trim()}",
                $"When: {WhenText(d.When)}"
            };
            if (!string.IsNullOrWhiteSpace(d.PetName)) lines.Add($"Name: {d.PetName.Trim()}");
            if (!string.IsNullOrWhiteSpace(d.Description)) lines.Add($"Details: {d.Description.Trim()}");
            lines.Add(d.HasPhoto ? "Photo: attached" : "Photo: none (reports with a clear photo are easier to match)");
            return string.Join("\n", lines);
        }

        static string WhenText(WhenOption w)
        {
            switch (w)
            {
                case WhenOption.JustNow: return "Just now";
                case WhenOption.Today: return "Earlier today";
                case WhenOption.Yesterday: return "Yesterday";
                default: return "A few days ago";
            }
        }

        /// <summary>Hooked to the review header's back arrow (persistent listener set by the scene builder).</summary>
        public void ShowFormFromReview() => ShowReview(false);

        void ShowReview(bool review)
        {
            reviewRoot.SetActive(review);
            if (demoNote != null) demoNote.SetActive(ReportServices.Repository.IsDemo);
            formRoot.SetActive(!review);
        }

        async void OnPostClicked()
        {
            if (_busy) return;
            _busy = true;
            postButton.SetLoading(true);
            reviewBanner.Hide();
            try
            {
                string userId = AuthService.Instance.UserId ?? "unknown";
                string ownerName = ProfileService.Instance.Cached?.display_name ?? "You";
                var report = await ReportServices.Repository.CreateAsync(AppSession.Draft, userId, ownerName);
                if (this == null) return;
                AppSession.Draft = new ReportDraft();
                AppSession.FlashMessage = ReportServices.Repository.IsDemo
                    ? $"Report posted for {report.DisplayName}. Demo data: it is kept only while the app is open."
                    : report.PhotoUploadFailed
                        ? $"Report posted for {report.DisplayName}, but the photo couldn't be uploaded."
                        : $"Report posted for {report.DisplayName}.";
                manager.Show(ScreenId.MyReports);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this != null) reviewBanner.ShowError(UiUtil.Describe(e, "Couldn't post the report. Please try again."));
            }
            finally
            {
                _busy = false;
                if (this != null) postButton.SetLoading(false);
            }
        }
    }
}
