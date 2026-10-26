using System.Collections.Generic;
using LostPetNetwork.Services.Reports;
using LostPetNetwork.Services.Sensors;
using LostPetNetwork.UI;
using TMPro;
using UnityEngine;
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

        class Target
        {
            public double Latitude, Longitude;
            public ArMarkerView Marker;
        }

        readonly List<Target> _targets = new List<Target>();
        float _failedAt;
        float _smoothedHeading;
        bool _hasHeading;

        public int MarkerCount => _targets.Count;

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
                    _targets.Add(new Target { Latitude = report.Latitude, Longitude = report.Longitude, Marker = marker });
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
        }

        void Close() => ArService.Close(ArService.CompassScene);
    }
}
