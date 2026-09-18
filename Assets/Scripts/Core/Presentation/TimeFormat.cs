using System;

namespace ArenaSurvivor.Core.Presentation
{
    /// <summary>Formats times for the HUD and the result screen.</summary>
    public static class TimeFormat
    {
        // Absorbs float error so 3.0000002 s is shown as 3, not 4.
        private const float Epsilon = 1e-4f;

        /// <summary>
        /// Whole seconds for a countdown, rounded up: 0.2 s left still shows "0:01",
        /// and "0:00" appears only when the time is really over.
        /// </summary>
        public static int CountdownSeconds(float seconds)
        {
            return Math.Max(0, (int)Math.Ceiling(seconds - Epsilon));
        }

        /// <summary>Whole seconds for elapsed time, rounded down: 59.9 s survived is "0:59".</summary>
        public static int ElapsedSeconds(float seconds)
        {
            return Math.Max(0, (int)Math.Floor(seconds + Epsilon));
        }

        /// <summary>"m:ss", e.g. 125 -> "2:05".</summary>
        public static string MinutesSeconds(int totalSeconds)
        {
            totalSeconds = Math.Max(0, totalSeconds);
            return $"{totalSeconds / 60}:{totalSeconds % 60:00}";
        }
    }
}
