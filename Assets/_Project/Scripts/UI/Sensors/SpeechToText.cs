using System;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace LostPetNetwork.UI
{
    /// <summary>
    /// Speech to text with the phone's own recogniser (Android's SpeechRecognizer). It listens live to the microphone,
    /// so it cannot run at the same moment as the voice-note recorder. Not available in the Editor or on iOS yet.
    /// </summary>
    public sealed class SpeechToText : MonoBehaviour
    {
        const string BridgeName = "SpeechBridge"; // the native side sends its answers to this object

        /// <summary>Test seam: replaces the real recogniser. Receives (language tag, onPartial, onFinal, onError).</summary>
        public static Action<string, Action<string>, Action<string>, Action<string>> Override;

        public static bool IsListening { get; private set; }

        static SpeechToText _instance;
        Action<string> _onPartial, _onFinal, _onError;

        public static bool IsAvailable
        {
            get
            {
                if (Override != null) return true;
#if UNITY_ANDROID && !UNITY_EDITOR
                try
                {
                    using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    using var bridge = new AndroidJavaClass("com.lostpetnetwork.speech.SpeechBridge");
                    return bridge.CallStatic<bool>("isAvailable", activity);
                }
                catch (Exception) { return false; }
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Starts listening. languageTag is a BCP-47 tag such as "ar-TN" or "fr-FR", or empty for the phone's language.
        /// Returns null on success or a message for the user. onPartial gets the text so far, onFinal the finished text.
        /// </summary>
        public static async Awaitable<string> StartAsync(string languageTag, Action<string> onPartial, Action<string> onFinal, Action<string> onError)
        {
            if (IsListening) return null;
            if (Override != null)
            {
                IsListening = true;
                Override(languageTag, onPartial, text => { IsListening = false; onFinal(text); }, message => { IsListening = false; onError(message); });
                return null;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                Permission.RequestUserPermission(Permission.Microphone);
                for (int i = 0; i < 80 && !Permission.HasUserAuthorizedPermission(Permission.Microphone); i++)
                    await Awaitable.WaitForSecondsAsync(0.25f);
                if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
                    return "Microphone permission was denied. Enable it in Settings to dictate.";
            }
            if (!IsAvailable) return "Speech recognition isn't available on this phone.";
            var self = Instance();
            self._onPartial = onPartial;
            self._onFinal = onFinal;
            self._onError = onError;
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var bridge = new AndroidJavaClass("com.lostpetnetwork.speech.SpeechBridge");
                bridge.CallStatic("start", activity, languageTag ?? string.Empty);
                IsListening = true;
                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Speech recognition could not start: " + e.Message);
                return "Speech recognition couldn't start.";
            }
#else
            await Awaitable.NextFrameAsync();
            return "Speech recognition is only available on an Android phone.";
#endif
        }

        /// <summary>Stops listening; the text heard so far still arrives through onFinal.</summary>
        public static void Stop()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Call("stop");
#endif
        }

        /// <summary>Drops the session without delivering a result.</summary>
        public static void Cancel()
        {
            IsListening = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            Call("cancel");
            if (_instance != null) { _instance._onPartial = null; _instance._onFinal = null; _instance._onError = null; }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static SpeechToText Instance()
        {
            if (_instance != null) return _instance;
            var go = new GameObject(BridgeName);
            DontDestroyOnLoad(go);
            return _instance = go.AddComponent<SpeechToText>();
        }

        static void Call(string method)
        {
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var bridge = new AndroidJavaClass("com.lostpetnetwork.speech.SpeechBridge");
                bridge.CallStatic(method, activity);
            }
            catch (Exception e) { Debug.LogWarning("Speech recognition call failed: " + e.Message); }
        }
#endif

        /// <summary>Friendly text for a SpeechRecognizer.ERROR_* code.</summary>
        public static string DescribeError(string code)
        {
            switch (code)
            {
                case "1": case "2": return "Speech recognition needs an internet connection right now. Check it and try again.";
                case "3": return "The microphone had a problem. Try again.";
                case "6": return "I didn't hear anything. Tap Dictate and speak after the beep.";
                case "7": return "I couldn't understand that. Try again, speaking clearly, or change the language.";
                case "8": return "Speech recognition is busy. Wait a moment and try again.";
                case "9": return "Microphone permission was denied. Enable it in Settings to dictate.";
                default: return "Speech recognition didn't work. Try again.";
            }
        }

        // ---- called by the native side through UnitySendMessage
        void OnSpeechPartial(string text) => _onPartial?.Invoke(text);

        void OnSpeechResult(string text)
        {
            IsListening = false;
            _onFinal?.Invoke(text);
        }

        void OnSpeechError(string code)
        {
            IsListening = false;
            _onError?.Invoke(DescribeError(code));
        }
    }
}
