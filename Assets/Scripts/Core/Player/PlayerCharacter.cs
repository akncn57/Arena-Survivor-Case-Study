using System;
using ArenaSurvivor.Core.Combat;
using UnityEngine;

namespace ArenaSurvivor.Core.Player
{
    /// <summary>
    /// Simulation state of the player: position, facing, velocity and health.
    /// Movement comes from a 2D joystick vector, mapped onto the ground plane (x -> x, y -> z).
    /// The camera does not rotate around the vertical axis, so "joystick up" is always world forward.
    /// </summary>
    public sealed class PlayerCharacter
    {
        private const float MinDirectionSqr = 1e-6f;

        private readonly PlayerConfig _config;
        private readonly float _arenaHalfSize;

        /// <param name="arenaHalfSize">Half the arena width; the player cannot leave the arena.</param>
        public PlayerCharacter(PlayerConfig config, float arenaHalfSize)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            if (arenaHalfSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(arenaHalfSize), arenaHalfSize, "Arena size must be positive.");
            }

            _arenaHalfSize = arenaHalfSize;
            Health = new Health(config.MaxHealth);
        }

        public Health Health { get; }

        /// <summary>Position on the ground plane (y = 0).</summary>
        public Vector3 Position { get; private set; }

        /// <summary>Unit direction the character model faces.</summary>
        public Vector3 Forward { get; private set; } = Vector3.forward;

        /// <summary>World velocity from the last <see cref="Move"/>. Drives the run animation.</summary>
        public Vector3 Velocity { get; private set; }

        public bool IsMoving => Velocity.sqrMagnitude > MinDirectionSqr;

        /// <summary>
        /// Moves by joystick input. Input longer than 1 is clamped, so diagonals are not faster.
        /// Faces the movement direction; <see cref="AimAt"/> may override the facing afterwards.
        /// </summary>
        public void Move(float deltaTime, Vector2 input)
        {
            if (Health.IsDead || deltaTime <= 0f)
            {
                Velocity = Vector3.zero;
                return;
            }

            input = Vector2.ClampMagnitude(input, 1f);
            Velocity = new Vector3(input.x, 0f, input.y) * _config.MoveSpeed;

            Vector3 next = Position + Velocity * deltaTime;
            Position = new Vector3(
                Mathf.Clamp(next.x, -_arenaHalfSize, _arenaHalfSize),
                0f,
                Mathf.Clamp(next.z, -_arenaHalfSize, _arenaHalfSize));

            if (IsMoving)
            {
                Forward = Velocity.normalized;
            }
        }

        /// <summary>Turns the player towards a point, e.g. the current target, so the rifle points at it.</summary>
        public void AimAt(Vector3 point)
        {
            Vector3 direction = point - Position;
            direction.y = 0f;

            if (direction.sqrMagnitude > MinDirectionSqr)
            {
                Forward = direction.normalized;
            }
        }

        /// <summary>Puts the player back at a position with full health. Used at the start of every run.</summary>
        public void Reset(Vector3 position)
        {
            Position = new Vector3(position.x, 0f, position.z);
            Forward = Vector3.forward;
            Velocity = Vector3.zero;
            Health.Reset();
        }
    }
}
