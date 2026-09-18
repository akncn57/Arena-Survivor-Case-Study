using System;
using UnityEngine;

namespace ArenaSurvivor.Core.World
{
    /// <summary>
    /// Arena-wide settings that are not specific to the player, an enemy type or a weapon.
    /// Plain serializable class, edited on the scene's bootstrap component.
    /// </summary>
    [Serializable]
    public sealed class WorldConfig
    {
        [Tooltip("Seconds the player must survive to win.")]
        [SerializeField, Min(1f)] private float runDuration = 180f;

        [Tooltip("Half the arena width. The arena is a square centred on the origin.")]
        [SerializeField, Min(1f)] private float arenaHalfSize = 20f;

        [Tooltip("Distance from the player at which enemies appear. Should be outside the camera view.")]
        [SerializeField, Min(1f)] private float spawnRadius = 18f;

        [Tooltip("How close a bullet must pass to an enemy's centre to hit it (enemy radius + bullet radius).")]
        [SerializeField, Min(0.01f)] private float projectileHitRadius = 0.6f;

        [Tooltip("Enemies created up front, so spawning does not allocate during play.")]
        [SerializeField, Min(0)] private int enemyPrewarm = 150;

        [Tooltip("Bullets created up front, so firing does not allocate during play.")]
        [SerializeField, Min(0)] private int projectilePrewarm = 32;

        public WorldConfig()
        {
        }

        public WorldConfig(float runDuration, float arenaHalfSize, float spawnRadius, float projectileHitRadius,
            int enemyPrewarm = 0, int projectilePrewarm = 0)
        {
            if (runDuration <= 0f || arenaHalfSize <= 0f || spawnRadius <= 0f || projectileHitRadius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(runDuration), "Duration, sizes and radii must be positive.");
            }

            if (enemyPrewarm < 0 || projectilePrewarm < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(enemyPrewarm), "Prewarm counts cannot be negative.");
            }

            this.runDuration = runDuration;
            this.arenaHalfSize = arenaHalfSize;
            this.spawnRadius = spawnRadius;
            this.projectileHitRadius = projectileHitRadius;
            this.enemyPrewarm = enemyPrewarm;
            this.projectilePrewarm = projectilePrewarm;
        }

        public float RunDuration => runDuration;
        public float ArenaHalfSize => arenaHalfSize;
        public float SpawnRadius => spawnRadius;
        public float ProjectileHitRadius => projectileHitRadius;
        public int EnemyPrewarm => enemyPrewarm;
        public int ProjectilePrewarm => projectilePrewarm;
    }
}
