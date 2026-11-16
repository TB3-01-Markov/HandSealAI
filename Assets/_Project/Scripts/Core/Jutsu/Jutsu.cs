using System;
using System.Collections.Generic;

namespace HandSeals.Core
{
    /// <summary>A technique: a named sequence of seals to perform within a time limit.</summary>
    public sealed class Jutsu
    {
        private readonly SealType[] _sequence;

        public Jutsu(string id, string name, IEnumerable<SealType> sequence, double timeLimitSeconds, int baseScore)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A jutsu needs an id.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A jutsu needs a name.", nameof(name));
            }

            if (sequence == null)
            {
                throw new ArgumentNullException(nameof(sequence));
            }

            _sequence = new List<SealType>(sequence).ToArray();

            if (_sequence.Length == 0)
            {
                throw new ArgumentException("A jutsu needs at least one seal.", nameof(sequence));
            }

            foreach (SealType seal in _sequence)
            {
                if (!seal.IsSeal())
                {
                    throw new ArgumentException($"'{seal}' cannot be part of a sequence.", nameof(sequence));
                }
            }

            if (timeLimitSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(timeLimitSeconds), "The time limit must be positive.");
            }

            if (baseScore < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(baseScore), "The base score cannot be negative.");
            }

            Id = id;
            Name = name;
            TimeLimitSeconds = timeLimitSeconds;
            BaseScore = baseScore;
        }

        public string Id { get; }
        public string Name { get; }
        public IReadOnlyList<SealType> Sequence => _sequence;
        public double TimeLimitSeconds { get; }
        public int BaseScore { get; }

        /// <summary>Builds a jutsu from seal names as they appear in data files ("Tiger", "ox", ...).</summary>
        public static Jutsu Create(string id, string name, IEnumerable<string> sealNames, double timeLimitSeconds, int baseScore)
        {
            if (sealNames == null)
            {
                throw new ArgumentNullException(nameof(sealNames));
            }

            var sequence = new List<SealType>();
            foreach (string sealName in sealNames)
            {
                sequence.Add(ParseSeal(sealName));
            }

            return new Jutsu(id, name, sequence, timeLimitSeconds, baseScore);
        }

        private static SealType ParseSeal(string sealName)
        {
            string trimmed = sealName == null ? string.Empty : sealName.Trim();
            bool looksLikeName = trimmed.Length > 0 && char.IsLetter(trimmed[0]);

            if (looksLikeName
                && Enum.TryParse(trimmed, true, out SealType seal)
                && Enum.IsDefined(typeof(SealType), seal))
            {
                return seal;
            }

            throw new ArgumentException($"Unknown seal '{sealName}'.");
        }
    }
}
