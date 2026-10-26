using LostPetNetwork.Services.Sensors;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>
    /// Shows a 3D object inside the 2D UI: the object lives on a hidden stage far from the origin,
    /// a private camera renders it into a RenderTexture and this RawImage displays that texture.
    /// Drag rotates, scroll wheel or two-finger pinch zooms. Needs the 3D renderer (index 1) in the URP asset.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class Preview3D : MonoBehaviour, IDragHandler, IEndDragHandler, IScrollHandler
    {
        /// <summary>Index of the standard (3D) renderer in the URP asset; index 0 is the 2D renderer.</summary>
        public const int RendererIndex = 1;

        const float MinZoom = 0.5f;
        const float MaxZoom = 2.5f;
        const float DefaultPitch = 10f;
        const float TiltDegrees = 40f;
        const float Fov = 30f;

        static int _stageCounter;

        [SerializeField] int textureSize = 512;
        [SerializeField] float degreesPerPixel = 0.4f;
        [SerializeField] float idleSpinDegreesPerSecond;

        RawImage _image;
        GameObject _stage;
        Transform _pivot;
        Camera _camera;
        RenderTexture _texture;
        GameObject _content;
        float _yaw;
        float _zoom = 1f;
        float _radius = 1f;
        float _lastPinch;
        float _pitch = DefaultPitch;
        float _tilt;
        bool _dragging;
        bool _tiltToRotate;
        bool _fixedFrame;
        Vector3 _fixedCenter;

        public Camera PreviewCamera => _camera;
        public Transform Pivot => _pivot;
        public float Yaw => _yaw;
        public float Zoom => _zoom;
        public GameObject Content => _content;

        void Awake()
        {
            _image = GetComponent<RawImage>();
            _image.raycastTarget = true;
        }

        void OnEnable()
        {
            EnsureStage();
            _stage.SetActive(true);
            _image.texture = _texture;
            _image.color = Color.white;
            if (_tiltToRotate) SensorHub.Motion.StartTracking();
            ApplyView();
        }

        void OnDisable()
        {
            if (_tiltToRotate) SensorHub.Motion.StopTracking();
            if (_stage != null) _stage.SetActive(false);
        }

        void OnDestroy()
        {
            if (_stage != null) DestroyTemp(_stage);
            if (_texture != null)
            {
                _texture.Release();
                DestroyTemp(_texture);
            }
        }

        void Update()
        {
            if (_content == null) return;
            if (idleSpinDegreesPerSecond != 0f) SetYaw(_yaw + idleSpinDegreesPerSecond * Time.unscaledDeltaTime);
            HandlePinch();
            UpdateTilt();
        }

        public float Pitch => _pitch;

        /// <summary>The turn the view returns to on ResetView (a three-quarter view suits animals).</summary>
        public float DefaultYaw { get; set; }

        /// <summary>When true and the phone has an accelerometer, tilting the phone turns the object.</summary>
        public bool TiltToRotate
        {
            get => _tiltToRotate;
            set
            {
                _tiltToRotate = value;
                if (!isActiveAndEnabled) return;
                if (value) SensorHub.Motion.StartTracking();
                else _tilt = 0f;
            }
        }

        /// <summary>
        /// Puts a 3D object on the stage (parented under the stage and re-centred). Pass frameCenter and
        /// frameRadius (in the object's own space) to keep the camera framing identical between rebuilds.
        /// </summary>
        public void SetContent(GameObject content, Vector3? frameCenter = null, float frameRadius = 0f)
        {
            EnsureStage();
            ClearContent();
            _content = content;
            content.transform.SetParent(_pivot, false);
            content.transform.localPosition = Vector3.zero;
            content.transform.localRotation = Quaternion.identity;
            _fixedFrame = frameCenter.HasValue && frameRadius > 0f;
            if (_fixedFrame)
            {
                _fixedCenter = frameCenter.Value;
                _radius = frameRadius;
            }
            Frame();
        }

        public void ClearContent()
        {
            if (_content != null) DestroyTemp(_content);
            _content = null;
        }

        public void SetPitch(float pitch)
        {
            _pitch = Mathf.Clamp(pitch, -60f, 60f);
            ApplyView();
        }

        public void SetYaw(float yaw)
        {
            _yaw = yaw;
            ApplyView();
        }

        public void SetZoom(float zoom)
        {
            _zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
            ApplyView();
        }

        public void ResetView()
        {
            _yaw = DefaultYaw;
            _pitch = DefaultPitch;
            _zoom = 1f;
            ApplyView();
        }

        /// <summary>Copies the most recently rendered frame (transparent background) into a new texture.</summary>
        public Texture2D Capture()
        {
            EnsureStage();
            var previous = RenderTexture.active;
            RenderTexture.active = _texture;
            var result = new Texture2D(_texture.width, _texture.height, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, _texture.width, _texture.height), 0, 0);
            result.Apply();
            RenderTexture.active = previous;
            return result;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_content == null) return;
            _dragging = true;
            SetYaw(_yaw - eventData.delta.x * degreesPerPixel);
        }

        public void OnEndDrag(PointerEventData eventData) => _dragging = false;

        public void OnScroll(PointerEventData eventData)
        {
            SetZoom(_zoom * (1f + eventData.scrollDelta.y * 0.05f));
        }

        void UpdateTilt()
        {
            if (!_tiltToRotate || _dragging || !SensorHub.Motion.IsAvailable) return;
            float target = Mathf.Clamp(SensorHub.Motion.Acceleration.x, -1f, 1f) * TiltDegrees;
            float next = Mathf.Lerp(_tilt, target, Mathf.Clamp01(Time.unscaledDeltaTime * 8f));
            if (Mathf.Abs(next - _tilt) < 0.01f) return;
            _tilt = next;
            ApplyView();
        }

        void HandlePinch()
        {
            var screen = Touchscreen.current;
            if (screen == null) return;
            var a = screen.touches[0];
            var b = screen.touches[1];
            if (!a.isInProgress || !b.isInProgress)
            {
                _lastPinch = 0f;
                return;
            }
            Vector2 pa = a.position.ReadValue(), pb = b.position.ReadValue();
            if (!RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, (pa + pb) * 0.5f, null))
                return;
            float distance = Vector2.Distance(pa, pb);
            if (_lastPinch > 0f && distance > 1f) SetZoom(_zoom * distance / _lastPinch);
            _lastPinch = distance;
        }

        void EnsureStage()
        {
            if (_stage != null) return;

            int index = _stageCounter++;
            _stage = new GameObject("Preview3D Stage " + index) { hideFlags = HideFlags.DontSave };
            _stage.transform.position = new Vector3(5000f + index * 50f, 5000f, 5000f);

            _pivot = new GameObject("Pivot").transform;
            _pivot.SetParent(_stage.transform, false);

            _texture = new RenderTexture(textureSize, textureSize, 24, RenderTextureFormat.ARGB32)
            {
                name = "Preview3D",
                antiAliasing = 4,
                hideFlags = HideFlags.DontSave
            };

            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(_stage.transform, false);
            _camera = camGo.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _camera.fieldOfView = Fov;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 100f;
            _camera.targetTexture = _texture;
            var data = _camera.GetUniversalAdditionalCameraData();
            data.SetRenderer(RendererIndex);
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;

            var keyGo = new GameObject("Key Light");
            keyGo.transform.SetParent(_stage.transform, false);
            keyGo.transform.localRotation = Quaternion.Euler(40f, 150f, 0f);
            var key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.1f;
            key.color = new Color(1f, 0.96f, 0.9f);
            key.shadows = LightShadows.None;

            var fillGo = new GameObject("Fill Light");
            fillGo.transform.SetParent(_stage.transform, false);
            fillGo.transform.localRotation = Quaternion.Euler(-20f, -150f, 0f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.45f;
            fill.color = new Color(0.85f, 0.9f, 1f);
            fill.shadows = LightShadows.None;

            // Flat ambient so the object is readable without a skybox; only 3D scenes use this.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.52f, 0.5f);
        }

        void Frame()
        {
            // Measure with the pivot unrotated so the framing does not depend on the current spin.
            _pivot.localRotation = Quaternion.identity;
            if (_fixedFrame)
            {
                _content.transform.position -= _fixedCenter;
                ApplyView();
                return;
            }

            var renderers = _content.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                _radius = 1f;
                ApplyView();
                return;
            }

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            _content.transform.position -= bounds.center - _pivot.position;
            _radius = Mathf.Max(0.01f, bounds.extents.magnitude);
            ApplyView();
        }

        void ApplyView()
        {
            if (_pivot == null || _camera == null) return;
            _pivot.localRotation = Quaternion.Euler(0f, _yaw + _tilt, 0f);
            float distance = _radius / Mathf.Sin(Fov * 0.5f * Mathf.Deg2Rad) * 1.05f / _zoom;
            // Content faces +Z, so the camera sits in front of it (rotated 180° around Y).
            var front = Quaternion.Euler(0f, 180f, 0f);
            var rig = front * Quaternion.Euler(_pitch, 0f, 0f);
            _camera.transform.localPosition = rig * new Vector3(0f, 0f, -distance);
            _camera.transform.localRotation = rig;
        }

        static void DestroyTemp(Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
