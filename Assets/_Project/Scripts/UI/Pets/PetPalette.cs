using UnityEngine;

namespace LostPetNetwork.UI
{
    public static class PetPalette
    {
        public static readonly Color[] Coat =
        {
            Hex("F1E2C2"), Hex("D9A441"), Hex("7A4B2A"), Hex("2B2724"), Hex("8C8F94"), Hex("F5F2EA"), Hex("D9772B"), Hex("A9835A")
        };

        /// <summary>Collar colours; index 0 means "no collar" and is never drawn.</summary>
        public static readonly Color[] Collar =
        {
            Color.clear, Hex("B4492A"), Hex("3B6EA5"), Hex("2F7A55"), Hex("E0A82E"), Hex("7A5C9E")
        };

        public static string[] SpeciesNames => new[] { "Dog", "Cat" };
        public static string[] PatternNames => new[] { "Plain", "Patches", "Socks" };
        public static string[] SizeNames => new[] { "Small", "Medium", "Large" };
        public static string[] EarNames => new[] { "Floppy", "Pointed" };

        /// <summary>Real-world scale of each size, used by the 3D preview and the AR "try before you adopt" scene.</summary>
        public static readonly float[] SizeScale = { 0.75f, 1f, 1.3f };

        /// <summary>A second colour for patches: darker for light coats, lighter for dark coats.</summary>
        public static Color Secondary(Color coat) =>
            coat.grayscale > 0.5f ? Color.Lerp(coat, new Color(0.25f, 0.15f, 0.08f), 0.55f) : Color.Lerp(coat, Color.white, 0.45f);

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }
}
