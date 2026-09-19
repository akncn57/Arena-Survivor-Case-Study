using System;
using UnityEngine;

namespace ArenaSurvivor.Unity.Cameras
{
    /// <summary>
    /// Tilted top-down follow camera. Keeps a fixed offset and angle from the player and follows
    /// with a little smoothing. The camera never rotates around the vertical axis, which keeps
    /// "joystick up" equal to "up the screen". A shake offset can be added on top of the follow position.
    /// </summary>
    public sealed class FollowCamera
    {
        [Serializable]
        public sealed class Settings
        {
            [Tooltip("Camera position relative to the player.")]
            public Vector3 offset = new Vector3(0f, 20f, -11.5f);

            [Tooltip("Seconds to catch up with the player. 0 = rigid follow.")]
            [Min(0f)] public float smoothTime = 0.12f;

            [Header("Shake")]
            [Tooltip("Largest shake offset in world units, reached at full trauma.")]
            [Min(0f)] public float shakeMaxOffset = 1.6f;

            [Tooltip("Trauma lost per second; 1 means full trauma fades in one second.")]
            [Min(0.01f)] public float shakeDecayPerSecond = 1.2f;

            [Tooltip("How fast the shake moves.")]
            [Min(0.01f)] public float shakeFrequency = 8f;

            [Tooltip("Trauma level the shake is raised to when the player takes damage (does not stack).")]
            [Range(0f, 1f)] public float damageTrauma = 0.6f;

            [Tooltip("Trauma level the shake is raised to when the player dies.")]
            [Range(0f, 1f)] public float deathTrauma = 1f;
        }

        private readonly Transform _camera;
        private readonly Settings _settings;
        private Vector3 _velocity;
        private Vector3 _followPosition;

        public FollowCamera(Transform camera, Settings settings)
        {
            _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _followPosition = _camera.position;
        }

        /// <param name="shakeOffset">Added after smoothing. Kept out of the smoothed position,
        /// otherwise the smoothing would absorb the shake.</param>
        public void Follow(Vector3 target, float deltaTime, Vector3 shakeOffset)
        {
            Vector3 desired = target + _settings.offset;
            _followPosition = Vector3.SmoothDamp(_followPosition, desired, ref _velocity, _settings.smoothTime,
                Mathf.Infinity, deltaTime);
            _camera.position = _followPosition + shakeOffset;
        }

        /// <summary>Jumps to the target without smoothing or shake and points the camera at it.</summary>
        public void Snap(Vector3 target)
        {
            _velocity = Vector3.zero;
            _followPosition = target + _settings.offset;
            _camera.position = _followPosition;
            _camera.rotation = Quaternion.LookRotation(-_settings.offset, Vector3.up);
        }
    }
}
