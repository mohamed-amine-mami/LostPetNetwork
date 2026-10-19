using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.EditorTools
{
    /// <summary>
    /// Dev helper used by automated Play-mode checks (via the Unity CLI) to drive the UI like a user:
    /// set field text, click buttons, read labels. Paths are relative to Canvas/SafeArea.
    /// </summary>
    public static class UiTestDriver
    {
        static Transform Find(string path)
        {
            var canvas = GameObject.Find("Canvas");
            var t = canvas != null ? canvas.transform.Find("SafeArea/" + path) : null;
            if (t == null) throw new System.Exception("UI path not found: " + path);
            return t;
        }

        public static void SetText(string path, string value) => Find(path).GetComponent<TMP_InputField>().text = value;

        public static void Click(string path) => Find(path).GetComponent<Button>().onClick.Invoke();

        public static string Label(string path) => Find(path).GetComponentInChildren<TMP_Text>(true).text;

        public static bool IsActive(string path) => Find(path).gameObject.activeSelf;

        public static bool IsInteractable(string path) => Find(path).GetComponent<Button>().interactable;

        /// <summary>Selects (creating if needed) a fixed-resolution Game view size, e.g. 780x1688 for a phone.</summary>
        public static string SetGameViewSize(int width, int height)
        {
            var editorAsm = typeof(UnityEditor.Editor).Assembly;
            var sizesType = editorAsm.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizesType);
            var instance = singleton.GetProperty("instance").GetValue(null);
            var group = sizesType.GetMethod("GetGroup").Invoke(instance, new object[] { 0 }); // Standalone
            var groupType = group.GetType();
            string label = $"Phone {width}x{height}";
            var getDisplayTexts = groupType.GetMethod("GetDisplayTexts");
            var texts = (string[])getDisplayTexts.Invoke(group, null);
            int index = System.Array.FindIndex(texts, t => t.Contains(label));
            if (index < 0)
            {
                var sizeType = editorAsm.GetType("UnityEditor.GameViewSize");
                var sizeEnum = editorAsm.GetType("UnityEditor.GameViewSizeType");
                var fixedRes = System.Enum.ToObject(sizeEnum, 1);
                var size = System.Activator.CreateInstance(sizeType, new object[] { fixedRes, width, height, label });
                groupType.GetMethod("AddCustomSize").Invoke(group, new object[] { size });
                texts = (string[])getDisplayTexts.Invoke(group, null);
                index = System.Array.FindIndex(texts, t => t.Contains(label));
            }
            var gvType = editorAsm.GetType("UnityEditor.GameView");
            var window = UnityEditor.EditorWindow.GetWindow(gvType);
            gvType.GetProperty("selectedSizeIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                .SetValue(window, index);
            window.Repaint();
            return $"game view size index {index} ({label})";
        }

        static RenderTexture _snapRt;
        static RenderMode _snapMode;

        /// <summary>Step 1 of a pixel-accurate screenshot: route the canvas to a 780x1688 render texture.</summary>
        public static string SnapPrepare(int width = 780, int height = 1688)
        {
            var canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
            var cam = Camera.main;
            if (_snapRt != null) _snapRt.Release();
            _snapRt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            _snapMode = canvas.renderMode;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            cam.targetTexture = _snapRt;
            return "prepared";
        }

        /// <summary>Step 2 (a frame later): render, save the PNG, restore the overlay canvas.</summary>
        public static string SnapCapture(string path)
        {
            var canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
            var cam = Camera.main;
            UnityEngine.Canvas.ForceUpdateCanvases();
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = _snapRt;
            var tex = new Texture2D(_snapRt.width, _snapRt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, _snapRt.width, _snapRt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            cam.targetTexture = null;
            canvas.renderMode = _snapMode;
            _snapRt.Release();
            _snapRt = null;
            return "saved " + path;
        }

        public static string Current() => Object.FindAnyObjectByType<LostPetNetwork.UI.ScreenManager>().Current.ToString();
    }
}
