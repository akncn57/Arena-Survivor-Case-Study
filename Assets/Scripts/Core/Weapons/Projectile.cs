using UnityEngine;

namespace ArenaSurvivor.Core.Weapons
{
    /// <summary>
    /// Simulation state of one projectile. Plain data owned and updated by <see cref="ProjectileSystem"/>.
    /// Instances are pooled and reused, so never keep a reference after <see cref="ProjectileSystem.Despawned"/>.
    /// </summary>
    public sealed class Projectile
    {
        internal Projectile()
        {
        }

        /// <summary>Position on the ground plane (y = 0). The view adds the muzzle height.</summary>
        public Vector3 Position { get; internal set; }

        /// <summary>Unit flight direction.</summary>
        public Vector3 Direction { get; internal set; }

        public bool IsActive { get; internal set; }

        internal float Speed { get; set; }
        internal float RemainingDistance { get; set; }
        internal int Damage { get; set; }
        internal int ActiveIndex { get; set; } = -1;
    }
}
