using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace ArenaSurvivor.Core.Progression
{
    /// <summary>
    /// Owns every pickup in the arena: dropping, attracting to the player and collecting.
    /// Same pattern as the enemy and projectile systems: pooled objects, one update loop, events for the views.
    ///
    /// A pickup waits where it dropped until the player comes within the magnet radius; from then on it flies to
    /// the player (even if the player walks away again) and is collected inside the collect radius.
    /// </summary>
    public sealed class PickupSystem
    {
        private readonly PickupConfig _config;
        private readonly ObjectPool<Pickup> _pool;
        private readonly List<Pickup> _active = new List<Pickup>();

        public PickupSystem(PickupConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _pool = new ObjectPool<Pickup>(
                createFunc: () => new Pickup(),
                collectionCheck: true,
                defaultCapacity: 64);
        }

        public IReadOnlyList<Pickup> Active => _active;
        public int ActiveCount => _active.Count;

        /// <summary>Scales the magnet radius. Raised by the magnet upgrade; 1 after <see cref="ResetModifiers"/>.</summary>
        public float MagnetMultiplier { get; set; } = 1f;

        public float MagnetRadius => _config.MagnetRadius * MagnetMultiplier;

        /// <summary>A pickup appeared. The Unity side attaches a model to it.</summary>
        public event Action<Pickup> Spawned;

        /// <summary>The player collected a pickup. Raised before <see cref="Despawned"/>.</summary>
        public event Action<Pickup> Collected;

        /// <summary>A pickup left the arena (collected or cleared). The Unity side returns its model.</summary>
        public event Action<Pickup> Despawned;

        public void Prewarm(int count)
        {
            var buffer = new List<Pickup>(count);
            for (int i = 0; i < count; i++)
            {
                buffer.Add(_pool.Get());
            }

            foreach (Pickup pickup in buffer)
            {
                _pool.Release(pickup);
            }
        }

        /// <summary>
        /// Drops a pickup. When the arena already holds <see cref="PickupConfig.MaxActive"/> pickups, XP is added to an
        /// existing gem instead (no XP is ever lost) and a health pack is skipped; returns the gem or null.
        /// </summary>
        public Pickup Spawn(PickupKind kind, Vector3 position, int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Pickup value must be positive.");
            }

            if (_active.Count >= _config.MaxActive)
            {
                return kind == PickupKind.Experience ? MergeExperience(value) : null;
            }

            Pickup pickup = _pool.Get();
            pickup.Kind = kind;
            pickup.Position = new Vector3(position.x, 0f, position.z);
            pickup.Value = value;
            pickup.IsAttracted = false;
            pickup.IsActive = true;
            pickup.ActiveIndex = _active.Count;
            _active.Add(pickup);

            Spawned?.Invoke(pickup);
            return pickup;
        }

        /// <summary>Attracts pickups in magnet range towards the player and collects the ones that reach the player.</summary>
        public void Tick(float deltaTime, Vector3 playerPosition)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            float magnetSqr = MagnetRadius * MagnetRadius;
            float collectSqr = _config.CollectRadius * _config.CollectRadius;
            float step = _config.MagnetSpeed * deltaTime;

            // Backwards: collecting swaps the last pickup into the freed slot, and that one is already updated.
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Pickup pickup = _active[i];
                Vector3 toPlayer = playerPosition - pickup.Position;
                toPlayer.y = 0f;
                float distanceSqr = toPlayer.sqrMagnitude;

                if (!pickup.IsAttracted && distanceSqr <= magnetSqr)
                {
                    pickup.IsAttracted = true;
                }

                if (pickup.IsAttracted && distanceSqr > collectSqr)
                {
                    float distance = Mathf.Sqrt(distanceSqr);
                    float move = Mathf.Min(step, distance);
                    pickup.Position += toPlayer / distance * move;
                    distanceSqr = (distance - move) * (distance - move);
                }

                if (distanceSqr <= collectSqr)
                {
                    Collected?.Invoke(pickup);
                    Despawn(pickup);
                }
            }
        }

        /// <summary>Puts the magnet multiplier back to 1. Used when a run starts.</summary>
        public void ResetModifiers()
        {
            MagnetMultiplier = 1f;
        }

        /// <summary>Removes all pickups without collecting them. Used when a run ends or restarts.</summary>
        public void Clear()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Despawn(_active[i]);
            }
        }

        private Pickup MergeExperience(int value)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].Kind == PickupKind.Experience)
                {
                    _active[i].Value += value;
                    return _active[i];
                }
            }

            return null;
        }

        private void Despawn(Pickup pickup)
        {
            int index = pickup.ActiveIndex;
            int lastIndex = _active.Count - 1;
            Pickup last = _active[lastIndex];
            _active[index] = last;
            last.ActiveIndex = index;
            _active.RemoveAt(lastIndex);

            pickup.IsActive = false;
            pickup.ActiveIndex = -1;
            Despawned?.Invoke(pickup);
            _pool.Release(pickup);
        }
    }
}
