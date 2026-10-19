using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace LostPetNetwork.UI
{
    /// <summary>
    /// Takes a photo with the device camera (WebCamTexture) and stores it in the report draft as a JPEG.
    /// Choosing from the gallery needs a native picker plugin and is NOT implemented yet.
    /// </summary>
    public class CameraScreen : MonoBehaviour
    {
        const int MaxPhotoSide = 1024;

        /// <summary>Test seam: skip permission prompts and device access (automated tests have no camera).</summary>
        public static bool SkipHardware;

        [SerializeField] ScreenManager manager;
        [SerializeField] RawImage preview;
        [SerializeField] AspectRatioFitter previewFitter;
        [SerializeField] Button closeButton;
        [SerializeField] Button shutterButton;
        [SerializeField] Button galleryButton;
        [SerializeField] Button flipButton;
        [SerializeField] GameObject statusPanel;
        [SerializeField] TMP_Text statusLabel;

        WebCamTexture _cam;
        int _deviceIndex;
        Coroutine _startRoutine;

        public bool IsStreaming => _cam != null && _cam.isPlaying && _cam.width > 16;

        void Awake()
        {
            closeButton.onClick.AddListener(Close);
            shutterButton.onClick.AddListener(Capture);
            flipButton.onClick.AddListener(Flip);
            galleryButton.onClick.AddListener(() =>
                ShowStatus("Choosing from your gallery needs a native picker plugin, which isn't installed yet. Use the camera for now."));
        }

        void OnEnable()
        {
            statusPanel.SetActive(false);
            shutterButton.interactable = false;
            _startRoutine = StartCoroutine(StartCamera());
        }

        void OnDisable()
        {
            if (_startRoutine != null) StopCoroutine(_startRoutine);
            StopCamera();
        }

        void Close()
        {
            if (!manager.Back()) manager.Show(ScreenId.Home);
        }

        void ShowStatus(string message)
        {
            statusLabel.text = message;
            statusPanel.SetActive(true);
        }

        IEnumerator StartCamera()
        {
            if (SkipHardware) yield break;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Permission.RequestUserPermission(Permission.Camera);
                float waited = 0f;
                while (!Permission.HasUserAuthorizedPermission(Permission.Camera) && waited < 20f)
                {
                    waited += 0.25f;
                    yield return new WaitForSeconds(0.25f);
                }
                if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
                {
                    ShowStatus("Camera permission was denied. Enable it in Settings to take photos.");
                    yield break;
                }
            }
#else
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                ShowStatus("Camera permission was denied. Enable it in Settings to take photos.");
                yield break;
            }
#endif
            var devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                ShowStatus("No camera was found on this device.");
                yield break;
            }

            _deviceIndex = Mathf.Clamp(_deviceIndex, 0, devices.Length - 1);
            flipButton.gameObject.SetActive(devices.Length > 1);
            _cam = new WebCamTexture(devices[_deviceIndex].name, 1280, 720, 30);
            preview.texture = _cam;
            _cam.Play();

            float timeout = 8f;
            while (_cam.width <= 16 && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            if (_cam.width <= 16)
            {
                ShowStatus("The camera didn't start. Close other apps using it and try again.");
                yield break;
            }

            ApplyPreviewOrientation();
            shutterButton.interactable = true;
        }

        void ApplyPreviewOrientation()
        {
            int angle = _cam.videoRotationAngle;
            preview.rectTransform.localEulerAngles = new Vector3(0, 0, -angle);
            preview.uvRect = _cam.videoVerticallyMirrored ? new Rect(0, 1, 1, -1) : new Rect(0, 0, 1, 1);
            bool rotated = angle == 90 || angle == 270;
            previewFitter.aspectRatio = rotated ? (float)_cam.height / _cam.width : (float)_cam.width / _cam.height;
        }

        void StopCamera()
        {
            if (_cam == null) return;
            _cam.Stop();
            Destroy(_cam);
            _cam = null;
            preview.texture = null;
        }

        void Flip()
        {
            var count = WebCamTexture.devices.Length;
            if (count < 2) return;
            _deviceIndex = (_deviceIndex + 1) % count;
            StopCamera();
            shutterButton.interactable = false;
            if (_startRoutine != null) StopCoroutine(_startRoutine);
            _startRoutine = StartCoroutine(StartCamera());
        }

        void Capture()
        {
            if (!IsStreaming) return;
            var raw = new Texture2D(_cam.width, _cam.height, TextureFormat.RGBA32, false);
            raw.SetPixels32(_cam.GetPixels32());
            raw.Apply();
            var oriented = Orient(raw, _cam.videoRotationAngle, _cam.videoVerticallyMirrored);
            if (oriented != raw) Destroy(raw);
            CommitCapture(oriented);
        }

        /// <summary>Stores the texture as the draft photo (JPEG) and returns to the report form. Also used by tests.</summary>
        public void CommitCapture(Texture2D texture)
        {
            AppSession.Draft.PhotoBytes = EncodeJpeg(texture, MaxPhotoSide);
            Destroy(texture);
            Close();
        }

        static void DestroyTemp(Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        public static byte[] EncodeJpeg(Texture2D source, int maxSide)
        {
            float scale = Mathf.Min(1f, (float)maxSide / Mathf.Max(source.width, source.height));
            if (scale >= 1f) return source.EncodeToJPG(82);

            int w = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
            int h = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            var scaled = new Texture2D(w, h, TextureFormat.RGB24, false);
            scaled.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            scaled.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            byte[] bytes = scaled.EncodeToJPG(82);
            DestroyTemp(scaled);
            return bytes;
        }

        /// <summary>Rotates (clockwise, multiples of 90°) and optionally flips vertically so the photo is upright.</summary>
        public static Texture2D Orient(Texture2D src, int angle, bool verticallyMirrored)
        {
            angle = ((angle % 360) + 360) % 360;
            if (angle == 0 && !verticallyMirrored) return src;

            int w = src.width, h = src.height;
            var input = src.GetPixels32();
            bool swap = angle == 90 || angle == 270;
            int ow = swap ? h : w, oh = swap ? w : h;
            var output = new Color32[ow * oh];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int sy = verticallyMirrored ? h - 1 - y : y;
                var pixel = input[sy * w + x];
                int nx, ny;
                switch (angle)
                {
                    case 90: nx = y; ny = w - 1 - x; break;
                    case 180: nx = w - 1 - x; ny = h - 1 - y; break;
                    case 270: nx = h - 1 - y; ny = x; break;
                    default: nx = x; ny = y; break;
                }
                output[ny * ow + nx] = pixel;
            }
            var result = new Texture2D(ow, oh, TextureFormat.RGBA32, false);
            result.SetPixels32(output);
            result.Apply();
            return result;
        }
    }
}
