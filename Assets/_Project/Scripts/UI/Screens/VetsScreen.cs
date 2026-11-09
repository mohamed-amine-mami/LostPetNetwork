using System;
using System.Collections.Generic;
using LostPetNetwork.Services.Sensors;
using LostPetNetwork.Services.Vets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>Veterinary clinics near the user, nearest first, with one tap to call or get directions.</summary>
    public class VetsScreen : MonoBehaviour
    {
        const int MaxShown = 30;

        [SerializeField] ScreenManager manager;
        [SerializeField] Button backButton;
        [SerializeField] ChipGroup chips;
        [SerializeField] TMP_Text countLabel;
        [SerializeField] TMP_Text locationNote;
        [SerializeField] Transform listRoot;
        [SerializeField] VetRowView rowPrefab;
        [SerializeField] GameObject loading;
        [SerializeField] GameObject emptyState;
        [SerializeField] TMP_Text emptyLabel;
        [SerializeField] MessageBanner banner;

        IReadOnlyList<VetClinic> _all = Array.Empty<VetClinic>();
        bool _visible;
        int _loadId;

        void Awake()
        {
            backButton.onClick.AddListener(() => { if (!manager.Back()) manager.Show(ScreenId.Sos); });
            chips.Changed += _ => Render();
        }

        void OnEnable()
        {
            _visible = true;
            banner.Hide();
            SensorHub.Location.StartTracking();
            Load(++_loadId);
        }

        void OnDisable()
        {
            _visible = false;
            SensorHub.Location.StopTracking();
        }

        /// <summary>The directory is loaded once per visit; the position can arrive later, so the list is sorted again as it does.</summary>
        async void Load(int id)
        {
            loading.SetActive(true);
            emptyState.SetActive(false);
            try
            {
                _all = await VetServices.Repository.GetAllAsync();
                if (this == null || !_visible || id != _loadId) return;
                Render();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this != null) banner.ShowError(UiUtil.Describe(e, "Couldn't load the vets. Please try again."));
            }
            finally
            {
                if (this != null) loading.SetActive(false);
            }

            // Give the GPS a few seconds to find the user, then sort by distance.
            for (int i = 0; i < 10 && this != null && _visible && id == _loadId; i++)
            {
                if (SensorHub.Location.Latest.HasValue) { Render(); return; }
                await Awaitable.WaitForSecondsAsync(1f);
            }
        }

        void Render()
        {
            UiUtil.ClearChildren(listRoot);
            var me = SensorHub.Location.Latest;
            bool only24h = chips.SelectedIndex == 1;
            var shown = VetFinder.Nearest(_all, me, only24h, MaxShown);
            foreach (var vet in shown)
                Instantiate(rowPrefab, listRoot).Bind(vet, Call, Directions);

            string noun = shown.Count == 1 ? "clinic" : "clinics";
            string demo = VetServices.Repository.IsDemo ? " · Demo data" : "";
            countLabel.text = $"{shown.Count} {noun}{demo}";
            locationNote.gameObject.SetActive(!me.HasValue && shown.Count > 0);
            emptyLabel.text = only24h ? "No clinic open 24 hours is listed yet." : "No vets are listed yet.";
            emptyState.SetActive(shown.Count == 0);
        }

        void Call(VetClinic clinic)
        {
            string url = clinic.DialUrl();
            if (url == null) { banner.ShowError("This clinic has no phone number."); return; }
            Application.OpenURL(url);
        }

        void Directions(VetClinic clinic) => Application.OpenURL(clinic.DirectionsUrl());
    }
}
