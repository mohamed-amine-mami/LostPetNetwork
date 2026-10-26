using System;
using LostPetNetwork.Services.Adoption;
using LostPetNetwork.Services.Pets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>A card in the adoption list: the pet's 3D picture, name, facts and (when known) the match score.</summary>
    [RequireComponent(typeof(Button))]
    public class AdoptionCardView : MonoBehaviour
    {
        [SerializeField] Image portrait;
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text detailsLabel;
        [SerializeField] TMP_Text locationLabel;
        [SerializeField] GameObject matchPill;
        [SerializeField] TMP_Text matchLabel;
        [SerializeField] GameObject demoTag;

        public AdoptionListing Listing { get; private set; }

        public void Bind(AdoptionListing listing, AdopterProfile home, string distance, Action<AdoptionListing> onClick)
        {
            Listing = listing;
            nameLabel.text = listing.Name;
            detailsLabel.text = listing.Summary;
            string place = string.IsNullOrEmpty(distance) ? listing.LocationText : $"{listing.LocationText} · {distance}";
            locationLabel.text = place;
            if (demoTag != null) demoTag.SetActive(listing.IsDemo);

            matchPill.SetActive(home != null);
            if (home != null)
            {
                var result = AdoptionCompatibility.Score(listing, home);
                matchLabel.text = $"{result.Score}% match";
            }

            var button = GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke(listing));
            ShowPortrait(listing);
        }

        async void ShowPortrait(AdoptionListing listing)
        {
            portrait.enabled = false;
            if (!PetConfig.TryParse(listing.PetAvatarJson, out var config))
                config = new PetConfig { species = listing.Species, size = listing.Size };
            try
            {
                var sprite = await PetPortrait.RenderAsync(config);
                if (this == null || Listing != listing) return;
                portrait.sprite = sprite;
                portrait.enabled = sprite != null;
            }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
