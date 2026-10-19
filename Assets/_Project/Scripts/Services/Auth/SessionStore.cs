using UnityEngine;

namespace LostPetNetwork.Services.Auth
{
    /// <summary>
    /// Persists the auth session between app launches.
    /// SECURITY NOTE: PlayerPrefs is not encrypted. Before public release, move this to the
    /// iOS Keychain / Android Keystore via a native plugin. Only tokens are stored, never passwords.
    /// </summary>
    public static class SessionStore
    {
        const string Key = "lpn.auth.session.v1";

        public static AuthSession Load()
        {
            string json = PlayerPrefs.GetString(Key, string.Empty);
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var session = JsonUtility.FromJson<AuthSession>(json);
                return session != null && session.IsValid ? session : null;
            }
            catch
            {
                return null;
            }
        }

        public static void Save(AuthSession session)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(session));
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
