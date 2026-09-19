using System;
using UnityEngine;

namespace ArenaSurvivor.Core.Presentation
{
    /// <summary>
    /// Small directional camera kick for each shot: the camera jumps back, away from the shot direction,
    /// and springs back quickly. Unlike <see cref="CameraShake"/> it is not random, so frequent shots read as
    /// weapon recoil instead of a constant tremble, and the stronger damage shake stays recognisable.
    /// </summary>
    public sealed class CameraRecoil
    {
        private readonly float _distance;
        private readonly float _returnSharpness;

        /// <param name="distance">How far a shot pushes the camera, in world units.</param>
        /// <param name="returnSharpness">How fast the camera returns; the kick shrinks by e^(-sharpness * t).</param>
        public CameraRecoil(float distance, float returnSharpness)
        {
            if (distance < 0f || returnSharpness <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(distance), "Distance cannot be negative; sharpness must be positive.");
            }

            _distance = distance;
            _returnSharpness = returnSharpness;
        }

        /// <summary>Offset to add to the camera position this frame.</summary>
        public Vector3 Offset { get; private set; }

        /// <summary>
        /// Kicks the camera away from <paramref name="shotDirection"/> (ground plane only). Replaces the current kick
        /// instead of adding to it, so rapid fire never pushes the camera further than one kick.
        /// </summary>
        public void Kick(Vector3 shotDirection)
        {
            shotDirection.y = 0f;
            if (shotDirection.sqrMagnitude < 1e-6f)
            {
                return;
            }

            Offset = -shotDirection.normalized * _distance;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            // Frame-rate independent exponential return.
            Offset *= Mathf.Exp(-_returnSharpness * deltaTime);
            if (Offset.sqrMagnitude < 1e-8f)
            {
                Offset = Vector3.zero;
            }
        }

        /// <summary>Clears the kick immediately, e.g. when a new run starts.</summary>
        public void Reset()
        {
            Offset = Vector3.zero;
        }
    }
}
