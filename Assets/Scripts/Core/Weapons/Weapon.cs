using System;
using System.Collections.Generic;
using ArenaSurvivor.Core.Enemies;
using UnityEngine;

namespace ArenaSurvivor.Core.Weapons
{
    /// <summary>
    /// The player's auto-firing rifle. Every tick it picks the nearest enemy in range and,
    /// when the fire cooldown is over, launches a projectile towards it.
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

        /// <summary>Raised on every shot. Used for muzzle flash, sound and the shoot animation.</summary>
        public event Action Fired;

        /// <param name="origin">Where projectiles start (the player position on the ground plane).</param>
        /// <param name="enemies">Candidates for targeting, normally <see cref="EnemySystem.Active"/>.</param>
        public void Tick(float deltaTime, Vector3 origin, IReadOnlyList<Enemy> enemies)
        {
            _cooldown = Mathf.Max(0f, _cooldown - deltaTime);
            CurrentTarget = Targeting.FindNearest(enemies, origin, _config.Range);

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

            _projectiles.Fire(origin, direction.normalized, _config.ProjectileSpeed,
                _config.ProjectileTravelDistance, _config.Damage);
            _cooldown = _config.FireInterval;
            Fired?.Invoke();
        }

        /// <summary>Clears the cooldown and target for a new run.</summary>
        public void Reset()
        {
            _cooldown = 0f;
            CurrentTarget = null;
        }
    }
}
