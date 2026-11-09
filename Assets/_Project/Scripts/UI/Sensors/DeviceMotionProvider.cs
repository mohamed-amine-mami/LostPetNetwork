using LostPetNetwork.Services.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LostPetNetwork.UI
{
    /// <summary>
    /// The phone's accelerometer and gyroscope attitude through the Input System. On a computer without
    /// these sensors it reports "unavailable", so features fall back to touch.
    /// </summary>
    public class DeviceMotionProvider : IMotionProvider
    {
        public bool IsAvailable => Accelerometer.current != null;

        const float StandardGravity = 9.80665f;

        float _restMagnitude = 1f; // slow average of the reading size; about 1 when the unit is g, about 9.8 when m/s²
        float _logPeak;
        float _nextLog;

        /// <summary>
        /// Acceleration in g. Devices report either g or m/s², so the unit is worked out from the average reading
        /// (a phone is mostly at rest, where the size is 1 g or 9.8 m/s²).
        /// </summary>
        public Vector3 Acceleration
        {
            get
            {
                if (Accelerometer.current == null) return Vector3.zero;
                var raw = Accelerometer.current.acceleration.ReadValue();
                _restMagnitude = Mathf.Lerp(_restMagnitude, raw.magnitude, 0.02f);
                var g = _restMagnitude > 4f ? raw / StandardGravity : raw;
                _logPeak = Mathf.Max(_logPeak, g.magnitude);
                if (Time.unscaledTime >= _nextLog)
                {
                    Debug.Log($"[Motion] peak {_logPeak:0.00} g, rest {_restMagnitude:0.0}");
                    _logPeak = 0f;
                    _nextLog = Time.unscaledTime + 1f;
                }
                return g;
            }
        }

        public Quaternion Attitude => AttitudeSensor.current != null ? AttitudeSensor.current.attitude.ReadValue() : Quaternion.identity;

        public void StartTracking()
        {
            if (Accelerometer.current != null)
            {
                InputSystem.EnableDevice(Accelerometer.current);
                Accelerometer.current.samplingFrequency = 60f; // a shake jolt lasts well under 100 ms
            }
            if (AttitudeSensor.current != null) InputSystem.EnableDevice(AttitudeSensor.current);
        }

        public void StopTracking()
        {
            if (Accelerometer.current != null) InputSystem.DisableDevice(Accelerometer.current);
            if (AttitudeSensor.current != null) InputSystem.DisableDevice(AttitudeSensor.current);
        }
    }

    /// <summary>Plugs the real phone sensors into the SensorHub when the app starts.</summary>
    public static class DeviceSensors
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            if (!SensorHub.Motion.IsAvailable) SensorHub.Motion = new DeviceMotionProvider();
            if (!SensorHub.Location.IsAvailable) SensorHub.Location = new DeviceLocationProvider();
            if (!SensorHub.Heading.IsAvailable) SensorHub.Heading = new DeviceHeadingProvider();
        }
    }
}
