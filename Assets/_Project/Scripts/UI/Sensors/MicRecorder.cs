using LostPetNetwork.Services.Sensors;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace LostPetNetwork.UI
{
    /// <summary>Records a short voice note (up to ten seconds, 16 kHz mono) and returns it as a WAV file.</summary>
    public static class MicRecorder
    {
        public const int SampleRate = 16000;
        public const int MaxSeconds = 10;

        static AudioClip _clip;
        static float _startedAt;

        public static bool IsRecording => _clip != null && Microphone.IsRecording(null);
        public static float Elapsed => IsRecording ? Time.realtimeSinceStartup - _startedAt : 0f;

        /// <summary>Starts recording. Returns null on success or a message for the user.</summary>
        public static async Awaitable<string> StartAsync()
        {
            if (IsRecording) return null;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                Permission.RequestUserPermission(Permission.Microphone);
                for (int i = 0; i < 80 && !Permission.HasUserAuthorizedPermission(Permission.Microphone); i++)
                    await Awaitable.WaitForSecondsAsync(0.25f);
                if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
                    return "Microphone permission was denied. Enable it in Settings to record a voice note.";
            }
#else
            await Awaitable.NextFrameAsync();
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                await Application.RequestUserAuthorization(UserAuthorization.Microphone);
                if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
                    return "Microphone permission was denied. Enable it in Settings to record a voice note.";
            }
#endif
            if (Microphone.devices.Length == 0) return "No microphone was found on this device.";
            _clip = Microphone.Start(null, false, MaxSeconds, SampleRate);
            _startedAt = Time.realtimeSinceStartup;
            return _clip == null ? "The microphone didn't start." : null;
        }

        /// <summary>Stops and returns the recording as WAV bytes (null when nothing was recorded).</summary>
        public static byte[] Stop()
        {
            if (_clip == null) return null;
            int position = Microphone.GetPosition(null);
            if (Microphone.IsRecording(null)) Microphone.End(null);
            var clip = _clip;
            _clip = null;
            if (position <= 0) position = Mathf.Min(clip.samples, Mathf.RoundToInt((Time.realtimeSinceStartup - _startedAt) * SampleRate));
            if (position <= SampleRate / 5) return null; // under 0.2 s is just a tap
            var samples = new float[position];
            clip.GetData(samples, 0);
            Object.Destroy(clip);
            return WavCodec.Encode(samples, SampleRate);
        }

        public static void Cancel()
        {
            if (Microphone.IsRecording(null)) Microphone.End(null);
            if (_clip != null) Object.Destroy(_clip);
            _clip = null;
        }
    }

    /// <summary>Plays a WAV voice note.</summary>
    public static class VoicePlayback
    {
        static AudioSource _source;

        public static bool IsPlaying => _source != null && _source.isPlaying;

        /// <summary>Returns false when the bytes are not a playable WAV.</summary>
        public static bool Play(byte[] wav)
        {
            if (!WavCodec.TryDecode(wav, out var samples, out int rate) || samples.Length == 0) return false;
            if (_source == null)
            {
                var go = new GameObject("VoicePlayback") { hideFlags = HideFlags.HideAndDontSave };
                Object.DontDestroyOnLoad(go);
                _source = go.AddComponent<AudioSource>();
            }
            Stop();
            var clip = AudioClip.Create("voice-note", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            _source.clip = clip;
            _source.Play();
            return true;
        }

        public static void Stop()
        {
            if (_source == null) return;
            _source.Stop();
            if (_source.clip != null) Object.Destroy(_source.clip);
            _source.clip = null;
        }
    }
}
