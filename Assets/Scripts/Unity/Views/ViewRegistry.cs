using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace ArenaSurvivor.Unity.Views
{
    /// <summary>
    /// Links simulation objects (e.g. <c>Enemy</c>) to pooled GameObjects that draw them.
    /// <see cref="Show"/> and <see cref="Hide"/> are connected to a system's Spawned/Despawned events;
    /// <see cref="Sync"/> copies simulation state to every visible view once per frame.
    /// GameObjects are deactivated and reused instead of destroyed, so there is no Instantiate/Destroy
    /// cost and no garbage during play.
    /// </summary>
    public sealed class ViewRegistry<TModel, TView> where TView : Component
    {
        private readonly TView _prefab;
        private readonly Transform _parent;
        private readonly ObjectPool<TView> _pool;
        private readonly Dictionary<TModel, TView> _visible = new Dictionary<TModel, TView>();

        public ViewRegistry(TView prefab, Transform parent, int prewarm)
        {
            _prefab = prefab != null ? prefab : throw new ArgumentNullException(nameof(prefab));
            _parent = parent;
            _pool = new ObjectPool<TView>(
                createFunc: Create,
                actionOnGet: view => view.gameObject.SetActive(true),
                actionOnRelease: view => view.gameObject.SetActive(false),
                actionOnDestroy: view => Object.Destroy(view.gameObject),
                collectionCheck: false,
                defaultCapacity: Mathf.Max(1, prewarm));

            Prewarm(prewarm);
        }

        public int VisibleCount => _visible.Count;

        /// <summary>Called right after a view is taken from the pool, before it is first synced.</summary>
        public event Action<TModel, TView> Shown;

        /// <summary>Called right before a view goes back to the pool.</summary>
        public event Action<TModel, TView> Hidden;

        public void Show(TModel model)
        {
            TView view = _pool.Get();
            _visible.Add(model, view);
            Shown?.Invoke(model, view);
        }

        public void Hide(TModel model)
        {
            if (_visible.Remove(model, out TView view))
            {
                Hidden?.Invoke(model, view);
                _pool.Release(view);
            }
        }

        /// <summary>Runs <paramref name="sync"/> for every visible model/view pair.</summary>
        public void Sync(Action<TModel, TView> sync)
        {
            foreach (KeyValuePair<TModel, TView> pair in _visible)
            {
                sync(pair.Key, pair.Value);
            }
        }

        private TView Create()
        {
            TView view = Object.Instantiate(_prefab, _parent);
            view.gameObject.SetActive(false);
            return view;
        }

        private void Prewarm(int count)
        {
            var buffer = new List<TView>(count);
            for (int i = 0; i < count; i++)
            {
                buffer.Add(_pool.Get());
            }

            foreach (TView view in buffer)
            {
                _pool.Release(view);
            }
        }
    }
}
