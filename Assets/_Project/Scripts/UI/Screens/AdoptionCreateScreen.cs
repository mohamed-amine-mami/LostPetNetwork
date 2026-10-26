using System;
using LostPetNetwork.Services.Adoption;
using LostPetNetwork.Services.Auth;
using LostPetNetwork.Services.Profile;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>Form for offering a pet for adoption.</summary>
    public class AdoptionCreateScreen : MonoBehaviour
    {
        /// <summary>Chip order for the age question: under 1 year, 1–3, 4–8, 9 or more.</summary>
        public static readonly int[] AgeMonthValues = { 6, 24, 72, 120 };

        [SerializeField] ScreenManager manager;
        [SerializeField] Button backButton;
        [SerializeField] TextFieldView nameField;
        [SerializeField] ChipGroup speciesChips;
        [SerializeField] ChipGroup ageChips;
        [SerializeField] ChipGroup sizeChips;
        [SerializeField] ChipGroup energyChips;
        [SerializeField] ChipGroup kidsChips;
        [SerializeField] ChipGroup petsChips;
        [SerializeField] ChipGroup gardenChips;
        [SerializeField] TextFieldView locationField;
        [SerializeField] Button useLocationLink;
        [SerializeField] TextFieldView descriptionField;
        [SerializeField] Button pet3dButton;
        [SerializeField] TMP_Text pet3dCaption;
        [SerializeField] LoadingButton submitButton;
        [SerializeField] MessageBanner banner;

        bool _busy;

        public static int AgeIndexFor(int months) => months < 12 ? 0 : months < 48 ? 1 : months < 108 ? 2 : 3;

        void Awake()
        {
            backButton.onClick.AddListener(() => { if (!manager.Back()) manager.Show(ScreenId.Adoption); });
            useLocationLink.onClick.AddListener(UseCurrentLocation);
            pet3dButton.onClick.AddListener(OpenPetCreator);
            submitButton.Button.onClick.AddListener(Submit);
        }

        void OnEnable()
        {
            _busy = false;
            banner.Hide();
            nameField.ClearError(); locationField.ClearError(); descriptionField.ClearError();
            var d = AppSession.AdoptionDraft;
            nameField.Input.SetTextWithoutNotify(d.Name ?? string.Empty);
            locationField.Input.SetTextWithoutNotify(d.LocationText ?? string.Empty);
            descriptionField.Input.SetTextWithoutNotify(d.Description ?? string.Empty);
            speciesChips.Select(d.Species, false);
            ageChips.Select(AgeIndexFor(d.AgeMonths), false);
            sizeChips.Select(d.Size, false);
            energyChips.Select((int)d.Energy, false);
            kidsChips.Select(d.GoodWithKids ? 0 : 1, false);
            petsChips.Select(d.GoodWithPets ? 0 : 1, false);
            gardenChips.Select(d.NeedsGarden ? 0 : 1, false);
            pet3dCaption.text = string.IsNullOrEmpty(d.PetAvatarJson) ? "Optional — a default look is used otherwise" : "3D look added — tap to edit";
        }

        /// <summary>Copies the form into the draft (also used before leaving for the 3D creator, and by tests).</summary>
        public void SyncFormToDraft()
        {
            var d = AppSession.AdoptionDraft;
            if (d.GeoLabel != null && locationField.Text != d.GeoLabel) { d.Latitude = null; d.Longitude = null; d.GeoLabel = null; }
            d.Name = nameField.Text;
            d.Species = speciesChips.SelectedIndex;
            d.AgeMonths = AgeMonthValues[Mathf.Clamp(ageChips.SelectedIndex, 0, AgeMonthValues.Length - 1)];
            d.Size = sizeChips.SelectedIndex;
            d.Energy = (Level)energyChips.SelectedIndex;
            d.GoodWithKids = kidsChips.SelectedIndex == 0;
            d.GoodWithPets = petsChips.SelectedIndex == 0;
            d.NeedsGarden = gardenChips.SelectedIndex == 0;
            d.LocationText = locationField.Text;
            d.Description = descriptionField.Text;
        }

        void OpenPetCreator()
        {
            SyncFormToDraft();
            AppSession.PetLookTarget = PetLookTarget.Adoption;
            manager.Push(ScreenId.PetCreator);
        }

        async void UseCurrentLocation()
        {
            banner.ShowInfo("Finding your location…");
            var found = await CurrentPlace.FindAsync();
            if (this == null) return;
            if (!found.Ok) { banner.ShowError(found.Error); return; }
            var d = AppSession.AdoptionDraft;
            d.Latitude = found.Fix.Latitude;
            d.Longitude = found.Fix.Longitude;
            d.GeoLabel = found.Label;
            d.LocationText = found.Label;
            locationField.Input.SetTextWithoutNotify(found.Label);
            locationField.ClearError();
            banner.ShowInfo("Location added. You can edit the text if you like.");
        }

        async void Submit()
        {
            if (_busy) return;
            banner.Hide();
            SyncFormToDraft();
            var errors = AppSession.AdoptionDraft.Validate();
            nameField.SetError(errors.TryGetValue("name", out var n) ? n : null);
            locationField.SetError(errors.TryGetValue("location", out var l) ? l : null);
            descriptionField.SetError(errors.TryGetValue("description", out var ds) ? ds : null);
            if (errors.Count > 0) return;

            _busy = true;
            submitButton.SetLoading(true);
            try
            {
                string ownerName = ProfileService.Instance.Cached?.display_name ?? "You";
                var listing = await AdoptionServices.Repository.CreateAsync(AppSession.AdoptionDraft, AuthService.Instance.UserId ?? "unknown", ownerName);
                if (this == null) return;
                AppSession.AdoptionDraft = new AdoptionDraft();
                AppSession.FlashMessage = AdoptionServices.Repository.IsDemo
                    ? $"{listing.Name} is listed. Demo data: it is kept only while the app is open."
                    : $"{listing.Name} is listed for adoption.";
                manager.Show(ScreenId.Adoption);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this != null) banner.ShowError(UiUtil.Describe(e, "Couldn't list the pet. Please try again."));
            }
            finally
            {
                _busy = false;
                if (this != null) submitButton.SetLoading(false);
            }
        }
    }
}
