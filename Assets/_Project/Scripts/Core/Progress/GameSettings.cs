using System;

namespace HandSeals.Core
{
    /// <summary>
    /// Player-adjustable settings. Public fields so that JSON serializers can read them;
    /// call <see cref="Normalize"/> after loading to repair missing or out-of-range values.
    /// </summary>
    [Serializable]
    public sealed class GameSettings
    {
        public const int CurrentVersion = 1;

        public const float DefaultConfirmScore = 0.6f;
        public const float MinConfirmScore = 0.3f;
        public const float MaxConfirmScore = 0.9f;
        public const float ConfirmScoreStep = 0.05f;

        public const float DefaultHoldSeconds = 0.4f;
        public const float MinHoldSeconds = 0.2f;
        public const float MaxHoldSeconds = 1.0f;
        public const float HoldSecondsStep = 0.1f;

        /// <summary>
        /// Format version. A value below 1 means the settings were never saved
        /// (for example a save file from before settings existed); they are then reset to defaults.
        /// </summary>
        public int Version = CurrentVersion;

        /// <summary>Score a seal must reach before the hold timer starts (0..1).</summary>
        public float ConfirmScore = DefaultConfirmScore;

        /// <summary>How long a seal must be held before it counts.</summary>
        public float HoldSeconds = DefaultHoldSeconds;

        public bool SoundOn = true;

        /// <summary>Run the model with GPU compute shaders when the device supports them.</summary>
        public bool UseGpu = true;

        /// <summary>Debug view of the image exactly as the model receives it.</summary>
        public bool ShowModelInput;

        public GameSettings Clone()
        {
            return (GameSettings)MemberwiseClone();
        }

        /// <summary>Resets unsaved settings to defaults and snaps the numbers to their allowed steps.</summary>
        public void Normalize()
        {
            if (Version < 1)
            {
                ConfirmScore = DefaultConfirmScore;
                HoldSeconds = DefaultHoldSeconds;
                SoundOn = true;
                UseGpu = true;
                ShowModelInput = false;
            }

            Version = CurrentVersion;
            ConfirmScore = Snap(ConfirmScore, MinConfirmScore, MaxConfirmScore, ConfirmScoreStep, DefaultConfirmScore);
            HoldSeconds = Snap(HoldSeconds, MinHoldSeconds, MaxHoldSeconds, HoldSecondsStep, DefaultHoldSeconds);
        }

        /// <summary>Moves the confirm score by whole steps (negative = lower), staying within the limits.</summary>
        public void StepConfirmScore(int steps)
        {
            ConfirmScore = Snap(
                ConfirmScore + steps * ConfirmScoreStep, MinConfirmScore, MaxConfirmScore, ConfirmScoreStep, DefaultConfirmScore);
        }

        /// <summary>Moves the hold time by whole steps (negative = shorter), staying within the limits.</summary>
        public void StepHoldSeconds(int steps)
        {
            HoldSeconds = Snap(
                HoldSeconds + steps * HoldSecondsStep, MinHoldSeconds, MaxHoldSeconds, HoldSecondsStep, DefaultHoldSeconds);
        }

        // Rounds to the nearest point of the grid min, min + step, ..., max. Computed in double and rounded
        // to two decimals so that repeated steps give exactly 0.6 rather than 0.60000004.
        private static float Snap(float value, float min, float max, float step, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                value = fallback;
            }

            int maxIndex = (int)Math.Round((max - (double)min) / step);
            int index = (int)Math.Round((value - (double)min) / step, MidpointRounding.AwayFromZero);
            index = Math.Min(Math.Max(index, 0), maxIndex);

            return (float)Math.Round(min + index * (double)step, 2);
        }
    }
}
