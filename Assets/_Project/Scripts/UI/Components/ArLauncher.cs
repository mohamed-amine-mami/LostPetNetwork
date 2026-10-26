using System.Collections.Generic;
using LostPetNetwork.Services.Adoption;
using LostPetNetwork.Services.Avatar;
using LostPetNetwork.Services.Pets;
using LostPetNetwork.Services.Reports;
using LostPetNetwork.Services.Sos;

namespace LostPetNetwork.UI
{
    public enum ArMode { PlacePet, PlaceAvatar, Radar, Guide }

    /// <summary>What the AR scene should show. Set by the screen that opens AR, read by the scene.</summary>
    public static class ArRequest
    {
        public static ArMode Mode;
        public static PetConfig Pet;
        public static string PetName;
        public static int RecommendedSpaceM2;
        public static AvatarConfig Avatar;
        public static SosAlert Alert;
        public static List<PetReport> Reports = new List<PetReport>();
        /// <summary>The report the user tapped in the radar; the app opens its details after AR closes.</summary>
        public static PetReport OpenAfterClose;
    }

    /// <summary>Implemented by the optional AR module; the app works without it.</summary>
    public interface IArService
    {
        /// <summary>True when this phone can run AR sessions (ARCore / ARKit available).</summary>
        bool IsSupported { get; }
        void Open(ArMode mode, System.Action<PetReport> onClosed);
    }

    /// <summary>Opens the AR scenes. Buttons for AR only appear when the AR module is present and the phone supports it.</summary>
    public static class ArLauncher
    {
        public static IArService Service { get; set; }

        static bool Supported => Service != null && Service.IsSupported;

        public static bool CanTryAtHome => Supported;
        public static bool CanGuideToSos => Supported;
        public static bool CanShowRadar => Supported;
        public static bool CanPlaceAvatar => Supported;

        public static void OpenTryAtHome(AdoptionListing listing)
        {
            if (!Supported || listing == null) return;
            ArRequest.Mode = ArMode.PlacePet;
            ArRequest.PetName = listing.Name;
            ArRequest.RecommendedSpaceM2 = AdoptionCompatibility.RecommendedSpaceM2(listing);
            ArRequest.Pet = PetConfig.TryParse(listing.PetAvatarJson, out var look)
                ? look
                : new PetConfig { species = listing.Species, size = listing.Size };
            Service.Open(ArMode.PlacePet, null);
        }

        public static void OpenAvatar(AvatarConfig avatar)
        {
            if (!Supported || avatar == null) return;
            ArRequest.Mode = ArMode.PlaceAvatar;
            ArRequest.Avatar = avatar;
            Service.Open(ArMode.PlaceAvatar, null);
        }

        public static void OpenRadar(IEnumerable<PetReport> reports, System.Action<PetReport> onClosed)
        {
            if (!Supported) return;
            ArRequest.Mode = ArMode.Radar;
            ArRequest.Reports = new List<PetReport>(reports);
            ArRequest.OpenAfterClose = null;
            Service.Open(ArMode.Radar, onClosed);
        }

        public static void OpenSosGuide(SosAlert alert)
        {
            if (!Supported || alert == null) return;
            ArRequest.Mode = ArMode.Guide;
            ArRequest.Alert = alert;
            Service.Open(ArMode.Guide, null);
        }
    }
}
