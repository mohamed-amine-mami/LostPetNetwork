using UnityEngine;

namespace LostPetNetwork.UI
{
    /// <summary>Design tokens copied from the approved Figma file (Warm &amp; Trustworthy).</summary>
    public static class Theme
    {
        public static readonly Color Cream = Hex("FAF5EA");
        public static readonly Color CreamDeep = Hex("F1E8D6");
        public static readonly Color Card = Hex("FFFDF8");
        public static readonly Color Border = Hex("E3D7BE");
        public static readonly Color Forest900 = Hex("163A2B");
        public static readonly Color Forest700 = Hex("1F4D3A");
        public static readonly Color Forest100 = Hex("DCEBE0");
        public static readonly Color Forest50 = Hex("EEF5EF");
        public static readonly Color Terracotta = Hex("B4492A");
        public static readonly Color TerracottaSoft = Hex("FBE3DA");
        public static readonly Color TerracottaInk = Hex("8F3418");
        public static readonly Color GoldSoft = Hex("FFF0C2");
        public static readonly Color GoldInk = Hex("6B4E00");
        public static readonly Color TextPrimary = Hex("25302A");
        public static readonly Color TextSecondary = Hex("56615B");
        public static readonly Color Placeholder = Hex("6B746E");
        public static readonly Color OnBrand = Hex("FFFDF8");
        public static readonly Color Error = Hex("B3261E");

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }
}
