using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace ArenaSurvivor.Unity.Effects
{
    /// <summary>
    /// Short particle bursts where bullets hit enemies. Every burst comes from a pool and goes back after
    /// <c>lifetime</c> seconds, so no particle system is instantiated or destroyed during play.
    /// </summary>
    public sealed class ImpactEffects
    {
        private struct Active
        {
            public ParticleSystem System;
            public float TimeLeft;
        }

        private readonly ObjectPool<ParticleSystem> _pool;
        private readonly List<Active> _active = new List<Active>();
        private readonly float _lifetime;

        /// <param name="lifetime">Seconds until a burst is returned; at least the particles' longest lifetime.</param>
        public ImpactEffects(ParticleSystem prefab, Transform parent, int prewarm, float lifetime)
        {
            _lifetime = lifetime;
            _pool = new ObjectPool<ParticleSystem>(
                createFunc: () =>
                {
                    ParticleSystem system = Object.Instantiate(prefab, parent);
                    system.gameObject.SetActive(false);
                    return system;
                },
                actionOnGet: system => system.gameObject.SetActive(true),
                actionOnRelease: system =>
                {
                    system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    system.gameObject.SetActive(false);
                },
                actionOnDestroy: system => Object.Destroy(system.gameObject),
                collectionCheck: false,
                defaultCapacity: prewarm);

            var buffer = new List<ParticleSystem>(prewarm);
            for (int i = 0; i < prewarm; i++)
            {
                buffer.Add(_pool.Get());
            }

            foreach (ParticleSystem system in buffer)
            {
                _pool.Release(system);
            }
        }

        public int ActiveCount => _active.Count;

        public void Play(Vector3 position)
        {
            ParticleSystem system = _pool.Get();
            system.transform.position = position;
            system.Play(true);
            _active.Add(new Active { System = system, TimeLeft = _lifetime });
        }

        public void Tick(float deltaTime)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Active effect = _active[i];
                effect.TimeLeft -= deltaTime;
                if (effect.TimeLeft > 0f)
                {
                    _active[i] = effect;
                    continue;
                }

                _pool.Release(effect.System);
                _active[i] = _active[_active.Count - 1];
                _active.RemoveAt(_active.Count - 1);
            }
        }

        public void Clear()
        {
            foreach (Active effect in _active)
            {
                _pool.Release(effect.System);
            }

            _active.Clear();
        }
    }
}
