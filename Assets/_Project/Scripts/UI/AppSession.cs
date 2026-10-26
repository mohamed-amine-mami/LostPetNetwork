using LostPetNetwork.Services.Adoption;
using LostPetNetwork.Services.Reports;
using LostPetNetwork.Services.Sos;

namespace LostPetNetwork.UI
{
    /// <summary>Which form the 3D pet creator is filling in.</summary>
    public enum PetLookTarget { Report, Adoption }

    /// <summary>Small in-memory state shared between screens (selection, form draft, one-shot messages).</summary>
    public static class AppSession
    {
        public static PetReport SelectedReport;
        public static ReportDraft Draft = new ReportDraft();
        public static AdoptionDraft AdoptionDraft = new AdoptionDraft();
        public static AdoptionListing SelectedListing;
        public static SosAlert SelectedAlert;
        /// <summary>The signed-in user's home profile once loaded (null: not set up or not loaded yet).</summary>
        public static AdopterProfile MyHome;
        public static bool MyHomeLoaded;
        public static PetLookTarget PetLookTarget;

        public static string PetLookJson
        {
            get => PetLookTarget == PetLookTarget.Adoption ? AdoptionDraft.PetAvatarJson : Draft.PetAvatarJson;
            set
            {
                if (PetLookTarget == PetLookTarget.Adoption) AdoptionDraft.PetAvatarJson = value;
                else Draft.PetAvatarJson = value;
            }
        }

        /// <summary>One-shot message shown by the next screen that supports it (e.g. after posting a report).</summary>
        public static string FlashMessage;

        public static void StartDraft(ReportType type)
        {
            Draft = new ReportDraft { Type = type };
        }

        public static string ConsumeFlash()
        {
            string m = FlashMessage;
            FlashMessage = null;
            return m;
        }

        public static void Reset()
        {
            SelectedReport = null;
            Draft = new ReportDraft();
            AdoptionDraft = new AdoptionDraft();
            SelectedListing = null;
            SelectedAlert = null;
            MyHome = null;
            MyHomeLoaded = false;
            PetLookTarget = PetLookTarget.Report;
            FlashMessage = null;
        }
    }
}
