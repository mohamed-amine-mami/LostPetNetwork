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

    /// <summary>
    /// The compass heading of the direction the back camera faces (degrees clockwise from north). On Android it
    /// reads the rotation-vector sensor directly, because Unity's legacy compass returns nothing when only the new
    /// Input System is active. Elsewhere it uses Unity's compass.
    /// </summary>
    public class DeviceHeadingProvider : IHeadingProvider
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        const int RotationVectorSensor = 11;
        const int SensorDelayUi = 2;

        sealed class Listener : AndroidJavaProxy
        {
            public volatile float Heading = -1f;
            public Listener() : base("android.hardware.SensorEventListener") { }

            public void onSensorChanged(AndroidJavaObject sensorEvent)
            {
                var v = sensorEvent.Get<float[]>("values");
                if (v == null || v.Length < 3) return;
                float w = v.Length > 3 ? v[3] : Mathf.Sqrt(Mathf.Max(0f, 1f - v[0] * v[0] - v[1] * v[1] - v[2] * v[2]));
                var h = CompassMath.HeadingFromRotationVector(v[0], v[1], v[2], w);
                if (h.HasValue) Heading = h.Value;
            }

            public void onAccuracyChanged(AndroidJavaObject sensor, int accuracy) { }
        }

        AndroidJavaObject _manager;
        AndroidJavaObject _sensor;
        Listener _listener;
#endif

        public bool IsAvailable => Application.isMobilePlatform;

        public float? HeadingDegrees
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (_listener != null) return _listener.Heading >= 0f ? _listener.Heading : (float?)null;
#endif
                if (!Input.compass.enabled) return null;
                return Input.compass.trueHeading > 0f ? Input.compass.trueHeading : Input.compass.magneticHeading;
            }
        }

        public void StartTracking()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (_listener == null)
                {
                    using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    _manager = activity.Call<AndroidJavaObject>("getSystemService", "sensor");
                    _sensor = _manager?.Call<AndroidJavaObject>("getDefaultSensor", RotationVectorSensor);
                    if (_sensor != null)
                    {
                        _listener = new Listener();
                        _manager.Call<bool>("registerListener", _listener, _sensor, SensorDelayUi);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Rotation-vector compass unavailable: " + e.Message);
                _listener = null;
            }
#endif
            Input.compass.enabled = true;
        }

        public void StopTracking()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (_listener != null && _manager != null) _manager.Call("unregisterListener", _listener);
            }
            catch (System.Exception e) { Debug.LogWarning("Stopping the compass failed: " + e.Message); }
            _listener = null;
            _sensor = null;
            _manager = null;
#endif
            Input.compass.enabled = false;
        }
    }
}
