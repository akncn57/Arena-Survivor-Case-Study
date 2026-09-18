using System;
using UnityEngine;

namespace ArenaSurvivor.Core.Input
{
    /// <summary>
    /// Maths of a floating virtual joystick: the base appears where the finger touches down,
    /// dragging moves the handle up to <c>radius</c> away, and <see cref="Value"/> is the
    /// resulting direction with strength 0..1. Positions are in any 2D space (canvas units in the game).
    /// </summary>
    public sealed class JoystickModel
    {
        private readonly float _radius;
        private readonly float _deadZone;

        /// <param name="radius">How far the handle can move from the base.</param>
        /// <param name="deadZone">Fraction of the radius (0..1) that is ignored, so a resting thumb does not drift.</param>
        public JoystickModel(float radius, float deadZone)
        {
            if (radius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "Radius must be positive.");
            }

            if (deadZone < 0f || deadZone >= 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(deadZone), deadZone, "Dead zone must be in [0, 1).");
            }

            _radius = radius;
            _deadZone = deadZone;
        }

        public bool IsPressed { get; private set; }

        /// <summary>Where the finger touched down; the joystick base is drawn here.</summary>
        public Vector2 Origin { get; private set; }

        /// <summary>Handle position relative to <see cref="Origin"/>, at most radius long.</summary>
        public Vector2 HandleOffset { get; private set; }

        /// <summary>Movement input: direction with length 0..1. Zero inside the dead zone or when released.</summary>
        public Vector2 Value { get; private set; }

        public void Press(Vector2 position)
        {
            IsPressed = true;
            Origin = position;
            HandleOffset = Vector2.zero;
            Value = Vector2.zero;
        }

        public void Drag(Vector2 position)
        {
            if (!IsPressed)
            {
                return;
            }

            HandleOffset = Vector2.ClampMagnitude(position - Origin, _radius);

            float strength = HandleOffset.magnitude / _radius;
            if (strength <= _deadZone)
            {
                Value = Vector2.zero;
                return;
            }

            // Rescale so strength starts at 0 right outside the dead zone instead of jumping to deadZone.
            float rescaled = (strength - _deadZone) / (1f - _deadZone);
            Value = HandleOffset.normalized * rescaled;
        }

        public void Release()
        {
            IsPressed = false;
            HandleOffset = Vector2.zero;
            Value = Vector2.zero;
        }
    }
}
