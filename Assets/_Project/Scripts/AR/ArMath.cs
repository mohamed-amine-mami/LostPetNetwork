using System;
using LostPetNetwork.Services.Pets;
using LostPetNetwork.UI;

namespace LostPetNetwork.AR
{
    /// <summary>Small calculations used by the AR scenes (pure logic, covered by tests).</summary>
    public static class ArMath
    {
        /// <summary>Radius in metres of a circle covering the given floor area.</summary>
        public static float RadiusForArea(float squareMetres) => (float)Math.Sqrt(Math.Max(0f, squareMetres) / Math.PI);

        /// <summary>Approximate real-world scale factor for a pet built by PetFactory (its chosen size is already part of the model).</summary>
        public static float PetScale(PetConfig pet) => PetFactory.LifeSizeScale(pet);

        /// <summary>Reports closer than this many metres get a 3D pet in the AR radar.</summary>
        public const float PetShowMeters = 30f;

        /// <summary>
        /// How far in front of the camera the 3D pet is drawn: its real distance, kept between 2 and 8 m so it is
        /// never inside the camera and never too small to see.
        /// </summary>
        public static float PetDrawDistance(double meters) => (float)Math.Max(2.0, Math.Min(8.0, meters));

        /// <summary>
        /// The compass-style yaw (degrees, 0 = world +Z, clockwise) of the way the camera faces. When the camera
        /// points almost straight up or down, the top of the phone gives the direction instead.
        /// </summary>
        public static float CameraYaw(UnityEngine.Transform camera)
        {
            var flat = UnityEngine.Vector3.ProjectOnPlane(camera.forward, UnityEngine.Vector3.up);
            if (flat.sqrMagnitude < 0.09f) flat = UnityEngine.Vector3.ProjectOnPlane(camera.up, UnityEngine.Vector3.up);
            return UnityEngine.Mathf.Atan2(flat.x, flat.z) * UnityEngine.Mathf.Rad2Deg;
        }

        /// <summary>The avatar is built about 2.2 units tall; this makes it a 1.7 m person.</summary>
        public const float AvatarScale = 0.77f;

        /// <summary>How far the user may shrink or enlarge the model, relative to its real size.</summary>
        public const float MinUserScale = 0.5f, MaxUserScale = 2f;

        public static float ClampUserScale(float scale) =>
            UnityEngine.Mathf.Clamp(float.IsNaN(scale) ? 1f : scale, MinUserScale, MaxUserScale);

        /// <summary>The new size multiplier after a pinch: the fingers' distance now against when the pinch began.</summary>
        public static float PinchScale(float startScale, float startDistance, float currentDistance) =>
            startDistance < 1f ? startScale : ClampUserScale(startScale * currentDistance / startDistance);

        /// <summary>Degrees the fingers have twisted since the gesture began; clockwise on the screen is positive.</summary>
        public static float TwistDegrees(UnityEngine.Vector2 startVector, UnityEngine.Vector2 currentVector) =>
            startVector.sqrMagnitude < 1f || currentVector.sqrMagnitude < 1f ? 0f : -UnityEngine.Vector2.SignedAngle(startVector, currentVector);

        /// <summary>A detected surface is worth placing on when it is at least 30 cm across both ways.</summary>
        public static bool PlaneBigEnough(UnityEngine.Vector2 size) => size.x >= 0.3f && size.y >= 0.3f;

        /// <summary>The text under the model: its height, whether it was resized, and the space a pet needs.</summary>
        public static string PlacementInfo(bool avatar, float realHeightMetres, float userScale, float spaceM2)
        {
            int cm = ToCentimetres(realHeightMetres * userScale);
            bool resized = UnityEngine.Mathf.Abs(userScale - 1f) > 0.02f;
            string size = resized ? $"About {cm} cm tall (resized to {UnityEngine.Mathf.RoundToInt(userScale * 100f)}%)" : $"About {cm} cm tall";
            if (avatar) return size + ".";
            string space = spaceM2 > 0 ? $" The ring shows roughly {spaceM2} m² of space." : "";
            return size + (resized ? "." : " (approximate).") + space;
        }

        /// <summary>Whole centimetres, for a readable "about 55 cm tall".</summary>
        public static int ToCentimetres(float metres) => (int)Math.Round(metres * 100f);
    }
}
