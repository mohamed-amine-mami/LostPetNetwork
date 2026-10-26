using LostPetNetwork.Services.Avatar;
using UnityEngine;

namespace LostPetNetwork.UI
{
    /// <summary>Colours behind the avatar's option indexes. Order must match the creator's swatch order.</summary>
    public static class AvatarPalette
    {
        public static readonly Color[] Skin =
        {
            Hex("F6D5BC"), Hex("EBBE97"), Hex("D69E72"), Hex("B97A50"), Hex("8A5636"), Hex("5C3A26")
        };

        public static readonly Color[] Hair =
        {
            Hex("2B1B12"), Hex("5A3A22"), Hex("9A6A3A"), Hex("D8B15A"), Hex("B4492A"), Hex("8E8E92"), Hex("4F7CAC")
        };

        public static readonly Color[] Eyes =
        {
            Hex("4A2E1B"), Hex("2F7A55"), Hex("3B6EA5"), Hex("6B746E")
        };

        public static readonly Color[] Outfit =
        {
            Hex("1F4D3A"), Hex("B4492A"), Hex("E0A82E"), Hex("3B6EA5"), Hex("E8DCC0"), Hex("7A5C9E"), Hex("3A403C")
        };

        public static readonly Color Pants = Hex("3A403C");
        public static readonly Color Shoes = Hex("E8DCC0");
        public static readonly Color Dark = Hex("2A2A2E");

        public static string[] HairStyleNames => new[] { "Bald", "Short", "Long", "Bun", "Curly" };
        public static string[] OutfitStyleNames => new[] { "T-shirt", "Hoodie", "Dress" };
        public static string[] AccessoryNames => new[] { "None", "Glasses", "Cap", "Scarf", "Headphones" };

        public static Color Colour(Color[] palette, int index) => palette[Mathf.Clamp(index, 0, palette.Length - 1)];

        /// <summary>A trim colour that stays readable against the chosen outfit.</summary>
        public static Color Trim(int outfitColor) => outfitColor == 1 ? Outfit[0] : Outfit[1];

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }
}
