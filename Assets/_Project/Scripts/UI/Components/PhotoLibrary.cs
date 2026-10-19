using System;
using System.Collections.Generic;
using LostPetNetwork.Services.Reports;
using UnityEngine;

namespace LostPetNetwork.UI
{
    /// <summary>Bundled demo photos (from the Figma design) plus runtime sprites for camera captures.</summary>
    [CreateAssetMenu(menuName = "LostPetNetwork/Photo Library")]
    public class PhotoLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string key;
            public Sprite sprite;
        }

        [SerializeField] List<Entry> entries = new List<Entry>();

        static PhotoLibrary _instance;
        static readonly Dictionary<string, Sprite> Captured = new Dictionary<string, Sprite>();

        public static PhotoLibrary Instance => _instance != null ? _instance : (_instance = Resources.Load<PhotoLibrary>("PhotoLibrary"));

        public Sprite Get(string key)
        {
            foreach (var e in entries)
                if (e.key == key) return e.sprite;
            return null;
        }

        /// <summary>Sprite for a report: captured JPEG, bundled demo photo, or null (UI shows a placeholder).</summary>
        public static Sprite ForReport(PetReport report)
        {
            if (report == null) return null;
            if (report.PhotoBytes != null && report.PhotoBytes.Length > 0)
            {
                if (Captured.TryGetValue(report.Id, out var cached) && cached != null) return cached;
                var sprite = FromJpeg(report.PhotoBytes);
                if (sprite != null) Captured[report.Id] = sprite;
                return sprite;
            }
            var lib = Instance;
            return lib != null && !string.IsNullOrEmpty(report.PhotoKey) ? lib.Get(report.PhotoKey) : null;
        }

        public static Sprite FromJpeg(byte[] bytes)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (!tex.LoadImage(bytes))
            {
                if (Application.isPlaying) Destroy(tex); else DestroyImmediate(tex);
                return null;
            }
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
