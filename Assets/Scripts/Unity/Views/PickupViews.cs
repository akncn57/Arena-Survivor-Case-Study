using System;
using ArenaSurvivor.Core.Progression;
using UnityEngine;

namespace ArenaSurvivor.Unity.Views
{
    /// <summary>
    /// Draws the endless mode pickups: one pooled <see cref="ViewRegistry{TModel,TView}"/> per pickup kind
    /// (XP gem, health pack). Connected to <see cref="PickupSystem"/>'s Spawned/Despawned events like the
    /// enemy and bullet views. Pickups hover, bob and spin so they read as collectable items, not scenery.
    /// </summary>
    public sealed class PickupViews
    {
        private readonly ViewRegistry<Pickup, Transform> _experience;
        private readonly ViewRegistry<Pickup, Transform> _health;
        private readonly Settings _settings;
        private readonly Action<Pickup, Transform> _sync;
        private float _time;

        [Serializable]
        public sealed class Settings
        {
            [Tooltip("Height of a pickup's centre above the ground.")]
            public float hoverHeight = 0.45f;

            [Tooltip("How far pickups move up and down.")]
            public float bobAmplitude = 0.12f;

            [Tooltip("Bobbing cycles per second.")]
            public float bobFrequency = 1.5f;

            [Tooltip("Spin in degrees per second.")]
            public float spinSpeed = 120f;

            [Tooltip("Pickup models created up front per kind.")]
            [Min(0)] public int prewarm = 60;
        }

        public PickupViews(Transform experiencePrefab, Transform healthPrefab, Transform parent, Settings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _experience = new ViewRegistry<Pickup, Transform>(experiencePrefab, parent, settings.prewarm);
            _health = new ViewRegistry<Pickup, Transform>(healthPrefab, parent, Mathf.Max(1, settings.prewarm / 10));

            _sync = Sync;
            // Place a freshly shown view before it is drawn, so it never flashes at its old position.
            _experience.Shown += _sync;
            _health.Shown += _sync;
        }

        public void Show(Pickup pickup) => RegistryFor(pickup.Kind).Show(pickup);

        public void Hide(Pickup pickup) => RegistryFor(pickup.Kind).Hide(pickup);

        /// <summary>Moves every visible pickup. Uses scaled time, so pickups freeze while the game is paused.</summary>
        public void Tick(float deltaTime)
        {
            _time += deltaTime;
            _experience.Sync(_sync);
            _health.Sync(_sync);
        }

        private ViewRegistry<Pickup, Transform> RegistryFor(PickupKind kind)
        {
            return kind == PickupKind.Health ? _health : _experience;
        }

        private void Sync(Pickup pickup, Transform view)
        {
            // A phase from the position, so neighbouring gems do not bob in lockstep.
            Vector3 position = pickup.Position;
            float phase = position.x * 1.7f + position.z * 2.3f;
            float bob = Mathf.Sin((_time * _settings.bobFrequency * 2f * Mathf.PI) + phase) * _settings.bobAmplitude;

            position.y = _settings.hoverHeight + bob;
            view.SetPositionAndRotation(position,
                Quaternion.Euler(0f, _time * _settings.spinSpeed + phase * Mathf.Rad2Deg, 0f));
        }
    }
}
