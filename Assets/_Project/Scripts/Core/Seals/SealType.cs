namespace HandSeals.Core
{
    /// <summary>
    /// Hand seals the game knows about. The order matches labels.csv of the YOLOX model:
    /// the twelve zodiac seals first, then the extra classes.
    /// </summary>
    public enum SealType
    {
        None = 0,
        Rat = 1,
        Ox = 2,
        Tiger = 3,
        Hare = 4,
        Dragon = 5,
        Snake = 6,
        Horse = 7,
        Ram = 8,
        Monkey = 9,
        Bird = 10,
        Dog = 11,
        Boar = 12,
        Gassho = 13,
        Unknown = 14,
        Mizunoe = 15
    }

    public static class SealTypeExtensions
    {
        /// <summary>True for a real seal; false for "nothing detected" and "unknown gesture".</summary>
        public static bool IsSeal(this SealType seal)
        {
            return seal != SealType.None && seal != SealType.Unknown;
        }
    }
}
