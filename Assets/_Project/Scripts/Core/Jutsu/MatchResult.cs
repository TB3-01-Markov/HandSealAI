namespace HandSeals.Core
{
    /// <summary>What a confirmed seal meant for the sequence in progress.</summary>
    public enum MatchResult
    {
        /// <summary>Not a seal, or the sequence is already finished.</summary>
        Ignored = 0,

        /// <summary>The expected seal; more seals follow.</summary>
        Correct = 1,

        /// <summary>Not the expected seal; progress was reset.</summary>
        Wrong = 2,

        /// <summary>The expected seal, and it was the last one.</summary>
        Completed = 3
    }
}
