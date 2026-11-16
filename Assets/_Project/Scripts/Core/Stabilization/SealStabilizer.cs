using System;

namespace HandSeals.Core
{
    /// <summary>
    /// Turns noisy per-frame detections into deliberate "the player made this seal" events.
    /// A seal is confirmed once it has been held for <see cref="HoldSeconds"/>; it is confirmed only once
    /// until the hands are released or change to another seal.
    /// </summary>
    public sealed class SealStabilizer
    {
        private double _candidateSince;
        private double _lastSeen;
        private bool _confirmed;

        /// <summary>How long a seal must be held before it counts.</summary>
        public double HoldSeconds { get; set; } = 0.4;

        /// <summary>Detections below this score are treated as "nothing".</summary>
        public float MinScore { get; set; } = 0.6f;

        /// <summary>A detection gap shorter than this does not interrupt the hold.</summary>
        public double GraceSeconds { get; set; } = 0.15;

        /// <summary>The seal currently being held, or None.</summary>
        public SealType Candidate { get; private set; } = SealType.None;

        /// <summary>True once the current candidate has been confirmed.</summary>
        public bool IsConfirmed => Candidate != SealType.None && _confirmed;

        /// <summary>Raised once per held seal.</summary>
        public event Action<SealType> SealConfirmed;

        /// <summary>Feeds the best detection of the latest frame. Pass None when nothing was detected.</summary>
        /// <param name="seal">Seal of the best detection.</param>
        /// <param name="score">Score of the best detection.</param>
        /// <param name="time">Current time in seconds; must not go backwards.</param>
        public void Update(SealType seal, float score, double time)
        {
            SealType observed = seal.IsSeal() && score >= MinScore ? seal : SealType.None;

            if (observed == SealType.None)
            {
                if (Candidate != SealType.None && time - _lastSeen > GraceSeconds)
                {
                    Reset();
                }

                return;
            }

            if (observed != Candidate)
            {
                Candidate = observed;
                _candidateSince = time;
                _confirmed = false;
            }

            _lastSeen = time;

            if (!_confirmed && time - _candidateSince >= HoldSeconds)
            {
                _confirmed = true;
                SealConfirmed?.Invoke(Candidate);
            }
        }

        /// <summary>Hold progress of the current candidate in the range 0..1, for the UI.</summary>
        public double GetHoldProgress(double time)
        {
            if (Candidate == SealType.None)
            {
                return 0;
            }

            if (_confirmed || HoldSeconds <= 0)
            {
                return 1;
            }

            double progress = (time - _candidateSince) / HoldSeconds;
            return progress < 0 ? 0 : progress > 1 ? 1 : progress;
        }

        public void Reset()
        {
            Candidate = SealType.None;
            _confirmed = false;
            _candidateSince = 0;
            _lastSeen = 0;
        }
    }
}
