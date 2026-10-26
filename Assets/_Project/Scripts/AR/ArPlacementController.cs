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
    /// Puts a life-size 3D pet (or the user's avatar) on the floor or a table in the real room. Tap a surface to
    /// place it, drag to turn it. For pets a ring on the floor shows the space the pet needs.
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

        static readonly List<ARRaycastHit> Hits = new List<ARRaycastHit>();

        GameObject _content;
        GameObject _ring;
        float _failedAt;
        float _dragStartX;
        float _dragYaw;
        bool _dragging;

        public bool HasPlaced => _content != null;

        void Awake()
        {
            closeButton.onClick.AddListener(Close);
            resetButton.onClick.AddListener(ResetPlacement);
        }

        void OnEnable()
        {
            if (cameraManager != null) cameraManager.frameReceived += OnFrame;
        }

        void OnDisable()
        {
            if (cameraManager != null) cameraManager.frameReceived -= OnFrame;
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
            if (!HasPlaced) hintLabel.text = planeManager.trackables.count == 0
                ? "Move your phone slowly to find the floor or a table…"
                : "Tap a surface to place it";
            HandlePointer();
        }

        /// <summary>Estimated light in the room tints the sun so the model does not look pasted on.</summary>
        void OnFrame(ARCameraFrameEventArgs args)
        {
            if (sun == null) return;
            var light = args.lightEstimation;
            if (light.averageBrightness.HasValue) sun.intensity = Mathf.Clamp(light.averageBrightness.Value * 1.6f + 0.2f, 0.3f, 1.6f);
            if (light.colorCorrection.HasValue) sun.color = light.colorCorrection.Value;
        }

        void HandlePointer()
        {
            var pointer = Pointer.current;
            if (pointer == null) return;
            Vector2 position = pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
                if (!HasPlaced) TryPlace(position);
                else { _dragging = true; _dragStartX = position.x; _dragYaw = _content.transform.eulerAngles.y; }
            }
            else if (pointer.press.isPressed && _dragging && HasPlaced)
            {
                _content.transform.rotation = Quaternion.Euler(0f, _dragYaw - (position.x - _dragStartX) * 0.4f, 0f);
            }
            else if (pointer.press.wasReleasedThisFrame)
            {
                _dragging = false;
            }
        }

        void TryPlace(Vector2 screenPoint)
        {
            if (!raycastManager.Raycast(screenPoint, Hits, TrackableType.PlaneWithinPolygon)) return;
            Place(Hits[0].pose.position);
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

            var bounds = Measure(_content);
            int cm = ArMath.ToCentimetres(bounds.size.y);
            infoLabel.text = ArRequest.Mode == ArMode.PlaceAvatar
                ? $"About {cm} cm tall. Drag to turn."
                : $"About {cm} cm tall (approximate). The ring shows roughly {ArRequest.RecommendedSpaceM2} m² of space. Drag to turn.";
            hintLabel.text = "Walk around it to see if it fits your space";
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
            infoLabel.text = string.Empty;
            resetButton.gameObject.SetActive(false);
        }

        void DestroyContent()
        {
            if (_content != null) Destroy(_content);
            if (_ring != null) Destroy(_ring);
            _content = null;
            _ring = null;
            _dragging = false;
        }

        void OnDestroy() => DestroyContent();

        void Close() => ArService.Close(ArService.PlaceScene);
    }
}
