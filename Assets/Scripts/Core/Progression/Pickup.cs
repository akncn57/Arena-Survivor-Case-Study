using UnityEngine;

namespace ArenaSurvivor.Core.Progression
{
    /// <summary>
    /// Simulation state of one pickup lying in the arena (an XP gem or a health pack).
    /// Plain data owned and updated by <see cref="PickupSystem"/>.
    /// Instances are pooled and reused, so never keep a reference after <see cref="PickupSystem.Despawned"/>.
    /// </summary>
    public sealed class Pickup
    {
        internal Pickup()
        {
        }

        public PickupKind Kind { get; internal set; }

        /// <summary>Position on the ground plane (y = 0). The view adds a hover height.</summary>
        public Vector3 Position { get; internal set; }

        /// <summary>XP points or health points, depending on <see cref="Kind"/>.</summary>
        public int Value { get; internal set; }

        /// <summary>True once the player came within magnet range; the pickup then flies to the player.</summary>
        public bool IsAttracted { get; internal set; }

        public bool IsActive { get; internal set; }

        internal int ActiveIndex { get; set; } = -1;
    }
}
