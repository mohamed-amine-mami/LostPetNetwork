using System;
using UnityEngine;

namespace LostPetNetwork.Services.Map
{
    /// <summary>
    /// Map settings read from the git-ignored Resources/MapConfig.local.json.
    /// Only Mapbox PUBLIC tokens (pk.…) are accepted; a secret token (sk.…) is refused.
    /// </summary>
    public static class MapConfig
    {
        [Serializable]
        class Data
        {
            public string mapboxAccessToken;
        }

        static string _token;
        static bool _loaded;

        public static bool IsConfigured => !string.IsNullOrEmpty(MapboxToken);

        public static string MapboxToken
        {
            get
            {
                if (!_loaded) Load();
                return _token;
            }
        }

        public static bool IsValidPublicToken(string token) =>
            !string.IsNullOrWhiteSpace(token) && token.StartsWith("pk.", StringComparison.Ordinal) && token.Length > 20;

        /// <summary>Test seam: replace or clear the loaded token.</summary>
        public static void SetForTests(string token)
        {
            _token = IsValidPublicToken(token) ? token : null;
            _loaded = true;
        }

        public static void Reload() => _loaded = false;

        static void Load()
        {
            _loaded = true;
            _token = null;
            var asset = Resources.Load<TextAsset>("MapConfig.local");
            if (asset == null) return;
            try
            {
                var data = JsonUtility.FromJson<Data>(asset.text);
                if (data != null && IsValidPublicToken(data.mapboxAccessToken)) _token = data.mapboxAccessToken.Trim();
            }
            catch (ArgumentException)
            {
                _token = null;
            }
        }
    }
}
