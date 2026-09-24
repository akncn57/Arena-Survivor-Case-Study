using System;

namespace ArenaSurvivor.Core.Combat
{
    /// <summary>
    /// Hit points of the player or an enemy. Damage below zero is clamped, and a dead
    /// target ignores further damage, so <see cref="Died"/> fires exactly once per life.
    /// </summary>
    public sealed class Health
    {
        public Health(int max)
        {
            Reset(max);
        }

        public int Max { get; private set; }
        public int Current { get; private set; }
        public bool IsDead => Current <= 0;

        /// <summary>While true, <see cref="TakeDamage"/> is ignored. Used by the benchmark run.</summary>
        public bool IsInvulnerable { get; set; }

        /// <summary>Health from 0 to 1, for health bars.</summary>
        public float Normalized => (float)Current / Max;

        /// <summary>Raised with the damage amount actually applied. Used for hit feedback.</summary>
        public event Action<int> Damaged;
        public event Action Died;

        /// <summary>Raised with the amount actually restored by <see cref="Heal"/>.</summary>
        public event Action<int> Healed;

        public void TakeDamage(int amount)
        {
            if (amount <= 0 || IsDead || IsInvulnerable)
            {
                return;
            }

            int applied = Math.Min(amount, Current);
            Current -= applied;
            Damaged?.Invoke(applied);

            if (IsDead)
            {
                Died?.Invoke();
            }
        }

        /// <summary>Restores health up to <see cref="Max"/>. A dead target cannot be healed.</summary>
        /// <returns>The amount actually restored.</returns>
        public int Heal(int amount)
        {
            if (amount <= 0 || IsDead)
            {
                return 0;
            }

            int applied = Math.Min(amount, Max - Current);
            if (applied <= 0)
            {
                return 0;
            }

            Current += applied;
            Healed?.Invoke(applied);
            return applied;
        }

        /// <summary>
        /// Raises <see cref="Max"/> and heals by the same amount, so the missing health stays the same
        /// (a max health upgrade feels like a bonus, not like losing health). A dead target is left dead.
        /// </summary>
        public void IncreaseMax(int amount)
        {
            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Increase must be positive.");
            }

            Max += amount;
            if (!IsDead)
            {
                Current += amount;
            }
        }

        /// <summary>Refills to <see cref="Max"/>. Used on respawn / replay.</summary>
        public void Reset()
        {
            Current = Max;
        }

        public void Reset(int max)
        {
            if (max <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(max), max, "Max health must be positive.");
            }

            Max = max;
            Current = max;
        }
    }
}
