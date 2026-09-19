using System;
using UnityEngine;

namespace ArenaSurvivor.Core.Presentation
{
    /// <summary>
    /// Trauma-based camera shake. Events add "trauma" (0..1), which decays linearly over time.
    /// The shake strength is trauma squared, so small knocks stay subtle and big ones stand out,
    /// and the fade-out feels natural. The direction comes from Perlin noise instead of random numbers,
    /// so the camera sways smoothly rather than jittering.
    /// Besides a position offset it produces a small roll: with a camera far above the arena, a few centimetres
    /// of movement are hard to see, while a slight rotation of the whole image is obvious.
    /// </summary>
    public sealed class CameraShake
    {
        private readonly float _maxOffset;
        private readonly float _maxRollDegrees;
        private readonly float _decayPerSecond;
        private readonly float _frequency;
        private float _time;

        /// <param name="maxOffset">Largest offset in world units, reached at trauma 1.</param>
        /// <param name="decayPerSecond">Trauma lost per second (1 = full trauma fades in one second).</param>
        /// <param name="frequency">How fast the noise is sampled; higher shakes faster.</param>
        /// <param name="maxRollDegrees">Largest roll around the view axis, reached at trauma 1.</param>
        public CameraShake(float maxOffset, float decayPerSecond, float frequency, float maxRollDegrees = 0f)
        {
            if (maxOffset < 0f || maxRollDegrees < 0f || decayPerSecond <= 0f || frequency <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maxOffset), "Offset and roll cannot be negative; decay and frequency must be positive.");
            }

            _maxOffset = maxOffset;
            _maxRollDegrees = maxRollDegrees;
            _decayPerSecond = decayPerSecond;
            _frequency = frequency;
        }

        /// <summary>Current trauma, 0..1.</summary>
        public float Trauma { get; private set; }

        /// <summary>Offset to add to the camera position this frame.</summary>
        public Vector3 Offset { get; private set; }

        /// <summary>Roll around the camera's view axis this frame, in degrees.</summary>
        public float Roll { get; private set; }

        /// <summary>Adds trauma, e.g. 0.3 for a hit. Capped at 1.</summary>
        public void AddTrauma(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            Trauma = Math.Min(1f, Trauma + amount);
        }

        /// <summary>
        /// Raises trauma to at least <paramref name="level"/> without stacking. For events that can repeat quickly
        /// (the player being hit by several enemies in a row): the shake stays at this level instead of piling up
        /// to full strength.
        /// </summary>
        public void RaiseTo(float level)
        {
            Trauma = Math.Max(Trauma, Math.Min(1f, level));
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            _time += deltaTime;
            Trauma = Math.Max(0f, Trauma - _decayPerSecond * deltaTime);

            if (Trauma <= 0f)
            {
                Offset = Vector3.zero;
                Roll = 0f;
                return;
            }

            float shake = Trauma * Trauma;
            float t = _time * _frequency;
            // Independent noise channels (different rows of the noise field), mapped from 0..1 to -1..1.
            float x = Mathf.PerlinNoise(t, 0.37f) * 2f - 1f;
            float z = Mathf.PerlinNoise(t, 7.91f) * 2f - 1f;
            float roll = Mathf.PerlinNoise(t, 15.3f) * 2f - 1f;
            Offset = new Vector3(x, 0f, z) * (shake * _maxOffset);
            Roll = roll * shake * _maxRollDegrees;
        }

        /// <summary>Stops the shake immediately, e.g. when a new run starts.</summary>
        public void Reset()
        {
            Trauma = 0f;
            Offset = Vector3.zero;
            Roll = 0f;
        }
    }
}
