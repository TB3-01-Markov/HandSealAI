namespace HandSeals.Core
{
    public enum RecognitionHint
    {
        None = 0,

        /// <summary>Nothing has been detected for a while: hands out of frame, too dark, too far.</summary>
        ShowHands = 1,

        /// <summary>Something is detected, but never confidently enough to count.</summary>
        HoldSteady = 2
    }

    /// <summary>
    /// Decides when the player needs a tip because recognition is not getting anywhere.
    /// Pure logic: the caller reports what each processed frame contained and the current time.
    /// </summary>
    public sealed class RecognitionHintAdvisor
    {
        private double _lastAny;
        private double _lastConfident;
        private bool _started;

        /// <summary>How long recognition must be stuck before a hint appears.</summary>
        public double DelaySeconds { get; set; } = 3.0;

        public RecognitionHint Current { get; private set; } = RecognitionHint.None;

        /// <summary>Starts counting afresh, for example when a new task begins.</summary>
        public void Reset(double time)
        {
            _lastAny = time;
            _lastConfident = time;
            _started = true;
            Current = RecognitionHint.None;
        }

        /// <param name="anyDetection">The frame contained at least one detection.</param>
        /// <param name="confident">The best detection was a seal scoring high enough to count.</param>
        /// <param name="time">Current time in seconds.</param>
        public void Update(bool anyDetection, bool confident, double time)
        {
            if (!_started)
            {
                Reset(time);
            }

            if (confident)
            {
                _lastConfident = time;
                _lastAny = time;
            }
            else if (anyDetection)
            {
                _lastAny = time;
            }

            if (time - _lastAny >= DelaySeconds)
            {
                Current = RecognitionHint.ShowHands;
            }
            else if (time - _lastConfident >= DelaySeconds)
            {
                Current = RecognitionHint.HoldSteady;
            }
            else
            {
                Current = RecognitionHint.None;
            }
        }
    }
}
