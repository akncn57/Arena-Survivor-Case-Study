using System;
using UnityEngine;

namespace ArenaSurvivor.Core.Player
{
    /// <summary>
    /// Player stats. Plain serializable class: edited in the Inspector through
    /// <see cref="PlayerDefinition"/>, and constructed directly in tests.
    /// </summary>
    [Serializable]
    public sealed class PlayerConfig
    {
        [SerializeField, Min(1)] private int maxHealth = 100;

        [Tooltip("World units per second at full joystick tilt.")]
        [SerializeField, Min(0f)] private float moveSpeed = 5f;

        public PlayerConfig()
        {
        }

        public PlayerConfig(int maxHealth, float moveSpeed)
        {
            if (maxHealth < 1 || moveSpeed < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth), "Health must be at least 1 and speed not negative.");
            }

            this.maxHealth = maxHealth;
            this.moveSpeed = moveSpeed;
        }

        public int MaxHealth => maxHealth;
        public float MoveSpeed => moveSpeed;
    }
}
