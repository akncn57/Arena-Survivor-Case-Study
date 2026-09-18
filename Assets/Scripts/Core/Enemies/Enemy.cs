using ArenaSurvivor.Core.Combat;
using UnityEngine;

namespace ArenaSurvivor.Core.Enemies
{
    /// <summary>
    /// Simulation state of one enemy. Plain data owned and updated by <see cref="EnemySystem"/>;
    /// the Unity side only reads it to place and animate the enemy's model.
    /// Instances are pooled and reused, so never keep a reference after <see cref="EnemySystem.Despawned"/>.
    /// </summary>
    public sealed class Enemy
    {
        internal Enemy(int maxHealth)
        {
            Health = new Health(maxHealth);
        }

        /// <summary>Position on the ground plane (y = 0).</summary>
        public Vector3 Position { get; internal set; }

        /// <summary>Unit direction the enemy is facing (towards the player).</summary>
        public Vector3 Forward { get; internal set; } = Vector3.forward;

        public Health Health { get; }

        /// <summary>True while close enough to the player to attack. Drives the attack animation.</summary>
        public bool IsInAttackRange { get; internal set; }

        /// <summary>True between spawn and despawn. False while waiting in the pool.</summary>
        public bool IsActive { get; internal set; }

        /// <summary>Seconds until this enemy may attack again.</summary>
        internal float AttackCooldown { get; set; }

        /// <summary>Slot in the system's active list, for O(1) removal.</summary>
        internal int ActiveIndex { get; set; } = -1;
    }
}
