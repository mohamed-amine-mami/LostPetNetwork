namespace LostPetNetwork.AR
{
    /// <summary>What the aiming ring is on: nothing, a rough spot (a loose point or a small surface) or a good surface.</summary>
    public enum ReticleState { None, Rough, Good }

    /// <summary>
    /// Stops the aiming ring from flickering. A new state only takes over once it has been seen for a short while
    /// (and going back to "nothing" takes a little longer), so one bad frame never flips green to amber and back.
    /// </summary>
    public sealed class ReticleStabilizer
    {
        public const float GainSeconds = 0.25f;
        public const float LoseSeconds = 0.4f;

        ReticleState _pending;
        float _pendingTime;

        public ReticleState State { get; private set; } = ReticleState.None;

        /// <summary>Feed what this frame saw and how long the frame took; returns the steady state to show.</summary>
        public ReticleState Feed(ReticleState observed, float deltaSeconds)
        {
            if (observed == State) { _pending = observed; _pendingTime = 0f; return State; }
            if (observed != _pending) { _pending = observed; _pendingTime = 0f; }
            _pendingTime += deltaSeconds;
            if (_pendingTime >= (observed == ReticleState.None ? LoseSeconds : GainSeconds))
            {
                State = observed;
                _pendingTime = 0f;
            }
            return State;
        }

        public void Reset()
        {
            State = ReticleState.None;
            _pending = ReticleState.None;
            _pendingTime = 0f;
        }
    }
}
