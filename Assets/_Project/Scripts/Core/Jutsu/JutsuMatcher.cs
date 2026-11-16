using System;

namespace HandSeals.Core
{
    /// <summary>Tracks how far the player is through the seal sequence of one jutsu.</summary>
    public sealed class JutsuMatcher
    {
        public JutsuMatcher(Jutsu target)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public Jutsu Target { get; }

        /// <summary>Number of seals performed correctly so far.</summary>
        public int Progress { get; private set; }

        public bool IsCompleted => Progress >= Target.Sequence.Count;

        /// <summary>The seal the player has to make next, or None when the sequence is finished.</summary>
        public SealType Expected => IsCompleted ? SealType.None : Target.Sequence[Progress];

        public MatchResult Apply(SealType seal)
        {
            if (!seal.IsSeal() || IsCompleted)
            {
                return MatchResult.Ignored;
            }

            if (seal == Expected)
            {
                Progress++;
                return IsCompleted ? MatchResult.Completed : MatchResult.Correct;
            }

            // A wrong seal restarts the sequence. If it happens to be the opening seal, it already counts
            // as the first step of the new attempt, so the player does not have to release and repeat it.
            Progress = seal == Target.Sequence[0] && Target.Sequence.Count > 1 ? 1 : 0;
            return MatchResult.Wrong;
        }

        public void Reset()
        {
            Progress = 0;
        }
    }
}
