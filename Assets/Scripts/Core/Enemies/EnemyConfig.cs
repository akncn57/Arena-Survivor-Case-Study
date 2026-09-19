using System;
using UnityEngine;

namespace ArenaSurvivor.Core.Enemies
{
    /// <summary>
    /// Stats of one enemy type. Plain serializable class: edited in the Inspector through
    /// <see cref="EnemyDefinition"/>, and constructed directly in tests.
    /// </summary>
    [Serializable]
    public sealed class EnemyConfig
    {
        [SerializeField, Min(1)] private int maxHealth = 3;

        [Tooltip("World units per second.")]
        [SerializeField, Min(0f)] private float moveSpeed = 2.5f;

        [Tooltip("Damage dealt to the player per attack.")]
        [SerializeField, Min(1)] private int contactDamage = 10;

        [Tooltip("Seconds between two attacks of the same enemy.")]
        [SerializeField, Min(0.1f)] private float attackInterval = 1f;

        [Tooltip("Distance to the player at which the enemy stops and attacks.")]
        [SerializeField, Min(0.1f)] private float attackRange = 1.2f;

        [Tooltip("Enemies closer than this push each other apart. 0 disables separation.")]
        [SerializeField, Min(0f)] private float separationRadius;

        [Tooltip("Fraction of the overlap between two enemies resolved each frame (0 = off, 1 = fully).")]
        [SerializeField, Range(0f, 1f)] private float separationStiffness;

        public EnemyConfig()
        {
        }

        public EnemyConfig(int maxHealth, float moveSpeed, int contactDamage, float attackInterval, float attackRange,
            float separationRadius = 0f, float separationStiffness = 0f)
        {
            if (maxHealth < 1 || contactDamage < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth), "Health and damage must be at least 1.");
            }

            if (moveSpeed < 0f || attackInterval <= 0f || attackRange <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(moveSpeed), "Speed must be >= 0, interval and range > 0.");
            }

            this.maxHealth = maxHealth;
            this.moveSpeed = moveSpeed;
            this.contactDamage = contactDamage;
            this.attackInterval = attackInterval;
            this.attackRange = attackRange;

            if (separationRadius < 0f || separationStiffness < 0f || separationStiffness > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(separationRadius),
                    "Separation radius cannot be negative; stiffness must be in [0, 1].");
            }

            this.separationRadius = separationRadius;
            this.separationStiffness = separationStiffness;
        }

        public int MaxHealth => maxHealth;
        public float MoveSpeed => moveSpeed;
        public int ContactDamage => contactDamage;
        public float AttackInterval => attackInterval;
        public float AttackRange => attackRange;
        public float SeparationRadius => separationRadius;
        public float SeparationStiffness => separationStiffness;
        public bool HasSeparation => separationRadius > 0f && separationStiffness > 0f;
    }
}
