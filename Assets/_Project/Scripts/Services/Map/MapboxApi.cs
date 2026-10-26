using System;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace LostPetNetwork.Services.Map
{
    /// <summary>Builds Mapbox request URLs and reads their answers. The token is only ever put in the request URL.</summary>
    public static class MapboxApi
    {
        public const string DefaultStyle = "mapbox/streets-v12";

        [Serializable]
        class GeocodeResponse
        {
            public GeocodeFeature[] features;
        }

        [Serializable]
        class GeocodeFeature
        {
            public string place_name;
            public string text;
        }

        public static string TileUrl(string token, int zoom, int x, int y, string style = DefaultStyle) =>
            $"https://api.mapbox.com/styles/v1/{style}/tiles/256/{zoom}/{x}/{y}@2x?access_token={Uri.EscapeDataString(token)}";

        public static string ReverseGeocodeUrl(string token, double lat, double lon) =>
            "https://api.mapbox.com/geocoding/v5/mapbox.places/" +
            $"{lon.ToString("0.#####", CultureInfo.InvariantCulture)},{lat.ToString("0.#####", CultureInfo.InvariantCulture)}.json" +
            $"?types=address,poi,neighborhood,locality,place&limit=1&language=en&access_token={Uri.EscapeDataString(token)}";

        /// <summary>The readable place name from a geocoding answer, or null when there is none.</summary>
        public static string ParsePlaceName(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var response = JsonUtility.FromJson<GeocodeResponse>(json);
                if (response?.features == null || response.features.Length == 0) return null;
                string name = response.features[0].place_name;
                return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>Turns coordinates into a place name (for example "Bab El Khadhra, Tunis, Tunisia"); null if unavailable.</summary>
        public static Task<string> ReverseGeocodeAsync(double lat, double lon)
        {
            var tcs = new TaskCompletionSource<string>();
            if (!MapConfig.IsConfigured)
            {
                tcs.SetResult(null);
                return tcs.Task;
            }

            var request = UnityWebRequest.Get(ReverseGeocodeUrl(MapConfig.MapboxToken, lat, lon));
            request.timeout = 8;
            var op = request.SendWebRequest();
            op.completed += _ =>
            {
                string result = request.result == UnityWebRequest.Result.Success
                    ? ParsePlaceName(request.downloadHandler.text)
                    : null;
                request.Dispose();
                tcs.TrySetResult(result);
            };
            return tcs.Task;
        }
    }
}
