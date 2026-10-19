using System;
using System.Linq;
using LostPetNetwork.Services.Auth;
using LostPetNetwork.Services.Profile;
using LostPetNetwork.Services.Reports;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>
    /// Profile: shows and edits the signed-in user's REAL profile (Supabase, protected by RLS) and signs out.
    /// The report counters come from the demo repository.
    /// </summary>
    public class ProfileScreen : MonoBehaviour
    {
        [SerializeField] ScreenManager manager;
        [SerializeField] GameObject viewRoot;
        [SerializeField] GameObject editRoot;
        [SerializeField] TMP_Text initials;
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text emailLabel;
        [SerializeField] TMP_Text cityLabel;
        [SerializeField] TMP_Text memberLabel;
        [SerializeField] TMP_Text reportsCount;
        [SerializeField] TMP_Text reunitedCount;
        [SerializeField] Button editButton;
        [SerializeField] TextFieldView nameField;
        [SerializeField] TextFieldView cityField;
        [SerializeField] TextFieldView phoneField;
        [SerializeField] LoadingButton saveButton;
        [SerializeField] Button cancelButton;
        [SerializeField] LoadingButton signOutButton;
        [SerializeField] GameObject loading;
        [SerializeField] MessageBanner banner;

        bool _busy;

        void Awake()
        {
            editButton.onClick.AddListener(BeginEdit);
            cancelButton.onClick.AddListener(() => ShowEdit(false));
            saveButton.Button.onClick.AddListener(OnSaveClicked);
            signOutButton.Button.onClick.AddListener(OnSignOutClicked);
        }

        async void OnEnable()
        {
            banner.Hide();
            ShowEdit(false);
            BindProfile(ProfileService.Instance.Cached);
            CountReports();

            if (ProfileService.Instance.Cached != null) return;
            loading.SetActive(true);
            try
            {
                var result = await ProfileService.Instance.GetMyProfileAsync();
                if (this == null) return;
                if (result.Ok) BindProfile(result.Profile);
                else banner.ShowError(result.Error);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this != null) banner.ShowError("Couldn't load your profile.");
            }
            finally
            {
                if (this != null) loading.SetActive(false);
            }
        }

        async void CountReports()
        {
            try
            {
                var mine = await ReportServices.Repository.GetMineAsync(AuthService.Instance.UserId);
                if (this == null) return;
                reportsCount.text = mine.Count.ToString();
                reunitedCount.text = mine.Count(r => r.Status == ReportStatus.Reunited).ToString();
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        void BindProfile(UserProfile p)
        {
            emailLabel.text = AuthService.Instance.Session?.email ?? string.Empty;
            string name = p?.display_name;
            nameLabel.text = string.IsNullOrEmpty(name) ? "Your profile" : name;
            initials.text = InitialsOf(name);
            cityLabel.text = string.IsNullOrWhiteSpace(p?.city) ? "City not set" : p.city;
            memberLabel.text = MemberSince(p?.created_at);
        }

        public static string InitialsOf(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string a = parts[0].Substring(0, 1);
            string b = parts.Length > 1 ? parts[parts.Length - 1].Substring(0, 1) : string.Empty;
            return (a + b).ToUpperInvariant();
        }

        static string MemberSince(string createdAt)
        {
            return DateTime.TryParse(createdAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
                ? $"Neighbour since {dt:MMMM yyyy}"
                : string.Empty;
        }

        void ShowEdit(bool edit)
        {
            editRoot.SetActive(edit);
            viewRoot.SetActive(!edit);
        }

        void BeginEdit()
        {
            var p = ProfileService.Instance.Cached;
            banner.Hide();
            nameField.ClearError(); cityField.ClearError(); phoneField.ClearError();
            nameField.Input.SetTextWithoutNotify(p?.display_name ?? string.Empty);
            cityField.Input.SetTextWithoutNotify(p?.city ?? string.Empty);
            phoneField.Input.SetTextWithoutNotify(p?.phone ?? string.Empty);
            ShowEdit(true);
        }

        async void OnSaveClicked()
        {
            if (_busy) return;
            banner.Hide();
            string nameError = ProfileValidator.ValidateDisplayName(nameField.Text);
            string cityError = ProfileValidator.ValidateCity(cityField.Text);
            string phoneError = ProfileValidator.ValidatePhone(phoneField.Text);
            nameField.SetError(nameError); cityField.SetError(cityError); phoneField.SetError(phoneError);
            if (nameError != null || cityError != null || phoneError != null) return;

            _busy = true;
            saveButton.SetLoading(true);
            try
            {
                var result = await ProfileService.Instance.UpdateMyProfileAsync(nameField.Text.Trim(), cityField.Text.Trim(), phoneField.Text.Trim());
                if (this == null) return;
                if (!result.Ok) { banner.ShowError(result.Error); return; }
                BindProfile(result.Profile);
                ShowEdit(false);
                banner.ShowInfo("Profile saved.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this != null) banner.ShowError("Couldn't save your profile. Please try again.");
            }
            finally
            {
                _busy = false;
                if (this != null) saveButton.SetLoading(false);
            }
        }

        async void OnSignOutClicked()
        {
            if (_busy) return;
            _busy = true;
            signOutButton.SetLoading(true);
            try { await AuthService.Instance.SignOutAsync(); }
            catch (Exception e) { Debug.LogException(e); }
            finally
            {
                _busy = false;
                if (this != null) signOutButton.SetLoading(false);
            }
        }
    }
}
