using System;
using System.Collections.Generic;
using ArenaSurvivor.Core.Enemies;
using UnityEngine;
using UnityEngine.Pool;

namespace ArenaSurvivor.Core.Weapons
{
    /// <summary>
    /// Owns every projectile: firing, flying, hitting enemies and expiring.
    /// Like <see cref="EnemySystem"/>, all projectiles are pooled and updated in one loop.
    /// Hits are detected against the path travelled this frame (a segment, not a point),
    /// so fast projectiles cannot skip over an enemy on a slow frame.
    /// </summary>
    public sealed class ProjectileSystem
    {
        private readonly EnemySystem _enemies;
        private readonly float _hitRadiusSqr;
        private readonly ObjectPool<Projectile> _pool;
        private readonly List<Projectile> _active = new List<Projectile>();

        /// <param name="enemies">Enemies that projectiles can hit.</param>
        /// <param name="hitRadius">How close a projectile's path must pass to an enemy's centre to hit it
        /// (enemy body radius plus projectile radius).</param>
        public ProjectileSystem(EnemySystem enemies, float hitRadius)
        {
            _enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));

            if (hitRadius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(hitRadius), hitRadius, "Hit radius must be positive.");
            }

            _hitRadiusSqr = hitRadius * hitRadius;
            _pool = new ObjectPool<Projectile>(
                createFunc: () => new Projectile(),
                collectionCheck: true,
                defaultCapacity: 32);
        }

        public IReadOnlyList<Projectile> Active => _active;
        public int ActiveCount => _active.Count;

        /// <summary>A projectile was fired. The Unity side attaches a bullet model to it.</summary>
        public event Action<Projectile> Spawned;

        /// <summary>A projectile hit an enemy at the given point. Used for impact effects.</summary>
        public event Action<Vector3> Hit;

        /// <summary>A projectile hit something or flew its full distance. The Unity side returns its model.</summary>
        public event Action<Projectile> Despawned;

        public void Prewarm(int count)
        {
            var buffer = new List<Projectile>(count);
            for (int i = 0; i < count; i++)
            {
                buffer.Add(_pool.Get());
            }

            foreach (Projectile projectile in buffer)
            {
                _pool.Release(projectile);
            }
        }

        public Projectile Fire(Vector3 origin, Vector3 direction, float speed, float travelDistance, int damage)
        {
            Projectile projectile = _pool.Get();
            projectile.Position = new Vector3(origin.x, 0f, origin.z);
            projectile.Direction = direction;
            projectile.Speed = speed;
            projectile.RemainingDistance = travelDistance;
            projectile.Damage = damage;
            projectile.IsActive = true;
            projectile.ActiveIndex = _active.Count;
            _active.Add(projectile);

            Spawned?.Invoke(projectile);
            return projectile;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            // Backwards: despawning swaps the last projectile into the freed slot, and that one
            // has already been updated this frame.
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Projectile projectile = _active[i];
                float distance = Mathf.Min(projectile.Speed * deltaTime, projectile.RemainingDistance);
                Vector3 start = projectile.Position;
                Vector3 end = start + projectile.Direction * distance;

                if (TryFindHit(start, end, out Enemy enemy, out Vector3 hitPoint))
                {
                    _enemies.ApplyDamage(enemy, projectile.Damage);
                    Hit?.Invoke(hitPoint);
                    Despawn(projectile);
                    continue;
                }

                projectile.Position = end;
                projectile.RemainingDistance -= distance;

                if (projectile.RemainingDistance <= 0f)
                {
                    Despawn(projectile);
                }
            }
        }

        /// <summary>Removes all projectiles. Used when a run ends or restarts.</summary>
        public void Clear()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Despawn(_active[i]);
            }
        }

        private bool TryFindHit(Vector3 start, Vector3 end, out Enemy hitEnemy, out Vector3 hitPoint)
        {
            Vector3 segment = end - start;
            float segmentLengthSqr = segment.sqrMagnitude;
            IReadOnlyList<Enemy> enemies = _enemies.Active;

            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];

                // Closest point on the travelled segment to the enemy's centre.
                float t = segmentLengthSqr > 0f
                    ? Mathf.Clamp01(Vector3.Dot(enemy.Position - start, segment) / segmentLengthSqr)
                    : 0f;
                Vector3 closest = start + segment * t;

                if ((enemy.Position - closest).sqrMagnitude <= _hitRadiusSqr)
                {
                    hitEnemy = enemy;
                    hitPoint = closest;
                    return true;
                }
            }

            hitEnemy = null;
            hitPoint = default;
            return false;
        }

        private void Despawn(Projectile projectile)
        {
            int index = projectile.ActiveIndex;
            int lastIndex = _active.Count - 1;
            Projectile last = _active[lastIndex];
            _active[index] = last;
            last.ActiveIndex = index;
            _active.RemoveAt(lastIndex);

            projectile.IsActive = false;
            projectile.ActiveIndex = -1;
            Despawned?.Invoke(projectile);
            _pool.Release(projectile);
        }
    }
}
