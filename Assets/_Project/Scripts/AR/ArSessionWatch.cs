using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace LostPetNetwork.AR
{
    /// <summary>
    /// Notices a phone that cannot run AR (or a user who refused camera access): the session reports
    /// "unsupported" and the scene says so and closes instead of showing a black screen.
    /// </summary>
    public static class ArSessionWatch
    {
        public const string Message = "AR isn't available on this phone, or camera access was denied.";

        const float GraceSeconds = 4f;

        /// <summary>True once the session has stayed unsupported for a few seconds (never in the Editor simulation).</summary>
        public static bool Failed(ref float firstSeen)
        {
            if (Application.isEditor) return false;
            if (ARSession.state != ARSessionState.Unsupported) { firstSeen = 0f; return false; }
            if (firstSeen == 0f) firstSeen = Time.unscaledTime;
            return Time.unscaledTime - firstSeen > GraceSeconds;
        }
    }
}
