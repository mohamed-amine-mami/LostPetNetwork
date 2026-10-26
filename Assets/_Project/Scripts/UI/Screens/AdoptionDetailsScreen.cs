using System;
using LostPetNetwork.Services.Adoption;
using LostPetNetwork.Services.Auth;
using LostPetNetwork.Services.Pets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>One pet up for adoption: 3D view, facts, the compatibility breakdown and the next action.</summary>
    public class AdoptionDetailsScreen : MonoBehaviour
    {
        [SerializeField] ScreenManager manager;
        [SerializeField] Button backButton;
        [SerializeField] Preview3D preview;
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text summaryLabel;
        [SerializeField] TMP_Text energyValue;
        [SerializeField] TMP_Text locationValue;
        [SerializeField] TMP_Text ownerValue;
        [SerializeField] TMP_Text descriptionLabel;
        [SerializeField] GameObject matchCard;
        [SerializeField] TMP_Text scoreLabel;
        [SerializeField] TMP_Text verdictLabel;
        [SerializeField] TMP_Text[] factorTitles;
        [SerializeField] TMP_Text[] factorPoints;
        [SerializeField] TMP_Text[] factorNotes;
        [SerializeField] GameObject homePrompt;
        [SerializeField] Button setupHomeButton;
        [SerializeField] LoadingButton requestButton;
        [SerializeField] Button arButton;
        [SerializeField] TMP_Text hint;
        [SerializeField] MessageBanner banner;

        AdoptionListing _listing;
        bool _busy;

        public enum NextAction { None, Request, MarkAdopted }

        public static NextAction ActionFor(AdoptionListing listing, string userId)
        {
            if (listing == null || listing.Status == ListingStatus.Adopted) return NextAction.None;
            return listing.OwnerUserId == userId ? NextAction.MarkAdopted : NextAction.Request;
        }

        void Awake()
        {
            backButton.onClick.AddListener(() => { if (!manager.Back()) manager.Show(ScreenId.Adoption); });
            setupHomeButton.onClick.AddListener(() => manager.Push(ScreenId.AdopterProfile));
            requestButton.Button.onClick.AddListener(OnActionClicked);
            arButton.onClick.AddListener(OnArClicked);
            preview.DefaultYaw = PetFactory.DefaultYaw;
            preview.TiltToRotate = true;
        }

        void OnEnable()
        {
            _busy = false;
            _listing = AppSession.SelectedListing;
            if (_listing == null)
            {
                if (!manager.Back()) manager.Show(ScreenId.Adoption);
                return;
            }
            banner.Hide();
            Bind();
        }

        void Bind()
        {
            var l = _listing;
            if (!PetConfig.TryParse(l.PetAvatarJson, out var look)) look = new PetConfig { species = l.Species, size = l.Size };
            preview.ResetView();
            preview.SetContent(PetFactory.Build(look), PetFactory.FrameCenter, PetFactory.FrameRadius);

            nameLabel.text = l.Name;
            summaryLabel.text = l.Summary;
            energyValue.text = l.Energy == Level.Low ? "Calm" : l.Energy == Level.High ? "Very active" : "Moderate";
            locationValue.text = l.LocationText;
            ownerValue.text = l.OwnerName;
            descriptionLabel.text = string.IsNullOrWhiteSpace(l.Description) ? "No extra details were added." : l.Description;

            var home = AppSession.MyHome;
            bool own = l.OwnerUserId == AuthService.Instance.UserId;
            matchCard.SetActive(home != null && !own);
            homePrompt.SetActive(home == null && !own);
            if (home != null && !own)
            {
                var result = AdoptionCompatibility.Score(l, home);
                scoreLabel.text = $"{result.Score}%";
                verdictLabel.text = result.Verdict;
                for (int i = 0; i < factorTitles.Length && i < result.Factors.Count; i++)
                {
                    var f = result.Factors[i];
                    factorTitles[i].text = f.Label;
                    factorPoints[i].text = $"{f.Points}/{f.Max}";
                    factorNotes[i].text = f.Note;
                }
            }

            arButton.gameObject.SetActive(ArLauncher.CanTryAtHome);
            switch (ActionFor(l, AuthService.Instance.UserId))
            {
                case NextAction.Request:
                    requestButton.gameObject.SetActive(true);
                    requestButton.SetIdleText($"Request to adopt {l.Name}");
                    hint.text = AdoptionServices.Repository.IsDemo
                        ? "Demo: requests are only recorded on this device for now."
                        : "The owner will see your request and can contact you.";
                    break;
                case NextAction.MarkAdopted:
                    requestButton.gameObject.SetActive(true);
                    requestButton.SetIdleText("Mark as adopted");
                    hint.text = "This is your listing. Mark it once the pet has a new home.";
                    break;
                default:
                    requestButton.gameObject.SetActive(false);
                    hint.text = l.Status == ListingStatus.Adopted ? $"{l.Name} has found a home. Thank you!" : string.Empty;
                    break;
            }
        }

        void OnArClicked()
        {
            if (!ArLauncher.CanTryAtHome) { banner.ShowInfo("AR isn't available on this device."); return; }
            ArLauncher.OpenTryAtHome(_listing);
        }

        async void OnActionClicked()
        {
            if (_busy || _listing == null) return;
            _busy = true;
            requestButton.SetLoading(true);
            banner.Hide();
            try
            {
                switch (ActionFor(_listing, AuthService.Instance.UserId))
                {
                    case NextAction.MarkAdopted:
                        var updated = await AdoptionServices.Repository.MarkAdoptedAsync(_listing.Id);
                        if (this == null) return;
                        if (updated != null) { _listing = updated; AppSession.SelectedListing = updated; }
                        Bind();
                        banner.ShowInfo("Marked as adopted. Congratulations!");
                        break;
                    case NextAction.Request:
                        bool sent = await AdoptionServices.Repository.RequestAsync(_listing.Id, null);
                        if (this == null) return;
                        banner.ShowInfo(sent
                            ? (AdoptionServices.Repository.IsDemo ? "Request recorded on this device (demo)." : "Request sent. The owner can see it.")
                            : "You've already sent a request for this pet.");
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
                if (this != null) requestButton.SetLoading(false);
            }
        }
    }
}
