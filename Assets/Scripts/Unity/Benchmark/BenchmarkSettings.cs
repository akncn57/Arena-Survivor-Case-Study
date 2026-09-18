using System;
using ArenaSurvivor.Core.Difficulty;
using UnityEngine;

namespace ArenaSurvivor.Unity.Benchmark
{
    /// <summary>
    /// The fixed benchmark scenario. Every benchmark run uses exactly these values, so the reference
    /// and the optimized build are measured under the same conditions.
    /// </summary>
    [Serializable]
    public sealed class BenchmarkSettings
    {
        [Tooltip("Spawn tuning for the benchmark; should reach the enemy cap quickly.")]
        public DifficultySettings difficulty;

        [Tooltip("Seconds played before measuring starts (enemies fill the arena, shaders warm up).")]
        [Min(0f)] public float warmupSeconds = 10f;

        [Tooltip("Seconds measured.")]
        [Min(1f)] public float measureSeconds = 60f;

        [Tooltip("Spawn seed; identical in every benchmark run.")]
        public int seed = 12345;

        [Tooltip("Frame rate cap during the benchmark. Higher than the game's 60 so headroom is visible.")]
        [Min(30)] public int targetFrameRate = 120;
    }
}
