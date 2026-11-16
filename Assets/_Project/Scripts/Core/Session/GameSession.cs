using System;

namespace HandSeals.Core
{
    /// <summary>
    /// One player's run through jutsu: countdown, timer, sequence progress, score and combo.
    /// Pure logic: time comes in through <see cref="Tick"/>, seals through <see cref="OnSealConfirmed"/>.
    /// </summary>
    public sealed class GameSession
    {
        private readonly ScoreCalculator _calculator;
        private JutsuMatcher _matcher;
        private SessionState _stateBeforePause;

        public GameSession(ScoreCalculator calculator = null, double countdownSeconds = 3)
        {
            if (countdownSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(countdownSeconds));
            }

            _calculator = calculator ?? new ScoreCalculator();
            CountdownSeconds = countdownSeconds;
        }

        public double CountdownSeconds { get; }

        public SessionState State { get; private set; } = SessionState.Idle;

        /// <summary>The jutsu being played, or null before the first <see cref="Start"/>.</summary>
        public Jutsu Current => _matcher?.Target;

        /// <summary>Seals of the current sequence performed so far.</summary>
        public int Progress => _matcher?.Progress ?? 0;

        /// <summary>The seal expected next, or None.</summary>
        public SealType Expected => _matcher?.Expected ?? SealType.None;

        public double CountdownLeft { get; private set; }

        public double TimeLeft { get; private set; }

        /// <summary>Seconds spent on the current jutsu since the timer started.</summary>
        public double Elapsed => Current == null ? 0 : Current.TimeLimitSeconds - TimeLeft;

        /// <summary>Wrong seals during the current jutsu.</summary>
        public int Mistakes { get; private set; }

        /// <summary>Jutsu completed in a row without a single wrong seal.</summary>
        public int Combo { get; private set; }

        /// <summary>Points for the most recently completed jutsu.</summary>
        public int LastScore { get; private set; }

        public int TotalScore { get; private set; }

        public event Action<SessionState> StateChanged;

        public event Action<SealType, MatchResult> SealApplied;

        public void Start(Jutsu jutsu)
        {
            if (jutsu == null)
            {
                throw new ArgumentNullException(nameof(jutsu));
            }

            _matcher = new JutsuMatcher(jutsu);
            Mistakes = 0;
            LastScore = 0;
            TimeLeft = jutsu.TimeLimitSeconds;
            CountdownLeft = CountdownSeconds;
            SetState(CountdownSeconds > 0 ? SessionState.Countdown : SessionState.WaitingForSeal);
        }

        /// <summary>Advances the countdown or the timer.</summary>
        public void Tick(double deltaSeconds)
        {
            if (deltaSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            }

            if (State == SessionState.Countdown)
            {
                CountdownLeft -= deltaSeconds;
                if (CountdownLeft <= 0)
                {
                    CountdownLeft = 0;
                    SetState(SessionState.WaitingForSeal);
                }
            }
            else if (State == SessionState.WaitingForSeal)
            {
                TimeLeft -= deltaSeconds;
                if (TimeLeft <= 0)
                {
                    TimeLeft = 0;
                    Combo = 0;
                    SetState(SessionState.Failed);
                }
            }
        }

        /// <summary>Applies a seal the player has held long enough. Ignored unless the timer is running.</summary>
        public MatchResult OnSealConfirmed(SealType seal)
        {
            if (State != SessionState.WaitingForSeal)
            {
                return MatchResult.Ignored;
            }

            MatchResult result = _matcher.Apply(seal);

            if (result == MatchResult.Wrong)
            {
                Mistakes++;
            }

            if (result != MatchResult.Ignored)
            {
                SealApplied?.Invoke(seal, result);
            }

            if (result == MatchResult.Completed)
            {
                Combo = Mistakes == 0 ? Combo + 1 : 0;
                LastScore = _calculator.Calculate(_matcher.Target, TimeLeft, Combo);
                TotalScore += LastScore;
                SetState(SessionState.Completed);
            }

            return result;
        }

        public void Pause()
        {
            if (State == SessionState.Countdown || State == SessionState.WaitingForSeal)
            {
                _stateBeforePause = State;
                SetState(SessionState.Paused);
            }
        }

        public void Resume()
        {
            if (State == SessionState.Paused)
            {
                SetState(_stateBeforePause);
            }
        }

        /// <summary>Leaves the current jutsu and returns to <see cref="SessionState.Idle"/>.</summary>
        public void Abort()
        {
            if (State != SessionState.Idle)
            {
                SetState(SessionState.Idle);
            }
        }

        private void SetState(SessionState state)
        {
            if (State == state)
            {
                return;
            }

            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
