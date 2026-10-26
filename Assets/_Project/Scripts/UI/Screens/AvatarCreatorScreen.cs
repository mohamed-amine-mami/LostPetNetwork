using LostPetNetwork.Services.Auth;
using LostPetNetwork.Services.Avatar;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>
    /// Lets the signed-in user design a 3D avatar: pick skin, hair, eyes, outfit and extras, turn the figure
    /// with a drag, then save it.
    /// </summary>
    public class AvatarCreatorScreen : MonoBehaviour
    {
        [SerializeField] ScreenManager manager;
        [SerializeField] Preview3D preview;
        [SerializeField] Button closeButton;
        [SerializeField] Button randomButton;
        [SerializeField] Button resetViewButton;
        [SerializeField] ChipGroup categories;
        [SerializeField] GameObject[] categoryPanels;
        [SerializeField] LoadingButton saveButton;
        [SerializeField] Button cancelButton;
        [SerializeField] MessageBanner banner;

        AvatarOptionRow[] _rows;
        AvatarConfig _config = AvatarConfig.Default();
        System.Random _random;

        public AvatarConfig Config => _config.Clone();

        void Awake()
        {
            _random = new System.Random();
            _rows = GetComponentsInChildren<AvatarOptionRow>(true);
            foreach (var row in _rows) row.Chosen += OnChosen;
            categories.Changed += ShowCategory;
            closeButton.onClick.AddListener(Leave);
            cancelButton.onClick.AddListener(Leave);
            randomButton.onClick.AddListener(Randomize);
            resetViewButton.onClick.AddListener(preview.ResetView);
            saveButton.Button.onClick.AddListener(Save);
        }

        void OnEnable()
        {
            banner.Hide();
            _config = AvatarServices.Store.Load(AuthService.Instance.UserId) ?? AvatarConfig.Default();
            preview.ResetView();
            categories.Select(0, false);
            ShowCategory(0);
            Apply();
        }

        /// <summary>Shows a given look in the creator (used by tests and by the Editor screenshot helper).</summary>
        public void SetConfig(AvatarConfig config)
        {
            _config = config.Clone().Clamp();
            Apply();
        }

        void OnChosen(AvatarProperty property, int index)
        {
            switch (property)
            {
                case AvatarProperty.Skin: _config.skin = index; break;
                case AvatarProperty.HairStyle: _config.hairStyle = index; break;
                case AvatarProperty.HairColor: _config.hairColor = index; break;
                case AvatarProperty.Eyes: _config.eyes = index; break;
                case AvatarProperty.OutfitStyle: _config.outfitStyle = index; break;
                case AvatarProperty.OutfitColor: _config.outfitColor = index; break;
                case AvatarProperty.Accessory: _config.accessory = index; break;
            }
            Rebuild();
        }

        void ShowCategory(int index)
        {
            for (int i = 0; i < categoryPanels.Length; i++) categoryPanels[i].SetActive(i == index);
        }

        void Randomize()
        {
            _config = AvatarConfig.Random(_random);
            Apply();
        }

        /// <summary>Shows the current settings on the option rows and the 3D figure.</summary>
        void Apply()
        {
            foreach (var row in _rows) row.SetSelected(ValueOf(row.Property));
            Rebuild();
        }

        int ValueOf(AvatarProperty property)
        {
            switch (property)
            {
                case AvatarProperty.Skin: return _config.skin;
                case AvatarProperty.HairStyle: return _config.hairStyle;
                case AvatarProperty.HairColor: return _config.hairColor;
                case AvatarProperty.Eyes: return _config.eyes;
                case AvatarProperty.OutfitStyle: return _config.outfitStyle;
                case AvatarProperty.OutfitColor: return _config.outfitColor;
                default: return _config.accessory;
            }
        }

        void Rebuild() => preview.SetContent(AvatarFactory.Build(_config), AvatarFactory.BodyCenter, AvatarFactory.BodyRadius);

        bool _saving;

        async void Save()
        {
            if (_saving) return;
            string userId = AuthService.Instance.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                banner.ShowError("Sign in to save your avatar.");
                return;
            }

            var config = _config.Clone().Clamp();
            AvatarServices.Store.Save(userId, config); // always kept on this device first
            _saving = true;
            saveButton.SetLoading(true);
            try
            {
                var sync = await AvatarSync.PushAsync(config);
                if (this == null) return;
                AppSession.FlashMessage = sync.Ok
                    ? "Avatar saved."
                    : "Avatar saved on this device. It couldn't be synced to your account: " + sync.Error;
                Leave();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                if (this != null) { AppSession.FlashMessage = "Avatar saved on this device."; Leave(); }
            }
            finally
            {
                _saving = false;
                if (this != null) saveButton.SetLoading(false);
            }
        }

        void Leave()
        {
            if (!manager.Back()) manager.Show(ScreenId.Profile);
        }
    }
}
