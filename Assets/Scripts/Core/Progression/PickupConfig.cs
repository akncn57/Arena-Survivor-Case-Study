using System;
using UnityEngine;

namespace ArenaSurvivor.Core.Progression
{
    /// <summary>
    /// What killed enemies drop and how pickups are collected. Plain serializable class, part of
    /// <see cref="Endless.EndlessConfig"/>; constructed directly in tests.
    /// </summary>
    [Serializable]
    public sealed class PickupConfig
    {
        [Tooltip("XP points in the gem every killed enemy drops.")]
        [SerializeField, Min(1)] private int experiencePerKill = 1;

        [Tooltip("Chance (0..1) that a killed enemy also drops a health pack.")]
        [SerializeField, Range(0f, 1f)] private float healthDropChance = 0.03f;

        [Tooltip("Health restored by one health pack.")]
        [SerializeField, Min(1)] private int healthPerPack = 20;

        [Tooltip("Pickups closer than this to the player are collected.")]
        [SerializeField, Min(0.05f)] private float collectRadius = 0.8f;

        [Tooltip("Pickups closer than this start flying to the player. The magnet upgrade scales it.")]
        [SerializeField, Min(0f)] private float magnetRadius = 3f;

        [Tooltip("Speed of a pickup flying to the player, in world units per second. Must beat the player's speed.")]
        [SerializeField, Min(0.1f)] private float magnetSpeed = 14f;

        [Tooltip("Most pickups lying in the arena at once. Beyond it XP is merged into an existing gem.")]
        [SerializeField, Min(1)] private int maxActive = 250;

        public PickupConfig()
        {
        }

        public PickupConfig(int experiencePerKill, float healthDropChance, int healthPerPack,
            float collectRadius, float magnetRadius, float magnetSpeed, int maxActive)
        {
            if (experiencePerKill < 1 || healthPerPack < 1 || maxActive < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(experiencePerKill), "XP, health per pack and max active must be at least 1.");
            }

            if (healthDropChance < 0f || healthDropChance > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(healthDropChance), healthDropChance, "Drop chance must be in [0, 1].");
            }

            if (collectRadius <= 0f || magnetRadius < 0f || magnetSpeed <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(collectRadius), "Collect radius and magnet speed must be positive, magnet radius not negative.");
            }

            this.experiencePerKill = experiencePerKill;
            this.healthDropChance = healthDropChance;
            this.healthPerPack = healthPerPack;
            this.collectRadius = collectRadius;
            this.magnetRadius = magnetRadius;
            this.magnetSpeed = magnetSpeed;
            this.maxActive = maxActive;
        }

        public int ExperiencePerKill => experiencePerKill;
        public float HealthDropChance => healthDropChance;
        public int HealthPerPack => healthPerPack;
        public float CollectRadius => collectRadius;
        public float MagnetRadius => magnetRadius;
        public float MagnetSpeed => magnetSpeed;
        public int MaxActive => maxActive;
    }
}
