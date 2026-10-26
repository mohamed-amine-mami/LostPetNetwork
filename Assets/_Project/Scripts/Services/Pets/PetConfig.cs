using System;
using UnityEngine;

namespace LostPetNetwork.Services.Pets
{
    /// <summary>
    /// The choices that define a pet's 3D look (small indexes only, so it can travel inside a report as JSON).
    /// </summary>
    [Serializable]
    public class PetConfig
    {
        public const int CurrentVersion = 1;
        public const int SpeciesCount = 2;   // 0 dog, 1 cat
        public const int CoatCount = 8;
        public const int PatternCount = 3;   // none, patches, white socks
        public const int SizeCount = 3;      // small, medium, large
        public const int EarCount = 2;       // 0 floppy, 1 pointed (dogs only; cats are always pointed)
        public const int CollarCount = 6;    // 0 none, 1..5 colours

        public int v = CurrentVersion;
        public int species;
        public int coat = 1;
        public int pattern;
        public int size = 1;
        public int ears;
        public int collar;

        public bool IsCat => species == 1;

        public static PetConfig Default() => new PetConfig();

        public static PetConfig Random(System.Random rng) => new PetConfig
        {
            species = rng.Next(SpeciesCount),
            coat = rng.Next(CoatCount),
            pattern = rng.Next(PatternCount),
            size = rng.Next(SizeCount),
            ears = rng.Next(EarCount),
            collar = rng.Next(CollarCount)
        };

        public PetConfig Clone() => (PetConfig)MemberwiseClone();

        public PetConfig Clamp()
        {
            v = CurrentVersion;
            species = Mathf.Clamp(species, 0, SpeciesCount - 1);
            coat = Mathf.Clamp(coat, 0, CoatCount - 1);
            pattern = Mathf.Clamp(pattern, 0, PatternCount - 1);
            size = Mathf.Clamp(size, 0, SizeCount - 1);
            ears = Mathf.Clamp(ears, 0, EarCount - 1);
            collar = Mathf.Clamp(collar, 0, CollarCount - 1);
            return this;
        }

        public bool SameAs(PetConfig o) =>
            o != null && species == o.species && coat == o.coat && pattern == o.pattern && size == o.size &&
            ears == o.ears && collar == o.collar;

        public string ToJson() => JsonUtility.ToJson(Clone().Clamp());

        public static bool TryParse(string json, out PetConfig config)
        {
            config = null;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                var parsed = JsonUtility.FromJson<PetConfig>(json);
                if (parsed == null) return false;
                config = parsed.Clamp();
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
