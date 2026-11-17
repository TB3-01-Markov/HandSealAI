using System;
using System.Globalization;

namespace HandSeals.Game
{
    /// <summary>Number formats used on screen; the same on every phone regardless of its language.</summary>
    internal static class UiFormat
    {
        public static string Seconds(double seconds)
        {
            return seconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";
        }

        public static string Percent(float fraction)
        {
            return ((int)Math.Round(fraction * 100f)).ToString(CultureInfo.InvariantCulture) + "%";
        }
    }
}
