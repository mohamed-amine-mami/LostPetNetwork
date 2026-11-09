using System.Collections.Generic;
using LostPetNetwork.Services.Avatar;
using LostPetNetwork.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace LostPetNetwork.AR
{
    /// <summary>
    /// Puts a life-size 3D pet (or the user's avatar) on the floor or a table in the real room. A ring follows the
    /// middle of the screen and turns green on a surface big enough to use; "Place here" drops the model there.
    /// Afterwards one finger moves (or turns) it, two fingers pinch to resize and twist to turn. For pets a ring on
    /// the floor shows the space the pet needs.
    /// </summary>
    public class ArPlacementController : MonoBehaviour
    {
        [SerializeField] ARRaycastManager raycastManager;
        [SerializeField] ARPlaneManager planeManager;
        [SerializeField] ARCameraManager cameraManager;
        [SerializeField] Camera arCamera;
        [SerializeField] Light sun;
        [SerializeField] TMP_Text titleLabel;
        [SerializeField] TMP_Text hintLabel;
        [SerializeField] TMP_Text infoLabel;
        [SerializeField] Button closeButton;
        [SerializeField] Button resetButton;
        [SerializeField] Button placeButton;
        [SerializeField] Button modeButton;
        [SerializeField] Button realSizeButton;
        [SerializeField] GameObject controlsRow;

        static readonly List<ARRaycastHit> Hits = new List<ARRaycastHit>();

        static readonly Color ReticleReady = new Color(0.25f, 0.9f, 0.5f);
        static readonly Color ReticleSmall = new Color(0.97f, 0.75f, 0.25f);
        const float ReticleRadius = 0.13f;

        GameObject _content;
        GameObject _ring;
        GameObject _reticle;
        Renderer _reticleRenderer;
        Material _readyMaterial, _smallMaterial;
        Vector3 _reticleTarget, _lastPoint, _smoothedPoint;
        bool _reticleReady, _hasPoint;
        float _scanSeconds;
        readonly ReticleStabilizer _stabilizer = new ReticleStabilizer();
        float _failedAt;
        float _dragStartX;
        float _dragYaw;
        bool _dragging;
        bool _moveMode = true;
        Vector3 _baseScale = Vector3.one;
        float _realHeight;
        float _userScale = 1f;
        Vector3 _moveOffset;
        bool _twoFingers;
        bool _ignoreUntilRelease;
        float _pinchStartDistance, _pinchStartScale, _twistStartYaw;
        Vector2 _twistStartVector;

        public bool HasPlaced => _content != null;
        /// <summary>The user's size multiplier against the real size (1 = life size).</summary>
        public float UserScale => _userScale;
        public bool MoveMode => _moveMode;
        public bool ReticleReadyToPlace => _reticleReady;
        public Vector3 ContentPosition => _content != null ? _content.transform.position : Vector3.zero;
        public float ContentYaw => _content != null ? _content.transform.eulerAngles.y : 0f;

        void Awake()
        {
            closeButton.onClick.AddListener(Close);
            resetButton.onClick.AddListener(ResetPlacement);
            placeButton.onClick.AddListener(PlaceAtReticle);
            modeButton.onClick.AddListener(ToggleMode);
            realSizeButton.onClick.AddListener(() => SetUserScale(1f));
        }

        void OnEnable()
        {
            if (cameraManager != null) cameraManager.frameReceived += OnFrame;
        }

        void OnDisable()
        {
            if (cameraManager != null) cameraManager.frameReceived -= OnFrame;
            if (_reticle != null) Destroy(_reticle);
            _reticle = null;
        }

        void Start()
        {
            titleLabel.text = ArRequest.Mode == ArMode.PlaceAvatar ? "Your avatar" : (string.IsNullOrEmpty(ArRequest.PetName) ? "Try at home" : $"{ArRequest.PetName} at home");
            ResetPlacement();
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Close(); return; }
            if (ArSessionWatch.Failed(ref _failedAt)) { hintLabel.text = ArSessionWatch.Message; Invoke(nameof(Close), 2.5f); return; }
            if (!HasPlaced) UpdateReticle();
            else if (_reticle != null) _reticle.SetActive(false);
            if (HasPlaced) HandleGestures();
            else HandleTapToPlace();
            UpdateControls();
        }

        // ------------------------------------------------------------------ finding a place

        const TrackableType SurfaceTypes = TrackableType.PlaneWithinPolygon | TrackableType.PlaneEstimated | TrackableType.FeaturePoint;
        const float ScanHelpAfterSeconds = 5f;

        /// <summary>
        /// What the middle of the screen is aimed at. Detected surfaces win; while the phone is still scanning, loose
        /// feature points below eye level count as a rough spot, so there is something to place on much sooner.
        /// </summary>
        ReticleState Observe(out Vector3 point)
        {
            point = Vector3.zero;
            if (raycastManager == null) return ReticleState.None;
            if (!raycastManager.Raycast(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), Hits, SurfaceTypes)) return ReticleState.None;

            foreach (var hit in Hits)
            {
                if ((hit.hitType & TrackableType.PlaneWithinPolygon) == 0) continue;
                var plane = planeManager.GetPlane(hit.trackableId);
                point = hit.pose.position;
                return plane == null || ArMath.PlaneBigEnough(plane.size) ? ReticleState.Good : ReticleState.Rough;
            }
            foreach (var hit in Hits)
            {
                // Walls and ceilings are no use: a loose point must be clearly below the phone.
                if (hit.pose.position.y > arCamera.transform.position.y - 0.3f) continue;
                point = hit.pose.position;
                return ReticleState.Rough;
            }
            return ReticleState.None;
        }

        /// <summary>Aims at the middle of the screen: the ring shows where the model would land and how reliable that spot is.</summary>
        void UpdateReticle()
        {
            var observed = Observe(out var point);
            if (observed != ReticleState.None)
            {
                _lastPoint = point;
                if (!_hasPoint) { _smoothedPoint = point; _hasPoint = true; }
            }
            var state = _stabilizer.Feed(observed, Time.unscaledDeltaTime);
            _reticleReady = state != ReticleState.None;
            if (state == ReticleState.None)
            {
                _hasPoint = false;
                if (_reticle != null) _reticle.SetActive(false);
                if (planeManager.trackables.count == 0) _scanSeconds += Time.unscaledDeltaTime;
                hintLabel.text = planeManager.trackables.count > 0 ? "Aim at the floor or a table"
                    : _scanSeconds > ScanHelpAfterSeconds ? "Still scanning. Use good light and a floor with texture, not plain or shiny"
                    : "Move your phone slowly from side to side to scan the floor…";
                return;
            }

            _scanSeconds = 0f;
            _smoothedPoint = Vector3.Lerp(_smoothedPoint, _lastPoint, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            _reticleTarget = _smoothedPoint;
            EnsureReticle();
            _reticle.SetActive(true);
            bool good = state == ReticleState.Good;
            _reticleRenderer.sharedMaterial = good ? _readyMaterial : _smallMaterial;
            float pulse = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 4f);
            _reticle.transform.SetPositionAndRotation(_reticleTarget + Vector3.up * 0.01f, Quaternion.identity);
            _reticle.transform.localScale = new Vector3(pulse, 0.05f, pulse);
            hintLabel.text = good ? "Tap Place here" : "Rough spot, the model may shift. Keep scanning for green, or place it anyway";
        }

        void EnsureReticle()
        {
            if (_reticle != null) return;
            _readyMaterial = AvatarMaterials.For(ReticleReady, 0.1f);
            _smallMaterial = AvatarMaterials.For(ReticleSmall, 0.1f);
            _reticle = new GameObject("Reticle");
            _reticle.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Torus(ReticleRadius, 0.012f);
            _reticleRenderer = _reticle.AddComponent<MeshRenderer>();
            var dot = new GameObject("Dot");
            dot.transform.SetParent(_reticle.transform, false);
            dot.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Torus(0.018f, 0.012f);
            dot.AddComponent<MeshRenderer>().sharedMaterial = _readyMaterial;
        }

        void PlaceAtReticle()
        {
            if (HasPlaced || !_reticleReady) return;
            Place(_reticleTarget);
        }

        /// <summary>A tap anywhere that is not a button also places the model, at the same spot the ring shows.</summary>
        void HandleTapToPlace()
        {
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (_reticleReady) PlaceAtReticle();
        }

        /// <summary>Estimated light in the room tints the sun so the model does not look pasted on.</summary>
        void OnFrame(ARCameraFrameEventArgs args)
        {
            if (sun == null) return;
            var light = args.lightEstimation;
            if (light.averageBrightness.HasValue) sun.intensity = Mathf.Clamp(light.averageBrightness.Value * 1.6f + 0.2f, 0.3f, 1.6f);
            if (light.colorCorrection.HasValue) sun.color = light.colorCorrection.Value;
        }

        // ------------------------------------------------------------------ moving, resizing, turning

        void HandleGestures()
        {
            if (TwoTouches(out var a, out var b))
            {
                if (EventSystem.current != null && !_twoFingers && EventSystem.current.IsPointerOverGameObject()) return;
                if (!_twoFingers)
                {
                    _twoFingers = true;
                    _dragging = false;
                    _pinchStartDistance = Vector2.Distance(a, b);
                    _pinchStartScale = _userScale;
                    _twistStartVector = b - a;
                    _twistStartYaw = _content.transform.eulerAngles.y;
                }
                SetUserScale(ArMath.PinchScale(_pinchStartScale, _pinchStartDistance, Vector2.Distance(a, b)));
                SetYaw(_twistStartYaw + ArMath.TwistDegrees(_twistStartVector, b - a));
                return;
            }
            if (_twoFingers)
            {
                _twoFingers = false;
                _ignoreUntilRelease = true; // the finger left behind must not drag the model
            }

            var pointer = Pointer.current;
            if (pointer == null) return;
            if (!pointer.press.isPressed) { _dragging = false; _ignoreUntilRelease = false; return; }
            if (_ignoreUntilRelease) return;
            Vector2 position = pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
                _dragging = true;
                _dragStartX = position.x;
                _dragYaw = _content.transform.eulerAngles.y;
                _moveOffset = Vector3.zero;
                if (_moveMode && raycastManager != null && raycastManager.Raycast(position, Hits, TrackableType.PlaneWithinPolygon))
                    _moveOffset = _content.transform.position - Hits[0].pose.position;
            }
            else if (_dragging)
            {
                if (_moveMode)
                {
                    if (raycastManager != null && raycastManager.Raycast(position, Hits, TrackableType.PlaneWithinPolygon))
                    {
                        var target = Hits[0].pose.position + _moveOffset;
                        MoveTo(Vector3.Lerp(_content.transform.position, target, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime)));
                    }
                }
                else
                {
                    SetYaw(_dragYaw - (position.x - _dragStartX) * 0.4f);
                }
            }
        }

        /// <summary>The first two fingers on the screen, when there are at least two.</summary>
        static bool TwoTouches(out Vector2 first, out Vector2 second)
        {
            first = second = Vector2.zero;
            var screen = Touchscreen.current;
            if (screen == null) return false;
            int found = 0;
            foreach (var touch in screen.touches)
            {
                if (!touch.isInProgress) continue;
                if (found == 0) first = touch.position.ReadValue();
                else { second = touch.position.ReadValue(); return true; }
                found++;
            }
            return false;
        }

        /// <summary>Moves the model (and the space ring) along the surface. Public so tests can call it.</summary>
        public void MoveTo(Vector3 position)
        {
            if (_content == null) return;
            _content.transform.position = position;
            if (_ring != null) _ring.transform.position = position + Vector3.up * 0.01f;
        }

        public void SetYaw(float degrees)
        {
            if (_content != null) _content.transform.rotation = Quaternion.Euler(0f, degrees, 0f);
        }

        /// <summary>Resizes the model between half and double its real size and updates the height text.</summary>
        public void SetUserScale(float scale)
        {
            if (_content == null) return;
            _userScale = ArMath.ClampUserScale(scale);
            _content.transform.localScale = _baseScale * _userScale;
            SetInfo(ArMath.PlacementInfo(ArRequest.Mode == ArMode.PlaceAvatar, _realHeight, _userScale, ArRequest.RecommendedSpaceM2));
        }

        void ToggleMode()
        {
            _moveMode = !_moveMode;
            RefreshModeLabel();
        }

        void RefreshModeLabel()
        {
            var label = modeButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = _moveMode ? "One finger: Move" : "One finger: Turn";
        }

        /// <summary>The Place here button shows only when the ring is green; the resize and move controls only after placing.</summary>
        void UpdateControls()
        {
            placeButton.gameObject.SetActive(!HasPlaced && _reticleReady);
            controlsRow.SetActive(HasPlaced);
            realSizeButton.gameObject.SetActive(HasPlaced && Mathf.Abs(_userScale - 1f) > 0.02f);
        }

        /// <summary>Places the model at a world position, turned to face the camera. Public so tests can call it.</summary>
        public void Place(Vector3 position)
        {
            DestroyContent();
            _content = ArRequest.Mode == ArMode.PlaceAvatar
                ? BuildAvatar()
                : BuildPet(out _ring, position);
            _content.transform.position = position;
            var toCamera = arCamera.transform.position - position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude > 0.001f) _content.transform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);

            _baseScale = _content.transform.localScale;
            _userScale = 1f;
            _realHeight = Measure(_content).size.y;
            SetUserScale(1f);
            RefreshModeLabel();
            hintLabel.text = "Pinch to resize · twist two fingers to turn";
            resetButton.gameObject.SetActive(true);
        }

        GameObject BuildPet(out GameObject ring, Vector3 position)
        {
            var root = new GameObject("PlacedPet");
            var pet = PetFactory.Build(ArRequest.Pet);
            pet.transform.SetParent(root.transform, false);
            root.transform.localScale = Vector3.one * ArMath.PetScale(ArRequest.Pet);

            ring = null;
            if (ArRequest.RecommendedSpaceM2 > 0)
            {
                float radius = ArMath.RadiusForArea(ArRequest.RecommendedSpaceM2);
                ring = new GameObject("SpaceRing");
                ring.transform.position = position + Vector3.up * 0.01f;
                var filter = ring.AddComponent<MeshFilter>();
                filter.sharedMesh = MeshFactory.Torus(radius, 0.02f);
                var renderer = ring.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = AvatarMaterials.For(new Color(0.95f, 0.78f, 0.25f), 0.1f);
                ring.transform.localScale = new Vector3(1f, 0.05f, 1f);
            }
            return root;
        }

        GameObject BuildAvatar()
        {
            var root = new GameObject("PlacedAvatar");
            var avatar = AvatarFactory.Build(ArRequest.Avatar ?? AvatarConfig.Default());
            avatar.transform.SetParent(root.transform, false);
            root.transform.localScale = Vector3.one * ArMath.AvatarScale;
            return root;
        }

        static Bounds Measure(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        public void ResetPlacement()
        {
            DestroyContent();
            _stabilizer.Reset();
            _hasPoint = false;
            SetInfo(string.Empty);
            resetButton.gameObject.SetActive(false);
        }

        /// <summary>Shows the info text in its pill, and hides the empty pill before anything is placed.</summary>
        void SetInfo(string text)
        {
            infoLabel.text = text;
            var pill = infoLabel.transform.parent;
            if (pill != null && pill.GetComponent<Canvas>() == null) pill.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        void DestroyContent()
        {
            if (_content != null) Destroy(_content);
            if (_ring != null) Destroy(_ring);
            _content = null;
            _ring = null;
            _dragging = false;
            _twoFingers = false;
            _userScale = 1f;
        }

        void OnDestroy() => DestroyContent();

        void Close() => ArService.Close(ArService.PlaceScene);
    }
}
