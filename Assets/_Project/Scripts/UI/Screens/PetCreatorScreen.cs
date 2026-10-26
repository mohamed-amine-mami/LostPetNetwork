using LostPetNetwork.Services.Pets;
using LostPetNetwork.Services.Reports;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>
    /// Designs the 3D look of the pet being reported (dog or cat, coat, pattern, size, ears, collar).
    /// The result is kept in the report draft and shown on the report's details page.
    /// </summary>
    public class PetCreatorScreen : MonoBehaviour
    {
        [SerializeField] ScreenManager manager;
        [SerializeField] Preview3D preview;
        [SerializeField] Button closeButton;
        [SerializeField] Button randomButton;
        [SerializeField] Button resetViewButton;
        [SerializeField] ChipGroup categories;
        [SerializeField] GameObject[] categoryPanels;
        [SerializeField] LoadingButton saveButton;
        [SerializeField] Button removeButton;
        [SerializeField] GameObject earsRow;

        AvatarOptionRow[] _rows;
        PetConfig _config = PetConfig.Default();
        System.Random _random;

        public PetConfig Config => _config.Clone();

        void Awake()
        {
            _random = new System.Random();
            _rows = GetComponentsInChildren<AvatarOptionRow>(true);
            foreach (var row in _rows) row.Chosen += OnChosen;
            categories.Changed += ShowCategory;
            closeButton.onClick.AddListener(Leave);
            randomButton.onClick.AddListener(() => { _config = PetConfig.Random(_random); Apply(); });
            resetViewButton.onClick.AddListener(preview.ResetView);
            saveButton.Button.onClick.AddListener(Save);
            removeButton.onClick.AddListener(Remove);
            preview.DefaultYaw = PetFactory.DefaultYaw;
            preview.TiltToRotate = true;
        }

        void OnEnable()
        {
            bool forAdoption = AppSession.PetLookTarget == PetLookTarget.Adoption;
            if (!PetConfig.TryParse(AppSession.PetLookJson, out _config))
                _config = new PetConfig { species = forAdoption ? AppSession.AdoptionDraft.Species : AppSession.Draft.Species == PetSpecies.Cat ? 1 : 0 };
            removeButton.gameObject.SetActive(!string.IsNullOrEmpty(AppSession.PetLookJson));
            preview.ResetView();
            categories.Select(0, false);
            ShowCategory(0);
            Apply();
        }

        /// <summary>Shows a given look in the creator (tests and the Editor screenshot helper).</summary>
        public void SetConfig(PetConfig config)
        {
            _config = config.Clone().Clamp();
            Apply();
        }

        void OnChosen(AvatarProperty property, int index)
        {
            switch (property)
            {
                case AvatarProperty.PetSpecies: _config.species = index; break;
                case AvatarProperty.PetCoat: _config.coat = index; break;
                case AvatarProperty.PetPattern: _config.pattern = index; break;
                case AvatarProperty.PetSize: _config.size = index; break;
                case AvatarProperty.PetEars: _config.ears = index; break;
                case AvatarProperty.PetCollar: _config.collar = index; break;
            }
            Apply();
        }

        void ShowCategory(int index)
        {
            for (int i = 0; i < categoryPanels.Length; i++) categoryPanels[i].SetActive(i == index);
        }

        void Apply()
        {
            foreach (var row in _rows) row.SetSelected(ValueOf(row.Property));
            if (earsRow != null) earsRow.SetActive(!_config.IsCat); // cats always have pointed ears
            preview.SetContent(PetFactory.Build(_config), PetFactory.FrameCenter, PetFactory.FrameRadius);
        }

        int ValueOf(AvatarProperty property)
        {
            switch (property)
            {
                case AvatarProperty.PetSpecies: return _config.species;
                case AvatarProperty.PetCoat: return _config.coat;
                case AvatarProperty.PetPattern: return _config.pattern;
                case AvatarProperty.PetSize: return _config.size;
                case AvatarProperty.PetEars: return _config.ears;
                case AvatarProperty.PetCollar: return _config.collar;
                default: return 0;
            }
        }

        void Save()
        {
            AppSession.PetLookJson = _config.ToJson();
            Leave();
        }

        void Remove()
        {
            AppSession.PetLookJson = null;
            Leave();
        }

        void Leave()
        {
            if (!manager.Back()) manager.Show(AppSession.PetLookTarget == PetLookTarget.Adoption ? ScreenId.AdoptionCreate : ScreenId.Report);
        }
    }
}
