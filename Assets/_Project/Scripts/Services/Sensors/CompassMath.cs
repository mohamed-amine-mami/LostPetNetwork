using System;
using UnityEngine;

namespace LostPetNetwork.Services.Sensors
{
    /// <summary>Maths for the compass overlays: where a target at a given bearing appears on a camera view.</summary>
    public static class CompassMath
    {
        /// <summary>
        /// Compass heading (degrees clockwise from magnetic north, 0 to 360) of the direction the phone's back camera
        /// faces, from Android's rotation-vector sensor. Returns null when the camera points straight up or down,
        /// where the heading is undefined.
        /// </summary>
        public static float? HeadingFromRotationVector(float x, float y, float z, float w)
        {
            // Rotation matrix entries that map the phone's axes to east, north, up (Android's convention).
            float r2 = 2f * (x * z + y * w);
            float r5 = 2f * (y * z - x * w);
            float east = -r2, north = -r5; // the back camera looks along the phone's -Z axis
            if (east * east + north * north < 0.01f) return null;
            float degrees = Mathf.Atan2(east, north) * Mathf.Rad2Deg;
            return (degrees + 360f) % 360f;
        }

        /// <summary>True when a target at this relative bearing (degrees, right is positive) is inside the camera's horizontal view.</summary>
        public static bool InView(double relativeBearing, double horizontalFovDegrees) =>
            Math.Abs(relativeBearing) <= horizontalFovDegrees / 2.0;

        /// <summary>
        /// Horizontal screen position of a target, 0 = left edge, 1 = right edge. Targets outside the view are
        /// pinned to the nearest edge, so they can be drawn as "turn this way" markers.
        /// </summary>
        public static float ScreenFraction(double relativeBearing, double horizontalFovDegrees)
        {
            double half = horizontalFovDegrees / 2.0;
            if (relativeBearing >= half) return 1f;
            if (relativeBearing <= -half) return 0f;
            double fraction = 0.5 + Math.Tan(relativeBearing * Math.PI / 180.0) / (2.0 * Math.Tan(half * Math.PI / 180.0));
            return (float)Math.Max(0.0, Math.Min(1.0, fraction));
        }

        /// <summary>Rotation (degrees, counter-clockwise as Unity UI measures it) that makes an up-arrow point toward the target.</summary>
        public static float ArrowRotation(double relativeBearing) => (float)-relativeBearing;

        public static string DistanceText(double meters)
        {
            if (meters < 10) return $"{(int)Math.Max(0, meters)} m";
            if (meters < 1000) return $"{(int)Math.Round(meters / 10.0) * 10} m";
            return $"{meters / 1000.0:0.#} km";
        }

        /// <summary>True when the target is close enough to count as "arrived".</summary>
        public static bool HasArrived(double meters, double radiusMeters = 20.0) => meters <= radiusMeters;
    }
}
