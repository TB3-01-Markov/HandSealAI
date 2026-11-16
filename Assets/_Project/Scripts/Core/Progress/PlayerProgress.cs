using System;
using System.Collections.Generic;

namespace HandSeals.Core
{
    /// <summary>Best completion time for one jutsu. Public fields so that JSON serializers can read it.</summary>
    [Serializable]
    public sealed class BestTime
    {
        public string JutsuId;
        public double Seconds;
    }

    /// <summary>What the game remembers about the player between launches.</summary>
    [Serializable]
    public sealed class PlayerProgress
    {
        public List<BestTime> BestTimes = new List<BestTime>();

        public bool TryGetBestTime(string jutsuId, out double seconds)
        {
            BestTime entry = Find(jutsuId);
            seconds = entry?.Seconds ?? 0;
            return entry != null;
        }

        /// <summary>Records a completion time. Returns true when it is a new record.</summary>
        public bool ReportTime(string jutsuId, double seconds)
        {
            if (string.IsNullOrEmpty(jutsuId))
            {
                throw new ArgumentException("A jutsu id is required.", nameof(jutsuId));
            }

            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds));
            }

            BestTime entry = Find(jutsuId);
            if (entry == null)
            {
                BestTimes.Add(new BestTime { JutsuId = jutsuId, Seconds = seconds });
                return true;
            }

            if (seconds < entry.Seconds)
            {
                entry.Seconds = seconds;
                return true;
            }

            return false;
        }

        private BestTime Find(string jutsuId)
        {
            if (BestTimes == null)
            {
                BestTimes = new List<BestTime>();
            }

            foreach (BestTime entry in BestTimes)
            {
                if (entry != null && entry.JutsuId == jutsuId)
                {
                    return entry;
                }
            }

            return null;
        }
    }
}
