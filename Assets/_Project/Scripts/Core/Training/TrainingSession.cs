using System;
using System.Collections.Generic;

namespace HandSeals.Core
{
    /// <summary>
    /// Practice without a timer: the game names one seal at a time, the player makes it,
    /// and a new random seal follows. Counts correct answers, mistakes and the fastest answer.
    /// </summary>
    public sealed class TrainingSession
    {
        private readonly SealType[] _pool;
        private readonly Random _random;

        /// <param name="pool">Seals to practise; duplicates are ignored.</param>
        /// <param name="random">Source of randomness; pass a seeded instance for repeatable tests.</param>
        public TrainingSession(IEnumerable<SealType> pool, Random random = null)
        {
            if (pool == null)
            {
                throw new ArgumentNullException(nameof(pool));
            }

            var unique = new List<SealType>();
            foreach (SealType seal in pool)
            {
                if (!seal.IsSeal())
                {
                    throw new ArgumentException($"'{seal}' cannot be practised.", nameof(pool));
                }

                if (!unique.Contains(seal))
                {
                    unique.Add(seal);
                }
            }

            if (unique.Count == 0)
            {
                throw new ArgumentException("Training needs at least one seal.", nameof(pool));
            }

            _pool = unique.ToArray();
            _random = random ?? new Random();
            Target = _pool[_random.Next(_pool.Length)];
        }

        /// <summary>The seal the player has to make now.</summary>
        public SealType Target { get; private set; }

        public int CorrectCount { get; private set; }

        public int Mistakes { get; private set; }

        /// <summary>Seconds spent on the current target.</summary>
        public double TargetElapsed { get; private set; }

        /// <summary>Seconds the most recent correct answer took.</summary>
        public double LastSeconds { get; private set; }

        /// <summary>The fastest correct answer so far; meaningful when <see cref="CorrectCount"/> is above zero.</summary>
        public double BestSeconds { get; private set; }

        public void Tick(double deltaSeconds)
        {
            if (deltaSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            }

            TargetElapsed += deltaSeconds;
        }

        /// <summary>
        /// Applies a seal the player has held long enough: Correct moves on to a new target,
        /// Wrong counts a mistake and keeps the target, anything that is not a seal is Ignored.
        /// </summary>
        public MatchResult OnSealConfirmed(SealType seal)
        {
            if (!seal.IsSeal())
            {
                return MatchResult.Ignored;
            }

            if (seal != Target)
            {
                Mistakes++;
                return MatchResult.Wrong;
            }

            LastSeconds = TargetElapsed;
            if (CorrectCount == 0 || LastSeconds < BestSeconds)
            {
                BestSeconds = LastSeconds;
            }

            CorrectCount++;
            PickNextTarget();
            return MatchResult.Correct;
        }

        /// <summary>Moves on to another seal without counting the current one.</summary>
        public void Skip()
        {
            PickNextTarget();
        }

        private void PickNextTarget()
        {
            TargetElapsed = 0;

            if (_pool.Length == 1)
            {
                return;
            }

            // Draw from the pool without the current target, so the same seal never comes twice in a row.
            int current = Array.IndexOf(_pool, Target);
            int next = _random.Next(_pool.Length - 1);
            if (next >= current)
            {
                next++;
            }

            Target = _pool[next];
        }
    }
}
