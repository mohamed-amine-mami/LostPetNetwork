using UnityEngine;

namespace LostPetNetwork.Services.Sensors
{
    /// <summary>
    /// Recognises a deliberate shake from raw accelerometer readings (in g). A shake is several strong jolts
    /// close together; a single bump or a phone put down hard does not count. After a shake it stays quiet
    /// for a few seconds so one gesture never fires twice.
    /// </summary>
    public sealed class ShakeDetector
    {
        readonly float _threshold;
        readonly int _jolts;
        readonly float _window;
        readonly float _minGap;
        readonly float _cooldown;

        readonly float[] _times;
        int _count;
        float _lastJolt = float.NegativeInfinity;
        float _blockedUntil = float.NegativeInfinity;

        /// <param name="threshold">Acceleration magnitude (g) above which a reading is a jolt; resting is about 1 g.</param>
        public ShakeDetector(float threshold = 2.2f, int jolts = 4, float window = 1.2f, float minGap = 0.08f, float cooldown = 4f)
        {
            _threshold = threshold;
            _jolts = jolts;
            _window = window;
            _minGap = minGap;
            _cooldown = cooldown;
            _times = new float[jolts];
        }

        /// <summary>Feed one reading with its time in seconds; returns true on the reading that completes a shake.</summary>
        public bool Feed(Vector3 acceleration, float timeSeconds)
        {
            if (timeSeconds < _blockedUntil) return false;
            if (acceleration.magnitude < _threshold) return false;
            if (timeSeconds - _lastJolt < _minGap) return false; // the same jolt seen in several readings
            _lastJolt = timeSeconds;

            if (_count == _jolts)
            {
                for (int i = 1; i < _jolts; i++) _times[i - 1] = _times[i];
                _count--;
            }
            _times[_count++] = timeSeconds;

            if (_count == _jolts && timeSeconds - _times[0] <= _window)
            {
                _count = 0;
                _blockedUntil = timeSeconds + _cooldown;
                return true;
            }
            return false;
        }

        public void Reset()
        {
            _count = 0;
            _lastJolt = float.NegativeInfinity;
            _blockedUntil = float.NegativeInfinity;
        }
    }
}
