using System;
using UnityEngine;

namespace ArenaSurvivor.Core.Weapons
{
    /// <summary>
    /// Rifle stats. Plain serializable class: edited in the Inspector through
    /// <see cref="WeaponDefinition"/>, and constructed directly in tests.
    /// </summary>
    [Serializable]
    public sealed class WeaponConfig
    {
        // Projectiles fly a bit further than the targeting range so they still reach a target that moved.
        private const float TravelDistanceFactor = 1.5f;

        [Tooltip("Damage per projectile.")]
        [SerializeField, Min(1)] private int damage = 1;

        [Tooltip("Seconds between two shots.")]
        [SerializeField, Min(0.05f)] private float fireInterval = 0.35f;

        [Tooltip("Enemies closer than this are targeted automatically.")]
        [SerializeField, Min(0.1f)] private float range = 8f;

        [Tooltip("Projectile speed in world units per second.")]
        [SerializeField, Min(0.1f)] private float projectileSpeed = 25f;

        public WeaponConfig()
        {
        }

        public WeaponConfig(int damage, float fireInterval, float range, float projectileSpeed)
        {
            if (damage < 1 || fireInterval <= 0f || range <= 0f || projectileSpeed <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), "Damage must be at least 1, other values positive.");
            }

            this.damage = damage;
            this.fireInterval = fireInterval;
            this.range = range;
            this.projectileSpeed = projectileSpeed;
        }

        public int Damage => damage;
        public float FireInterval => fireInterval;
        public float Range => range;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileTravelDistance => range * TravelDistanceFactor;
    }
}
