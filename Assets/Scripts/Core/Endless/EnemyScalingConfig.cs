using System;
using UnityEngine;

namespace ArenaSurvivor.Core.Endless
{
    /// <summary>
    /// How enemies grow stronger the longer an endless run lasts. Every multiplier starts at 1 and grows linearly
    /// per minute survived. Speed is capped so enemies never become faster than an upgraded player can run;
    /// health and damage keep growing, so every endless run ends eventually.
    /// Plain serializable class, part of <see cref="EndlessConfig"/>.
    /// </summary>
    [Serializable]
    public sealed class EnemyScalingConfig
    {
        [Tooltip("Enemy max health grows by this fraction per minute (0.3 = +30% per minute). Applies to new spawns.")]
        [SerializeField, Min(0f)] private float healthPerMinute = 0.3f;

        [Tooltip("Enemy walking speed grows by this fraction per minute.")]
        [SerializeField, Min(0f)] private float speedPerMinute = 0.08f;

        [Tooltip("Enemy walking speed never exceeds base speed times this.")]
        [SerializeField, Min(1f)] private float maxSpeedMultiplier = 1.6f;

        [Tooltip("Enemy attack damage grows by this fraction per minute.")]
        [SerializeField, Min(0f)] private float damagePerMinute = 0.15f;

        public EnemyScalingConfig()
        {
        }

        public EnemyScalingConfig(float healthPerMinute, float speedPerMinute, float maxSpeedMultiplier, float damagePerMinute)
        {
            if (healthPerMinute < 0f || speedPerMinute < 0f || damagePerMinute < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(healthPerMinute), "Growth rates cannot be negative.");
            }

            if (maxSpeedMultiplier < 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSpeedMultiplier), maxSpeedMultiplier, "Speed cap must be at least 1.");
            }

            this.healthPerMinute = healthPerMinute;
            this.speedPerMinute = speedPerMinute;
            this.maxSpeedMultiplier = maxSpeedMultiplier;
            this.damagePerMinute = damagePerMinute;
        }

        public float HealthMultiplier(float elapsedSeconds) => 1f + healthPerMinute * Minutes(elapsedSeconds);

        public float SpeedMultiplier(float elapsedSeconds) =>
            Mathf.Min(maxSpeedMultiplier, 1f + speedPerMinute * Minutes(elapsedSeconds));

        public float DamageMultiplier(float elapsedSeconds) => 1f + damagePerMinute * Minutes(elapsedSeconds);

        private static float Minutes(float elapsedSeconds) => Mathf.Max(0f, elapsedSeconds) / 60f;
    }
}
