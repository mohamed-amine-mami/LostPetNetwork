using System;
using UnityEngine;

namespace LostPetNetwork.Services.Sensors
{
    /// <summary>One GPS reading.</summary>
    public readonly struct GeoFix
    {
        public readonly double Latitude;
        public readonly double Longitude;
        public readonly float AccuracyMeters;

        public GeoFix(double latitude, double longitude, float accuracyMeters = 10f)
        {
            Latitude = latitude;
            Longitude = longitude;
            AccuracyMeters = accuracyMeters;
        }
    }

    public interface ILocationProvider
    {
        bool IsAvailable { get; }
        GeoFix? Latest { get; }
        event Action<GeoFix> Updated;
        void StartTracking();
        void StopTracking();
    }

    public interface IHeadingProvider
    {
        bool IsAvailable { get; }
        /// <summary>Degrees clockwise from true north, or null when unknown.</summary>
        float? HeadingDegrees { get; }
        void StartTracking();
        void StopTracking();
    }

    public interface IMotionProvider
    {
        bool IsAvailable { get; }
        /// <summary>Raw accelerometer reading in g (gravity included).</summary>
        Vector3 Acceleration { get; }
        /// <summary>Device orientation from the gyroscope.</summary>
        Quaternion Attitude { get; }
        void StartTracking();
        void StopTracking();
    }

    /// <summary>Test and Editor stand-in for GPS. Real phone implementations are added per feature.</summary>
    public class FakeLocationProvider : ILocationProvider
    {
        public bool IsAvailable => true;
        public GeoFix? Latest { get; private set; }
        public bool IsTracking { get; private set; }
        public event Action<GeoFix> Updated;

        public void StartTracking() => IsTracking = true;
        public void StopTracking() => IsTracking = false;

        public void Push(GeoFix fix)
        {
            Latest = fix;
            Updated?.Invoke(fix);
        }
    }

    public class FakeHeadingProvider : IHeadingProvider
    {
        public bool IsAvailable => true;
        public float? HeadingDegrees { get; set; }
        public bool IsTracking { get; private set; }

        public void StartTracking() => IsTracking = true;
        public void StopTracking() => IsTracking = false;
    }

    public class FakeMotionProvider : IMotionProvider
    {
        public bool IsAvailable => true;
        public Vector3 Acceleration { get; set; } = new Vector3(0f, -1f, 0f);
        public Quaternion Attitude { get; set; } = Quaternion.identity;
        public bool IsTracking { get; private set; }

        public void StartTracking() => IsTracking = true;
        public void StopTracking() => IsTracking = false;
    }

    /// <summary>
    /// Single place that features read sensors from. Defaults report "unavailable" so callers never null-check;
    /// tests and the Editor assign fakes, phone builds will assign the real providers.
    /// </summary>
    public static class SensorHub
    {
        sealed class Unavailable : ILocationProvider, IHeadingProvider, IMotionProvider
        {
            public bool IsAvailable => false;
            public GeoFix? Latest => null;
            public float? HeadingDegrees => null;
            public Vector3 Acceleration => Vector3.zero;
            public Quaternion Attitude => Quaternion.identity;
#pragma warning disable CS0067
            public event Action<GeoFix> Updated;
#pragma warning restore CS0067
            public void StartTracking() { }
            public void StopTracking() { }
        }

        static readonly Unavailable None = new Unavailable();

        public static ILocationProvider Location { get; set; } = None;
        public static IHeadingProvider Heading { get; set; } = None;
        public static IMotionProvider Motion { get; set; } = None;

        public static void Reset()
        {
            Location = None;
            Heading = None;
            Motion = None;
        }
    }
}
