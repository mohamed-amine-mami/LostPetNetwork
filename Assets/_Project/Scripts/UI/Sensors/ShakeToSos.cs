using LostPetNetwork.Services.Sensors;
using UnityEngine;
using LostPetNetwork.Services.Auth;

namespace LostPetNetwork.UI
{
    /// <summary>
    /// Opens the SOS form when the phone is shaken. It only opens the form: nothing is sent until the user
    /// confirms, so an accidental shake cannot raise a false alarm.
    /// </summary>
    public class ShakeToSos : MonoBehaviour
    {
        ScreenManager _manager;
        readonly ShakeDetector _detector = new ShakeDetector();

        public static ShakeToSos Attach(ScreenManager manager)
        {
            var existing = manager.GetComponent<ShakeToSos>();
            if (existing != null) return existing;
            var component = manager.gameObject.AddComponent<ShakeToSos>();
            component._manager = manager;
            return component;
        }

        void OnEnable() => SensorHub.Motion.StartTracking();

        void OnDisable() => SensorHub.Motion.StopTracking();

        void Update()
        {
            var motion = SensorHub.Motion;
            if (!motion.IsAvailable) return;
            if (_detector.Feed(motion.Acceleration, Time.unscaledTime)) OnShake();
        }

        /// <summary>Called when a shake is recognised (public so tests and the Editor can trigger it).</summary>
        public void OnShake()
        {
            if (_manager == null || ScreenManager.InputBlocked || !AuthService.Instance.IsSignedIn) return;
            switch (_manager.Current)
            {
                case ScreenId.Splash: case ScreenId.SignIn: case ScreenId.SignUp: case ScreenId.ResetPassword:
                case ScreenId.Sos: case ScreenId.SosCreate: case ScreenId.SosDetails:
                case ScreenId.Camera: case ScreenId.AvatarCreator: case ScreenId.PetCreator:
                    return;
            }
            Handheld.Vibrate();
            _manager.Push(ScreenId.SosCreate);
        }
    }
}
