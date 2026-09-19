using System;
using System.Collections.Generic;
using ArenaSurvivor.Core.Combat;
using ArenaSurvivor.Core.Spatial;
using UnityEngine;
using UnityEngine.Pool;

namespace ArenaSurvivor.Core.Enemies
{
    /// <summary>
    /// Owns every enemy: spawning, chasing the player, attacking, taking damage and dying.
    /// All enemies are updated in one loop inside <see cref="Tick"/> instead of one Update() per enemy.
    /// Enemy objects are pooled, so spawning and killing enemies allocates nothing after warm-up.
    /// </summary>
    public sealed class EnemySystem
    {
        // Absorbs float rounding when an enemy stops exactly at the range edge.
        private const float RangeTolerance = 1e-4f;

        private readonly EnemyConfig _config;
        private readonly Health _target;
        private readonly ObjectPool<Enemy> _pool;
        private readonly List<Enemy> _active = new List<Enemy>();
        private readonly SpatialGrid _grid;
        private Vector3[] _push = new Vector3[64];

        /// <param name="config">Stats shared by all enemies.</param>
        /// <param name="target">The player's health; enemies in range damage it.</param>
        /// <param name="arenaHalfSize">Half the arena width, for the neighbour grid used by separation.</param>
        public EnemySystem(EnemyConfig config, Health target, float arenaHalfSize = 50f)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _target = target ?? throw new ArgumentNullException(nameof(target));
            if (_config.HasSeparation)
            {
                // Cells as large as the separation radius: every neighbour is in the 3 x 3 cells around an enemy.
                _grid = new SpatialGrid(arenaHalfSize, _config.SeparationRadius);
            }

            _pool = new ObjectPool<Enemy>(
                createFunc: () => new Enemy(_config.MaxHealth),
                collectionCheck: true,
                defaultCapacity: 64);
        }

        public IReadOnlyList<Enemy> Active => _active;
        public int AliveCount => _active.Count;

        /// <summary>Distance checks done by the last separation pass. Used by tests and profiling.</summary>
        public int LastSeparationChecks { get; private set; }

        /// <summary>An enemy entered the arena. The Unity side attaches a model to it.</summary>
        public event Action<Enemy> Spawned;

        /// <summary>An enemy was killed by the player. Counts as a kill. Raised before <see cref="Despawned"/>.</summary>
        public event Action<Enemy> Died;

        /// <summary>An enemy left the arena (killed or cleared). The Unity side returns its model to the pool.</summary>
        public event Action<Enemy> Despawned;

        /// <summary>Creates enemies up front so the first waves do not allocate during gameplay.</summary>
        public void Prewarm(int count)
        {
            var buffer = new List<Enemy>(count);
            for (int i = 0; i < count; i++)
            {
                buffer.Add(_pool.Get());
            }

            foreach (Enemy enemy in buffer)
            {
                _pool.Release(enemy);
            }
        }

        public Enemy Spawn(Vector3 position)
        {
            Enemy enemy = _pool.Get();
            enemy.Position = new Vector3(position.x, 0f, position.z);
            enemy.Forward = Vector3.forward;
            enemy.Health.Reset();
            enemy.AttackCooldown = 0f;
            enemy.IsInAttackRange = false;
            enemy.IsActive = true;
            enemy.ActiveIndex = _active.Count;
            _active.Add(enemy);

            Spawned?.Invoke(enemy);
            return enemy;
        }

        /// <summary>Moves every enemy towards the player and lets the ones in range attack.</summary>
        public void Tick(float deltaTime, Vector3 targetPosition)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            float step = _config.MoveSpeed * deltaTime;
            float range = _config.AttackRange;
            int damageToTarget = 0;

            for (int i = 0; i < _active.Count; i++)
            {
                Enemy enemy = _active[i];

                Vector3 toTarget = targetPosition - enemy.Position;
                toTarget.y = 0f;
                float distance = toTarget.magnitude;

                if (distance > 0.0001f)
                {
                    enemy.Forward = toTarget / distance;
                }

                if (distance > range)
                {
                    // Move, but stop at the edge of the attack range instead of walking into the player.
                    float move = Mathf.Min(step, distance - range);
                    enemy.Position += enemy.Forward * move;
                    distance -= move;
                }

                // Checked after moving, so an enemy that arrives this frame can attack this frame.
                enemy.IsInAttackRange = distance <= range + RangeTolerance;

                enemy.AttackCooldown = Mathf.Max(0f, enemy.AttackCooldown - deltaTime);

                if (enemy.IsInAttackRange && enemy.AttackCooldown <= 0f)
                {
                    damageToTarget += _config.ContactDamage;
                    enemy.AttackCooldown = _config.AttackInterval;
                }
            }

            if (_config.HasSeparation && _active.Count > 1)
            {
                ApplySeparation();
            }

            // Applied after the loop: the damage may kill the player, and listeners reacting to that
            // (e.g. clearing all enemies) must not modify the list while we iterate it.
            _target.TakeDamage(damageToTarget);
        }

        /// <summary>
        /// Pushes enemies that stand closer than the separation radius apart, so a crowd spreads around the player
        /// instead of collapsing into one spot. Neighbours come from the grid, so each enemy is compared only with
        /// the few enemies in the 3 x 3 cells around it rather than with every other enemy (O(n) instead of O(n^2)).
        ///
        /// This is a position correction, not a force: each overlapping pair moves apart by half of its overlap
        /// (times the stiffness). A force would have to be tuned against the walking speed, and in a dense crowd the
        /// enemies walking in from behind would still squeeze the front rows together; a position correction removes
        /// the overlap within a few frames whatever the speed. All corrections are computed first and applied
        /// afterwards, so the result does not depend on list order.
        /// </summary>
        private void ApplySeparation()
        {
            int count = _active.Count;
            if (_push.Length < count)
            {
                _push = new Vector3[Math.Max(count, _push.Length * 2)];
            }

            _grid.Clear();
            for (int i = 0; i < count; i++)
            {
                _grid.Insert(i, _active[i].Position);
            }

            float radius = _config.SeparationRadius;
            float radiusSqr = radius * radius;
            int checks = 0;

            for (int i = 0; i < count; i++)
            {
                Vector3 position = _active[i].Position;
                Vector3 push = Vector3.zero;

                int minX = _grid.CellCoord(position.x - radius), maxX = _grid.CellCoord(position.x + radius);
                int minZ = _grid.CellCoord(position.z - radius), maxZ = _grid.CellCoord(position.z + radius);

                for (int cz = minZ; cz <= maxZ; cz++)
                {
                    for (int cx = minX; cx <= maxX; cx++)
                    {
                        for (int j = _grid.First(cx, cz); j != -1; j = _grid.Next(j))
                        {
                            if (j == i)
                            {
                                continue;
                            }

                            checks++;
                            Vector3 away = position - _active[j].Position;
                            away.y = 0f;
                            float distanceSqr = away.sqrMagnitude;
                            if (distanceSqr >= radiusSqr)
                            {
                                continue;
                            }

                            if (distanceSqr < 1e-8f)
                            {
                                // Exactly on top of each other: pick a stable direction from the index pair.
                                float angle = (i * 2.399963f) - (j * 0.5f);
                                push += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (radius * 0.5f);
                                continue;
                            }

                            float distance = Mathf.Sqrt(distanceSqr);
                            // Half the overlap: the other enemy of the pair moves the other half.
                            push += away / distance * ((radius - distance) * 0.5f);
                        }
                    }
                }

                _push[i] = push;
            }

            float stiffness = _config.SeparationStiffness;
            for (int i = 0; i < count; i++)
            {
                if (_push[i].sqrMagnitude > 0f)
                {
                    // Capped at half the radius so a very dense crowd cannot fling an enemy across the screen.
                    _active[i].Position += Vector3.ClampMagnitude(_push[i] * stiffness, radius * 0.5f);
                }
            }

            LastSeparationChecks = checks;
        }

        /// <summary>Damages an enemy. Kills and despawns it when its health runs out.</summary>
        public void ApplyDamage(Enemy enemy, int amount)
        {
            if (enemy == null || !enemy.IsActive)
            {
                return;
            }

            enemy.Health.TakeDamage(amount);

            if (enemy.Health.IsDead)
            {
                Died?.Invoke(enemy);
                Despawn(enemy);
            }
        }

        /// <summary>Removes all enemies without counting kills. Used when a run ends or restarts.</summary>
        public void Clear()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Despawn(_active[i]);
            }
        }

        private void Despawn(Enemy enemy)
        {
            // Swap-remove: move the last enemy into this slot so removal is O(1).
            int index = enemy.ActiveIndex;
            int lastIndex = _active.Count - 1;
            Enemy last = _active[lastIndex];
            _active[index] = last;
            last.ActiveIndex = index;
            _active.RemoveAt(lastIndex);

            enemy.IsActive = false;
            enemy.ActiveIndex = -1;
            Despawned?.Invoke(enemy);
            _pool.Release(enemy);
        }
    }
}
