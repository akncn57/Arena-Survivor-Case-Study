namespace ArenaSurvivor.Core.World
{
    /// <summary>
    /// Optional overrides for one run. The default value is a normal game;
    /// the benchmark uses them to make every measurement run identical.
    /// </summary>
    public struct RunOptions
    {
        /// <summary>Seed for enemy spawn positions. Null keeps the world's own random sequence.</summary>
        public int? Seed;

        /// <summary>The player takes no damage, so the run always lasts its full duration.</summary>
        public bool Invulnerable;

        /// <summary>Run length in seconds. Null uses <see cref="WorldConfig.RunDuration"/>.</summary>
        public float? Duration;

        /// <summary>Do not add this run's kills to the lifetime total (test runs must not change the save).</summary>
        public bool SkipProgress;
    }
}
