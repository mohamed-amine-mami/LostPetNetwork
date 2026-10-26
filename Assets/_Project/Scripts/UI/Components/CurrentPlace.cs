using LostPetNetwork.Services.Map;
using LostPetNetwork.Services.Sensors;
using UnityEngine;

namespace LostPetNetwork.UI
{
    /// <summary>Finds the phone's position and, when possible, a readable place name for it.</summary>
    public static class CurrentPlace
    {
        public readonly struct Result
        {
            public readonly bool Ok;
            public readonly GeoFix Fix;
            public readonly string Label;
            public readonly string Error;

            public Result(bool ok, GeoFix fix, string label, string error)
            {
                Ok = ok; Fix = fix; Label = label; Error = error;
            }
        }

        /// <summary>Waits for the phone's position only (no network lookup of the place name).</summary>
        public static async Awaitable<Result> FindFixAsync()
        {
            var provider = SensorHub.Location;
            if (!provider.IsAvailable)
                return new Result(false, default, null, "Location isn't available on this device. Type the place instead.");

            provider.StartTracking();
            for (int i = 0; i < 40 && !provider.Latest.HasValue; i++) await Awaitable.WaitForSecondsAsync(0.25f);
            var fix = provider.Latest;
            if (!fix.HasValue)
                return new Result(false, default, null, "Couldn't get your location. Check that location is on and allowed for this app.");

            return new Result(true, fix.Value, null, null);
        }

        /// <summary>A readable name for a position (street or area), or its coordinates when none is found.</summary>
        public static async Awaitable<string> NameAsync(GeoFix fix)
        {
            string place = await MapboxApi.ReverseGeocodeAsync(fix.Latitude, fix.Longitude);
            return string.IsNullOrEmpty(place) ? $"Near {fix.Latitude:0.0000}, {fix.Longitude:0.0000}" : place;
        }

        /// <summary>Position plus place name.</summary>
        public static async Awaitable<Result> FindAsync()
        {
            var found = await FindFixAsync();
            if (!found.Ok) return found;
            return new Result(true, found.Fix, await NameAsync(found.Fix), null);
        }
    }
}
