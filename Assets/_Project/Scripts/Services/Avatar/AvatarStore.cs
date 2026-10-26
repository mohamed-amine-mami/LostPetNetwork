using UnityEngine;

namespace LostPetNetwork.Services.Avatar
{
    public interface IAvatarStore
    {
        /// <summary>The saved avatar for this user, or null when none was created yet.</summary>
        AvatarConfig Load(string userId);
        void Save(string userId, AvatarConfig config);
        void Delete(string userId);
    }

    /// <summary>Keeps one avatar per user on this device (a few bytes of JSON, no secrets).</summary>
    public class PlayerPrefsAvatarStore : IAvatarStore
    {
        static string Key(string userId) => "lpn.avatar.v1." + (string.IsNullOrEmpty(userId) ? "anonymous" : userId);

        public AvatarConfig Load(string userId) =>
            AvatarConfig.TryParse(PlayerPrefs.GetString(Key(userId), string.Empty), out var config) ? config : null;

        public void Save(string userId, AvatarConfig config)
        {
            PlayerPrefs.SetString(Key(userId), config.ToJson());
            PlayerPrefs.Save();
        }

        public void Delete(string userId)
        {
            PlayerPrefs.DeleteKey(Key(userId));
            PlayerPrefs.Save();
        }
    }

    public static class AvatarServices
    {
        public static IAvatarStore Store { get; set; } = new PlayerPrefsAvatarStore();
    }
}
