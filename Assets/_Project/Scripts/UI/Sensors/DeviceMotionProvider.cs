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

        public Vector3 Acceleration => Accelerometer.current != null ? Accelerometer.current.acceleration.ReadValue() : Vector3.zero;

        public Quaternion Attitude => AttitudeSensor.current != null ? AttitudeSensor.current.attitude.ReadValue() : Quaternion.identity;

        public void StartTracking()
        {
            if (Accelerometer.current != null) InputSystem.EnableDevice(Accelerometer.current);
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
