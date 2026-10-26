using System;
using UnityEngine;

namespace LostPetNetwork.Services.Avatar
{
    /// <summary>
    /// The choices that define a user's 3D avatar. Only small indexes are stored (never meshes or images),
    /// so the same JSON can be saved locally, in the profile row or rendered on any device.
    /// </summary>
    [Serializable]
    public class AvatarConfig
    {
        public const int CurrentVersion = 1;
        public const int SkinCount = 6;
        public const int HairStyleCount = 5;
        public const int HairColorCount = 7;
        public const int EyeCount = 4;
        public const int OutfitStyleCount = 3;
        public const int OutfitColorCount = 7;
        public const int AccessoryCount = 5;

        public int v = CurrentVersion;
        public int skin = 2;
        public int hairStyle = 1;
        public int hairColor = 1;
        public int eyes;
        public int outfitStyle;
        public int outfitColor = 2;
        public int accessory;

        public static AvatarConfig Default() => new AvatarConfig();

        public static AvatarConfig Random(System.Random rng) => new AvatarConfig
        {
            skin = rng.Next(SkinCount),
            hairStyle = rng.Next(HairStyleCount),
            hairColor = rng.Next(HairColorCount),
            eyes = rng.Next(EyeCount),
            outfitStyle = rng.Next(OutfitStyleCount),
            outfitColor = rng.Next(OutfitColorCount),
            accessory = rng.Next(AccessoryCount)
        };

        public AvatarConfig Clone() => (AvatarConfig)MemberwiseClone();

        /// <summary>Forces every field into its valid range (old or tampered data can never break rendering).</summary>
        public AvatarConfig Clamp()
        {
            v = CurrentVersion;
            skin = Mathf.Clamp(skin, 0, SkinCount - 1);
            hairStyle = Mathf.Clamp(hairStyle, 0, HairStyleCount - 1);
            hairColor = Mathf.Clamp(hairColor, 0, HairColorCount - 1);
            eyes = Mathf.Clamp(eyes, 0, EyeCount - 1);
            outfitStyle = Mathf.Clamp(outfitStyle, 0, OutfitStyleCount - 1);
            outfitColor = Mathf.Clamp(outfitColor, 0, OutfitColorCount - 1);
            accessory = Mathf.Clamp(accessory, 0, AccessoryCount - 1);
            return this;
        }

        public bool SameAs(AvatarConfig other) =>
            other != null && skin == other.skin && hairStyle == other.hairStyle && hairColor == other.hairColor &&
            eyes == other.eyes && outfitStyle == other.outfitStyle && outfitColor == other.outfitColor &&
            accessory == other.accessory;

        public string ToJson() => JsonUtility.ToJson(Clone().Clamp());

        public static bool TryParse(string json, out AvatarConfig config)
        {
            config = null;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                var parsed = JsonUtility.FromJson<AvatarConfig>(json);
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
