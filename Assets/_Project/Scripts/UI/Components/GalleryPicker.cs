using System;
using System.IO;
using UnityEngine;

namespace LostPetNetwork.UI
{
    /// <summary>
    /// Lets the user choose a photo from the phone's gallery. On Android a small native screen (PickerActivity)
    /// opens the system chooser and hands back an upright, reduced JPEG; in the Unity Editor a file dialog is used.
    /// </summary>
    public sealed class GalleryPicker : MonoBehaviour
    {
        const string BridgeName = "GalleryPickerBridge"; // the native side sends its answer to this object

        /// <summary>Test seam: replaces the real picker. Call the callback with the picture's bytes, or null for "cancelled".</summary>
        public static Action<Action<byte[]>> Override;

        static GalleryPicker _instance;
        Action<byte[]> _pending;

        public static bool IsAvailable =>
            Override != null || Application.isEditor || Application.platform == RuntimePlatform.Android;

        public static void Pick(Action<byte[]> done)
        {
            if (Override != null) { Override(done); return; }
#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanel("Choose a photo", string.Empty, "jpg,jpeg,png");
            done(string.IsNullOrEmpty(path) ? null : File.ReadAllBytes(path));
#elif UNITY_ANDROID
            Instance()._pending = done;
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var intent = new AndroidJavaObject("android.content.Intent");
                intent.Call<AndroidJavaObject>("setClassName", activity, "com.lostpetnetwork.gallery.PickerActivity");
                activity.Call("startActivity", intent);
            }
            catch (Exception e)
            {
                Debug.LogWarning("The gallery could not be opened: " + e.Message);
                Instance()._pending = null;
                done(null);
            }
#else
            done(null);
#endif
        }

#if !UNITY_EDITOR
        static GalleryPicker Instance()
        {
            if (_instance != null) return _instance;
            var go = new GameObject(BridgeName);
            DontDestroyOnLoad(go);
            return _instance = go.AddComponent<GalleryPicker>();
        }
#endif

        /// <summary>Called by the native picker (through UnitySendMessage) with the picture's file path, or "" when cancelled.</summary>
        void OnPhotoPicked(string path)
        {
            var callback = _pending;
            _pending = null;
            byte[] bytes = null;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try { bytes = File.ReadAllBytes(path); File.Delete(path); }
                catch (Exception e) { Debug.LogWarning("The chosen photo could not be read: " + e.Message); }
            }
            callback?.Invoke(bytes);
        }
    }
}
