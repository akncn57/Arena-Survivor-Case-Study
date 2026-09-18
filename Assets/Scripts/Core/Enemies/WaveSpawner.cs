using System;
using ArenaSurvivor.Core.Difficulty;
using UnityEngine;

namespace ArenaSurvivor.Core.Enemies
{
    /// <summary>
    /// Decides when and where enemies appear. Every spawn interval it spawns one wave on a ring
    /// around the player (outside the camera view), using the current difficulty and run progress.
    /// Waves are trimmed so the number of alive enemies never exceeds the difficulty's cap.
    /// </summary>
    public sealed class WaveSpawner
    {
        private readonly EnemySystem _enemies;
        private readonly float _spawnRadius;
        private readonly float _arenaHalfSize;
        private System.Random _random;

        private DifficultyConfig _difficulty;
        private float _timeUntilNextWave;

        /// <param name="enemies">System that creates the enemies.</param>
        /// <param name="spawnRadius">Distance from the player at which enemies appear.</param>
        /// <param name="arenaHalfSize">Half the arena width; spawn points are clamped inside the arena.</param>
        /// <param name="random">Random source. Pass a seeded instance in tests for repeatable results.</param>
        public WaveSpawner(EnemySystem enemies, float spawnRadius, float arenaHalfSize, System.Random random)
        {
            _enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));
            _random = random ?? throw new ArgumentNullException(nameof(random));

            if (spawnRadius <= 0f || arenaHalfSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(spawnRadius), "Spawn radius and arena size must be positive.");
            }

            _spawnRadius = spawnRadius;
            _arenaHalfSize = arenaHalfSize;
        }

        /// <summary>Prepares a new run with the chosen difficulty. The first wave spawns on the next tick.</summary>
        /// <param name="random">Optional new random source, e.g. a freshly seeded one for a repeatable run.</param>
        public void Begin(DifficultyConfig difficulty, System.Random random = null)
        {
            _difficulty = difficulty ?? throw new ArgumentNullException(nameof(difficulty));
            _timeUntilNextWave = 0f;

            if (random != null)
            {
                _random = random;
            }
        }

        /// <returns>Number of enemies spawned this tick.</returns>
        public int Tick(float deltaTime, float progress, Vector3 playerPosition)
        {
            if (_difficulty == null)
            {
                throw new InvalidOperationException("Call Begin() before Tick().");
            }

            _timeUntilNextWave -= deltaTime;
            if (_timeUntilNextWave > 0f)
            {
                return 0;
            }

            _timeUntilNextWave = _difficulty.GetSpawnInterval(progress);

            int freeSlots = _difficulty.MaxAliveEnemies - _enemies.AliveCount;
            int count = Math.Min(_difficulty.GetWaveSize(progress), freeSlots);

            for (int i = 0; i < count; i++)
            {
                _enemies.Spawn(GetSpawnPosition(playerPosition));
            }

            return Math.Max(0, count);
        }

        private Vector3 GetSpawnPosition(Vector3 center)
        {
            float angle = (float)(_random.NextDouble() * Math.PI * 2.0);
            float x = center.x + Mathf.Cos(angle) * _spawnRadius;
            float z = center.z + Mathf.Sin(angle) * _spawnRadius;

            // Near the arena edge the ring leaves the arena; clamping keeps enemies inside.
            return new Vector3(
                Mathf.Clamp(x, -_arenaHalfSize, _arenaHalfSize),
                0f,
                Mathf.Clamp(z, -_arenaHalfSize, _arenaHalfSize));
        }
    }
}
