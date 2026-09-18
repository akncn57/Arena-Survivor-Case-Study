using System;
using UnityEngine;

namespace ArenaSurvivor.Unity.Cameras
{
    /// <summary>
    /// Tilted top-down follow camera. Keeps a fixed offset and angle from the player and follows
    /// with a little smoothing. The camera never rotates around the vertical axis, which keeps
    /// "joystick up" equal to "up the screen".
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
        }

        private readonly Transform _camera;
        private readonly Settings _settings;
        private Vector3 _velocity;

        public FollowCamera(Transform camera, Settings settings)
        {
            _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Follow(Vector3 target, float deltaTime)
        {
            Vector3 desired = target + _settings.offset;
            _camera.position = Vector3.SmoothDamp(_camera.position, desired, ref _velocity, _settings.smoothTime,
                Mathf.Infinity, deltaTime);
        }

        /// <summary>Jumps to the target without smoothing and points the camera at it.</summary>
        public void Snap(Vector3 target)
        {
            _velocity = Vector3.zero;
            _camera.position = target + _settings.offset;
            _camera.rotation = Quaternion.LookRotation(-_settings.offset, Vector3.up);
        }
    }
}
