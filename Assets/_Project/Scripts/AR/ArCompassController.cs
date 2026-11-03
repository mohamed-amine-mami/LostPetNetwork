using System.Collections.Generic;
using LostPetNetwork.Services.Pets;
using LostPetNetwork.Services.Reports;
using LostPetNetwork.Services.Sensors;
using LostPetNetwork.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LostPetNetwork.AR
{
    /// <summary>
    /// Two views over the phone's camera that use GPS and the compass:
    /// the radar shows pins for reports around you, and the guide shows an arrow toward an SOS alert.
    /// </summary>
    public class ArCompassController : MonoBehaviour
    {
        [SerializeField] Camera arCamera;
        [SerializeField] RectTransform markerRoot;
        [SerializeField] ArMarkerView markerPrefab;
        [SerializeField] RectTransform arrow;
        [SerializeField] TMP_Text titleLabel;
        [SerializeField] TMP_Text statusLabel;
        [SerializeField] TMP_Text distanceLabel;
        [SerializeField] Button closeButton;

        /// <summary>A hand-held phone is about this far above the floor.</summary>
        const float FloorBelowCamera = 1.3f;

        class Target
        {
            public double Latitude, Longitude;
            public ArMarkerView Marker;
        }

        /// <summary>A 3D copy of a nearby report's pet, shown standing in the direction of the report.</summary>
        class PetTarget
        {
            public Target Target;
            public PetReport Report;
            public GameObject Root;
            public bool Placed;
        }

        readonly List<Target> _targets = new List<Target>();
        readonly List<PetTarget> _pets = new List<PetTarget>();
        float _failedAt;
        float _smoothedHeading;
        bool _hasHeading;

        public int MarkerCount => _targets.Count;
        public int PetCount => _pets.Count;
        public int ShownPetCount { get; private set; }

        void Awake() => closeButton.onClick.AddListener(Close);

        void OnEnable()
        {
            SensorHub.Location.StartTracking();
            SensorHub.Heading.StartTracking();
        }

        void OnDisable()
        {
            SensorHub.Location.StopTracking();
            SensorHub.Heading.StopTracking();
        }

        void Start()
        {
            bool guide = ArRequest.Mode == ArMode.Guide;
            titleLabel.text = guide ? "Guide to the alert" : "Pets around you";
            arrow.gameObject.SetActive(guide);
            distanceLabel.transform.parent.gameObject.SetActive(guide); // the label sits inside its own pill

            if (guide && ArRequest.Alert != null)
            {
                var marker = Instantiate(markerPrefab, markerRoot);
                marker.Bind(ArRequest.Alert, "Alert", new Color(0.7f, 0.29f, 0.16f), null);
                _targets.Add(new Target { Latitude = ArRequest.Alert.Latitude, Longitude = ArRequest.Alert.Longitude, Marker = marker });
            }
            else if (!guide)
            {
                foreach (var report in ArRequest.Reports)
                {
                    if (!report.HasLocation) continue;
                    var marker = Instantiate(markerPrefab, markerRoot);
                    var captured = report;
                    marker.Bind(report, report.DisplayName,
                        report.Type == ReportType.Lost ? new Color(0.7f, 0.29f, 0.16f) : new Color(0.12f, 0.3f, 0.23f),
                        () => { ArRequest.OpenAfterClose = captured; ArService.Close(ArService.CompassScene); });
                    var target = new Target { Latitude = report.Latitude, Longitude = report.Longitude, Marker = marker };
                    _targets.Add(target);
                    AddPet(report, target);
                }
            }
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Close(); return; }
            if (ArSessionWatch.Failed(ref _failedAt)) { statusLabel.text = ArSessionWatch.Message; Invoke(nameof(Close), 2.5f); return; }

            var heading = SensorHub.Heading.HeadingDegrees;
            var me = SensorHub.Location.Latest;
            if (!me.HasValue) { statusLabel.text = "Waiting for GPS… go outside for the best signal."; HideAll(); return; }
            if (!heading.HasValue) { statusLabel.text = "The compass isn't available on this device."; HideAll(); return; }

            // Smooth the compass so markers do not jitter.
            if (!_hasHeading) { _smoothedHeading = heading.Value; _hasHeading = true; }
            else _smoothedHeading = Mathf.LerpAngle(_smoothedHeading, heading.Value, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));

            float hfov = Camera.VerticalToHorizontalFieldOfView(arCamera.fieldOfView, arCamera.aspect);
            float width = markerRoot.rect.width;
            float halfRange = Mathf.Max(0f, width / 2f - 8f - markerPrefab.Rect.rect.width / 2f); // keeps whole cards on screen
            statusLabel.text = _targets.Count == 0 ? "No reports with a location nearby." : "Turn around to find them. Tap a pin to open it.";

            ShownPetCount = 0;
            foreach (var p in _pets) UpdatePet(p, me.Value);
            if (ShownPetCount > 0) statusLabel.text = "A 3D pet is near you. Look around and tap it to open the report.";
            HandlePetTap();

            Target nearest = null;
            double nearestMeters = double.MaxValue;
            foreach (var t in _targets)
            {
                double meters = GeoMath.DistanceMeters(me.Value.Latitude, me.Value.Longitude, t.Latitude, t.Longitude);
                double bearing = GeoMath.BearingDegrees(me.Value.Latitude, me.Value.Longitude, t.Latitude, t.Longitude);
                double rel = GeoMath.RelativeBearing(bearing, _smoothedHeading);
                float fraction = CompassMath.ScreenFraction(rel, hfov);
                bool left = rel < -hfov / 2, right = rel > hfov / 2;
                t.Marker.gameObject.SetActive(true);
                t.Marker.Rect.anchoredPosition = new Vector2((fraction - 0.5f) * 2f * halfRange, 0f);
                t.Marker.SetDistance(CompassMath.DistanceText(meters), left, right);
                if (meters < nearestMeters) { nearestMeters = meters; nearest = t; }
            }

            if (ArRequest.Mode == ArMode.Guide && nearest != null)
            {
                double bearing = GeoMath.BearingDegrees(me.Value.Latitude, me.Value.Longitude, nearest.Latitude, nearest.Longitude);
                arrow.localRotation = Quaternion.Euler(0f, 0f, CompassMath.ArrowRotation(GeoMath.RelativeBearing(bearing, _smoothedHeading)));
                bool arrived = CompassMath.HasArrived(nearestMeters);
                distanceLabel.text = arrived ? "You're here. Look around carefully." : CompassMath.DistanceText(nearestMeters) + " away";
            }
        }

        void HideAll()
        {
            foreach (var t in _targets) t.Marker.gameObject.SetActive(false);
            foreach (var p in _pets) p.Root.SetActive(false);
        }

        void AddPet(PetReport report, Target target)
        {
            if (!PetConfig.TryParse(report.PetAvatarJson, out var look)) return;
            var root = new GameObject("Pet " + report.DisplayName);
            var pet = PetFactory.Build(look);
            pet.transform.SetParent(root.transform, false);
            root.transform.localScale = Vector3.one * ArMath.PetScale(look);

            // A box around the model so a tap on the pet can be told from a tap on empty space.
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                float scale = root.transform.localScale.y;
                var box = root.AddComponent<BoxCollider>();
                box.center = (bounds.center - root.transform.position) / scale;
                box.size = bounds.size / scale;
            }
            root.SetActive(false);
            _pets.Add(new PetTarget { Target = target, Report = report, Root = root });
        }

        /// <summary>
        /// Stands the pet in the compass direction of its report, in front of the camera. GPS is only accurate to
        /// a few metres, so the direction is right but the spot is approximate.
        /// </summary>
        void UpdatePet(PetTarget p, GeoFix me)
        {
            double meters = GeoMath.DistanceMeters(me.Latitude, me.Longitude, p.Target.Latitude, p.Target.Longitude);
            if (meters > ArMath.PetShowMeters) { p.Root.SetActive(false); p.Placed = false; return; }

            double bearing = GeoMath.BearingDegrees(me.Latitude, me.Longitude, p.Target.Latitude, p.Target.Longitude);
            double relative = GeoMath.RelativeBearing(bearing, _smoothedHeading);
            var cam = arCamera.transform;
            var direction = Quaternion.Euler(0f, ArMath.CameraYaw(cam) + (float)relative, 0f) * Vector3.forward;
            var wanted = cam.position + direction * ArMath.PetDrawDistance(meters);
            wanted.y = cam.position.y - FloorBelowCamera;

            var t = p.Root.transform;
            t.position = p.Placed ? Vector3.Lerp(t.position, wanted, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime)) : wanted;
            var toCamera = cam.position - t.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude > 0.001f) t.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
            p.Root.SetActive(true);
            p.Placed = true;
            ShownPetCount++;
        }

        void HandlePetTap()
        {
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame || _pets.Count == 0) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            var ray = arCamera.ScreenPointToRay(pointer.position.ReadValue());
            if (!Physics.Raycast(ray, out var hit, 60f)) return;
            foreach (var p in _pets)
            {
                if (hit.collider.gameObject != p.Root) continue;
                ArRequest.OpenAfterClose = p.Report;
                ArService.Close(ArService.CompassScene);
                return;
            }
        }

        void Close() => ArService.Close(ArService.CompassScene);
    }
}
