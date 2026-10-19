namespace LostPetNetwork.Services.Supabase
{
    /// <summary>
    /// Public connection details for the LostPetNetwork Supabase project.
    /// The publishable key is designed to ship inside client apps; data is protected by
    /// Row Level Security. NEVER put the service_role / secret key in this project.
    /// </summary>
    public static class SupabaseConfig
    {
        public const string Url = "https://oipjwpraaapwduhpyrcs.supabase.co";
        public const string PublishableKey = "sb_publishable_Xp9KYfJTGxd4tzMQ19nP-Q_4kkk_qG2";

        public static string AuthUrl => Url + "/auth/v1";
        public static string RestUrl => Url + "/rest/v1";
    }
}
