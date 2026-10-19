using LostPetNetwork.Services.Reports;

namespace LostPetNetwork.UI
{
    /// <summary>Small in-memory state shared between screens (selection, form draft, one-shot messages).</summary>
    public static class AppSession
    {
        public static PetReport SelectedReport;
        public static ReportDraft Draft = new ReportDraft();

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
            FlashMessage = null;
        }
    }
}
