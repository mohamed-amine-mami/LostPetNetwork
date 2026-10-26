using System;
using LostPetNetwork.Services.Adoption;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>A short questionnaire about the user's home. It is private and only used to score matches.</summary>
    public class AdopterProfileScreen : MonoBehaviour
    {
        /// <summary>Chip order for the home size question.</summary>
        public static readonly int[] SizeValues = { 30, 60, 110, 200 };

        [SerializeField] ScreenManager manager;
        [SerializeField] Button backButton;
        [SerializeField] ChipGroup homeTypeChips;
        [SerializeField] ChipGroup sizeChips;
        [SerializeField] ChipGroup gardenChips;
        [SerializeField] ChipGroup kidsChips;
        [SerializeField] ChipGroup petsChips;
        [SerializeField] ChipGroup activityChips;
        [SerializeField] LoadingButton saveButton;
        [SerializeField] GameObject loading;
        [SerializeField] MessageBanner banner;

        bool _busy;

        public static int SizeIndexFor(int m2)
        {
            int best = 0;
            for (int i = 0; i < SizeValues.Length; i++)
                if (m2 >= SizeValues[i] - 15) best = i;
            return best;
        }

        void Awake()
        {
            backButton.onClick.AddListener(Leave);
            saveButton.Button.onClick.AddListener(Save);
        }

        async void OnEnable()
        {
            banner.Hide();
            Show(AppSession.MyHome ?? new AdopterProfile());
            if (AppSession.MyHomeLoaded) return;
            loading.SetActive(true);
            try
            {
                AppSession.MyHome = await AdoptionServices.Repository.GetMyHomeAsync();
                AppSession.MyHomeLoaded = true;
                if (this != null && AppSession.MyHome != null) Show(AppSession.MyHome);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this != null) banner.ShowError(UiUtil.Describe(e, "Couldn't load your home details."));
            }
            finally
            {
                if (this != null) loading.SetActive(false);
            }
        }

        void Show(AdopterProfile h)
        {
            homeTypeChips.Select((int)h.HomeType, false);
            sizeChips.Select(SizeIndexFor(h.HomeSizeM2), false);
            gardenChips.Select(h.HasGarden ? 0 : 1, false);
            kidsChips.Select(h.HasKids ? 0 : 1, false);
            petsChips.Select(h.HasOtherPets ? 0 : 1, false);
            activityChips.Select((int)h.Activity, false);
        }

        public AdopterProfile Read() => new AdopterProfile
        {
            HomeType = (HomeType)homeTypeChips.SelectedIndex,
            HomeSizeM2 = SizeValues[Mathf.Clamp(sizeChips.SelectedIndex, 0, SizeValues.Length - 1)],
            HasGarden = gardenChips.SelectedIndex == 0,
            HasKids = kidsChips.SelectedIndex == 0,
            HasOtherPets = petsChips.SelectedIndex == 0,
            Activity = (Level)activityChips.SelectedIndex
        };

        async void Save()
        {
            if (_busy) return;
            _busy = true;
            saveButton.SetLoading(true);
            banner.Hide();
            try
            {
                var home = Read();
                await AdoptionServices.Repository.SaveMyHomeAsync(home);
                if (this == null) return;
                AppSession.MyHome = home;
                AppSession.MyHomeLoaded = true;
                AppSession.FlashMessage = "Home saved. Match scores are now shown on each pet.";
                Leave();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this != null) banner.ShowError(UiUtil.Describe(e, "Couldn't save your home details. Please try again."));
            }
            finally
            {
                _busy = false;
                if (this != null) saveButton.SetLoading(false);
            }
        }

        void Leave()
        {
            if (!manager.Back()) manager.Show(ScreenId.Adoption);
        }
    }
}
