namespace HandSeals.Core
{
    public enum SessionState
    {
        /// <summary>No jutsu is being played.</summary>
        Idle = 0,

        /// <summary>3-2-1 before the timer starts.</summary>
        Countdown = 1,

        /// <summary>The timer is running and seals are accepted.</summary>
        WaitingForSeal = 2,

        Paused = 3,

        /// <summary>The whole sequence was performed in time.</summary>
        Completed = 4,

        /// <summary>The time ran out.</summary>
        Failed = 5
    }
}
