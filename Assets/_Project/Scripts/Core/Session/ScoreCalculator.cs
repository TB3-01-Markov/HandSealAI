using System;

namespace HandSeals.Core
{
    /// <summary>
    /// Points for a completed jutsu: base score x time bonus (1..2) x combo multiplier (1..2).
    /// </summary>
    public sealed class ScoreCalculator
    {
        /// <summary>Extra multiplier per jutsu in a clean streak after the first one.</summary>
        public const double ComboStep = 0.25;

        /// <summary>The combo multiplier stops growing after this many steps.</summary>
        public const int MaxComboSteps = 4;

        /// <param name="jutsu">The completed jutsu.</param>
        /// <param name="timeLeft">Seconds left on the timer at completion.</param>
        /// <param name="combo">Length of the current streak of jutsu completed without mistakes.</param>
        public int Calculate(Jutsu jutsu, double timeLeft, int combo)
        {
            if (jutsu == null)
            {
                throw new ArgumentNullException(nameof(jutsu));
            }

            double timeShare = timeLeft / jutsu.TimeLimitSeconds;
            timeShare = timeShare < 0 ? 0 : timeShare > 1 ? 1 : timeShare;

            int comboSteps = Math.Min(Math.Max(combo - 1, 0), MaxComboSteps);

            double points = jutsu.BaseScore * (1 + timeShare) * (1 + comboSteps * ComboStep);
            return (int)Math.Round(points, MidpointRounding.AwayFromZero);
        }
    }
}
