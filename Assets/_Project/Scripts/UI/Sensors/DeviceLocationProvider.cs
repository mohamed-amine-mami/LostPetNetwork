using System;
using System.Collections;
using LostPetNetwork.Services.Sensors;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace LostPetNetwork.UI
{
    /// <summary>Runs sensor polling loops that must outlive any single screen.</summary>
    public class DeviceSensorRunner : MonoBehaviour
    {
        static DeviceSensorRunner _instance;

        public static DeviceSensorRunner Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("DeviceSensorRunner") { hideFlags = HideFlags.HideAndDontSave };
                DontDestroyOnLoad(go);
                return _instance = go.AddComponent<DeviceSensorRunner>();
            }
        }
    }

    /// <summary>
    /// GPS through Unity's location service. Asks for the location permission on Android, then reports a fix
    /// about once a second while tracking. If the user denies it or location is off, Latest stays null.
    /// </summary>
    public class DeviceLocationProvider : ILocationProvider
    {
        bool _tracking;
        Coroutine _loop;

        public bool IsAvailable => Application.isMobilePlatform;
        public GeoFix? Latest { get; private set; }
        public event Action<GeoFix> Updated;

        public void StartTracking()
        {
            if (_tracking) return;
            _tracking = true;
            _loop = DeviceSensorRunner.Instance.StartCoroutine(Run());
        }

        public void StopTracking()
        {
            _tracking = false;
            if (_loop != null && DeviceSensorRunner.Instance != null) DeviceSensorRunner.Instance.StopCoroutine(_loop);
            _loop = null;
            if (Input.location.status != LocationServiceStatus.Stopped) Input.location.Stop();
        }

        IEnumerator Run()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                Permission.RequestUserPermission(Permission.FineLocation);
                float waited = 0f;
                while (!Permission.HasUserAuthorizedPermission(Permission.FineLocation) && waited < 20f)
                {
                    waited += 0.25f;
                    yield return new WaitForSecondsRealtime(0.25f);
                }
                if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation)) { _tracking = false; yield break; }
            }
#endif
            if (!Input.location.isEnabledByUser) { _tracking = false; yield break; }
            Input.location.Start(10f, 10f);
            float timeout = 15f;
            while (Input.location.status == LocationServiceStatus.Initializing && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            while (_tracking && Input.location.status == LocationServiceStatus.Running)
            {
                var d = Input.location.lastData;
                var fix = new GeoFix(d.latitude, d.longitude, d.horizontalAccuracy);
                Latest = fix;
                Updated?.Invoke(fix);
                yield return new WaitForSecondsRealtime(1f);
            }
            _tracking = false;
        }
    }

    /// <summary>The phone's compass heading (degrees clockwise from north).</summary>
    public class DeviceHeadingProvider : IHeadingProvider
    {
        public bool IsAvailable => Application.isMobilePlatform;

        public float? HeadingDegrees
        {
            get
            {
                if (!Input.compass.enabled) return null;
                float heading = Input.compass.trueHeading > 0f ? Input.compass.trueHeading : Input.compass.magneticHeading;
                return heading;
            }
        }

        public void StartTracking() => Input.compass.enabled = true;

        public void StopTracking() => Input.compass.enabled = false;
    }
}
