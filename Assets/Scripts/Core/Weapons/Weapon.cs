using System;
using System.Collections.Generic;
using ArenaSurvivor.Core.Enemies;
using UnityEngine;

namespace ArenaSurvivor.Core.Weapons
{
    /// <summary>
    /// The player's auto-firing rifle. Every tick it picks the nearest enemy in range and,
    /// when the fire cooldown is over, launches a projectile towards it.
    /// Upgrades change the modifier properties; <see cref="Reset"/> puts them back to the base stats.
    /// </summary>
    public sealed class Weapon
    {
        private readonly WeaponConfig _config;
        private readonly ProjectileSystem _projectiles;
        private float _cooldown;

        public Weapon(WeaponConfig config, ProjectileSystem projectiles)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _projectiles = projectiles ?? throw new ArgumentNullException(nameof(projectiles));
        }

        /// <summary>Nearest enemy in range after the last tick, or null. The player aims at it.</summary>
        public Enemy CurrentTarget { get; private set; }

        /// <summary>Scales the damage per projectile (1 = base damage).</summary>
        public float DamageMultiplier { get; set; } = 1f;

        /// <summary>Scales shots per second (2 = twice as fast, half the fire interval).</summary>
        public float FireRateMultiplier { get; set; } = 1f;

        /// <summary>Scales the targeting range and, with it, how far projectiles fly.</summary>
        public float RangeMultiplier { get; set; } = 1f;

        /// <summary>Projectiles fired per shot in addition to the first one (multishot upgrade).</summary>
        public int ExtraProjectiles { get; set; }

        /// <summary>Damage per projectile after upgrades. Never below 1.</summary>
        public int Damage => Math.Max(1, Mathf.RoundToInt(_config.Damage * DamageMultiplier));

        /// <summary>Seconds between shots after upgrades.</summary>
        public float FireInterval => _config.FireInterval / Mathf.Max(0.01f, FireRateMultiplier);

        public float Range => _config.Range * RangeMultiplier;

        public int ProjectilesPerShot => 1 + Math.Max(0, ExtraProjectiles);

        /// <summary>Raised on every shot. Used for muzzle flash, sound and the shoot animation.</summary>
        public event Action Fired;

        /// <param name="origin">Where projectiles start (the player position on the ground plane).</param>
        /// <param name="enemies">Candidates for targeting, normally <see cref="EnemySystem.Active"/>.</param>
        public void Tick(float deltaTime, Vector3 origin, IReadOnlyList<Enemy> enemies)
        {
            _cooldown = Mathf.Max(0f, _cooldown - deltaTime);
            float range = Range;
            CurrentTarget = Targeting.FindNearest(enemies, origin, range);

            if (CurrentTarget == null || _cooldown > 0f)
            {
                return;
            }

            Vector3 direction = CurrentTarget.Position - origin;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f)
            {
                // Target exactly on top of the player: any direction hits it.
                direction = Vector3.forward;
            }

            direction = direction.normalized;
            int count = ProjectilesPerShot;
            int damage = Damage;
            float travel = WeaponConfig.TravelDistanceFor(range);

            // A fan centred on the target: the middle projectile (or the middle pair) aims at it,
            // the others spread out evenly by the spread angle.
            for (int i = 0; i < count; i++)
            {
                float angle = (i - (count - 1) * 0.5f) * _config.MultishotSpreadDegrees;
                _projectiles.Fire(origin, RotateAroundUp(direction, angle), _config.ProjectileSpeed, travel, damage);
            }

            _cooldown = FireInterval;
            Fired?.Invoke();
        }

        /// <summary>Clears the cooldown, the target and every upgrade for a new run.</summary>
        public void Reset()
        {
            _cooldown = 0f;
            CurrentTarget = null;
            DamageMultiplier = 1f;
            FireRateMultiplier = 1f;
            RangeMultiplier = 1f;
            ExtraProjectiles = 0;
        }

        /// <summary>Rotates a ground-plane direction around the vertical axis (positive = clockwise seen from above).</summary>
        private static Vector3 RotateAroundUp(Vector3 direction, float degrees)
        {
            if (degrees == 0f)
            {
                return direction;
            }

            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector3(direction.x * cos + direction.z * sin, 0f, direction.z * cos - direction.x * sin);
        }
    }
}
