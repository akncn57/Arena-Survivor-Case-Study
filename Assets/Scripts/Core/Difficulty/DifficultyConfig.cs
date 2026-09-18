using System;
using UnityEngine;

namespace ArenaSurvivor.Core.Difficulty
{
    /// <summary>
    /// Spawn tuning for one difficulty level. Values ramp linearly from their "start" value at the
    /// beginning of the run to their "end" value when the timer finishes, so the pressure grows over time.
    /// Plain serializable class: edited in the Inspector through <see cref="DifficultySettings"/>,
    /// and constructed directly in tests.
    /// </summary>
    [Serializable]
    public sealed class DifficultyConfig
    {
        [Tooltip("Seconds between two waves at the start of the run.")]
        [SerializeField, Min(0.1f)] private float startSpawnInterval = 3f;

        [Tooltip("Seconds between two waves at the end of the run.")]
        [SerializeField, Min(0.1f)] private float endSpawnInterval = 1f;

        [Tooltip("Enemies spawned per wave at the start of the run.")]
        [SerializeField, Min(1)] private int startWaveSize = 3;

        [Tooltip("Enemies spawned per wave at the end of the run.")]
        [SerializeField, Min(1)] private int endWaveSize = 10;

        [Tooltip("Hard cap on enemies alive at the same time. Waves are trimmed to stay under it.")]
        [SerializeField, Min(1)] private int maxAliveEnemies = 60;

        public DifficultyConfig()
        {
        }

        public DifficultyConfig(float startSpawnInterval, float endSpawnInterval,
            int startWaveSize, int endWaveSize, int maxAliveEnemies)
        {
            if (startSpawnInterval <= 0f || endSpawnInterval <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(startSpawnInterval), "Spawn intervals must be positive.");
            }

            if (startWaveSize < 1 || endWaveSize < 1 || maxAliveEnemies < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(startWaveSize), "Wave sizes and max alive must be at least 1.");
            }

            this.startSpawnInterval = startSpawnInterval;
            this.endSpawnInterval = endSpawnInterval;
            this.startWaveSize = startWaveSize;
            this.endWaveSize = endWaveSize;
            this.maxAliveEnemies = maxAliveEnemies;
        }

        public int MaxAliveEnemies => maxAliveEnemies;

        /// <param name="progress">Run progress, 0 = start, 1 = end. Clamped.</param>
        public float GetSpawnInterval(float progress)
        {
            return Mathf.Lerp(startSpawnInterval, endSpawnInterval, Mathf.Clamp01(progress));
        }

        /// <param name="progress">Run progress, 0 = start, 1 = end. Clamped.</param>
        public int GetWaveSize(float progress)
        {
            return Mathf.RoundToInt(Mathf.Lerp(startWaveSize, endWaveSize, Mathf.Clamp01(progress)));
        }
    }
}
