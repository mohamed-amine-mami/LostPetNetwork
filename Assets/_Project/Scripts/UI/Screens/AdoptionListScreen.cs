using System;
using System.Collections.Generic;
using System.Linq;
using LostPetNetwork.Services.Adoption;
using LostPetNetwork.Services.Auth;
using LostPetNetwork.Services.Reports;
using LostPetNetwork.Services.Sensors;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>Pets looking for a home. With the user's home profile, each card shows a match score and the best fits come first.</summary>
    public class AdoptionListScreen : MonoBehaviour
    {
        [SerializeField] ScreenManager manager;
        [SerializeField] Button homeButton;
        [SerializeField] Button listButton;
        [SerializeField] ChipGroup chips;
        [SerializeField] TMP_Text countLabel;
        [SerializeField] Transform listRoot;
        [SerializeField] AdoptionCardView cardPrefab;
        [SerializeField] GameObject loading;
        [SerializeField] GameObject emptyState;
        [SerializeField] MessageBanner banner;

        List<AdoptionListing> _all = new List<AdoptionListing>();

        void Awake()
        {
            homeButton.onClick.AddListener(() => manager.Push(ScreenId.AdopterProfile));
            listButton.onClick.AddListener(() =>
            {
                AppSession.AdoptionDraft = new AdoptionDraft();
                manager.Push(ScreenId.AdoptionCreate);
            });
            chips.Changed += _ => Render();
        }

        async void OnEnable()
        {
            banner.Hide();
            loading.SetActive(true);
            emptyState.SetActive(false);
            SensorHub.Location.StartTracking();
            try
            {
                if (!AppSession.MyHomeLoaded)
                {
                    AppSession.MyHome = await AdoptionServices.Repository.GetMyHomeAsync();
                    AppSession.MyHomeLoaded = true;
                }
                _all = (await AdoptionServices.Repository.GetAvailableAsync()).ToList();
                if (this == null) return;
                Render();
                string flash = AppSession.ConsumeFlash();
                if (flash != null) banner.ShowInfo(flash);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this != null) banner.ShowError(UiUtil.Describe(e, "Couldn't load the pets. Please try again."));
            }
            finally
            {
                if (this != null) loading.SetActive(false);
            }
        }

        void OnDisable() => SensorHub.Location.StopTracking();

        /// <summary>Chip order: All, Dogs, Cats.</summary>
        public static List<AdoptionListing> Filter(IEnumerable<AdoptionListing> listings, int chipIndex) =>
            listings.Where(l => chipIndex == 0 || (chipIndex == 1 && !l.IsCat) || (chipIndex == 2 && l.IsCat)).ToList();

        /// <summary>Best match first when the home is known, otherwise newest first.</summary>
        public static List<AdoptionListing> Arrange(IEnumerable<AdoptionListing> listings, AdopterProfile home) =>
            home == null
                ? listings.OrderByDescending(l => l.CreatedAtUtc).ToList()
                : listings.OrderByDescending(l => AdoptionCompatibility.Score(l, home).Score)
                          .ThenByDescending(l => l.CreatedAtUtc).ToList();

        void Render()
        {
            UiUtil.ClearChildren(listRoot);
            var home = AppSession.MyHome;
            var me = SensorHub.Location.Latest;
            var shown = Arrange(Filter(_all, chips.SelectedIndex), home);
            foreach (var listing in shown)
            {
                string distance = null;
                if (me.HasValue && listing.HasLocation)
                {
                    double km = GeoMath.DistanceMeters(me.Value.Latitude, me.Value.Longitude, listing.Latitude, listing.Longitude) / 1000.0;
                    distance = km < 1 ? $"{(int)(km * 100) * 10} m" : $"{km:0.#} km";
                }
                Instantiate(cardPrefab, listRoot).Bind(listing, home, distance, Open);
            }

            string noun = shown.Count == 1 ? "pet" : "pets";
            countLabel.text = AdoptionServices.Repository.IsDemo ? $"{shown.Count} {noun} · Demo data" : $"{shown.Count} {noun}";
            emptyState.SetActive(shown.Count == 0);
            if (home == null && shown.Count > 0) banner.ShowInfo("Tell us about your home to see how well each pet fits.");
        }

        void Open(AdoptionListing listing)
        {
            AppSession.SelectedListing = listing;
            manager.Push(ScreenId.AdoptionDetails);
        }
    }
}
