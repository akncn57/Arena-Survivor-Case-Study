using System.Collections.Generic;
using ArenaSurvivor.Core.Enemies;

namespace ArenaSurvivor.Unity.Views
{
    /// <summary>
    /// Keeps a killed enemy's model on screen while its death animation plays, then returns it to the pool.
    /// The simulation removes a dead enemy immediately (it no longer moves, attacks or can be hit);
    /// only the visual lingers.
    /// </summary>
    public sealed class EnemyDeathViews
    {
        private struct Corpse
        {
            public EnemyView View;
            public float TimeLeft;
        }

        private readonly ViewRegistry<Enemy, EnemyView> _views;
        private readonly float _duration;
        private readonly List<Corpse> _corpses = new List<Corpse>();

        /// <param name="duration">Seconds a corpse stays visible before going back to the pool.</param>
        public EnemyDeathViews(ViewRegistry<Enemy, EnemyView> views, float duration)
        {
            _views = views;
            _duration = duration;
        }

        public int Count => _corpses.Count;

        /// <summary>
        /// Connected to <c>EnemySystem.Died</c>, which fires before <c>Despawned</c>. The view is detached here,
        /// so the following <c>Despawned -> Hide</c> finds nothing and leaves it alone.
        /// </summary>
        public void OnEnemyDied(Enemy enemy)
        {
            if (_views.Detach(enemy, out EnemyView view))
            {
                view.PlayDeath();
                _corpses.Add(new Corpse { View = view, TimeLeft = _duration });
            }
        }

        public void Tick(float deltaTime)
        {
            for (int i = _corpses.Count - 1; i >= 0; i--)
            {
                Corpse corpse = _corpses[i];
                corpse.TimeLeft -= deltaTime;
                corpse.View.UpdateFlash(deltaTime);

                if (corpse.TimeLeft > 0f)
                {
                    _corpses[i] = corpse;
                    continue;
                }

                _views.Release(corpse.View);
                _corpses[i] = _corpses[_corpses.Count - 1];
                _corpses.RemoveAt(_corpses.Count - 1);
            }
        }

        /// <summary>Pauses every corpse's death animation (the run ended and the arena is frozen).</summary>
        public void Freeze()
        {
            foreach (Corpse corpse in _corpses)
            {
                corpse.View.SetFrozen(true);
            }
        }

        /// <summary>Returns all corpses to the pool at once, e.g. when a new run starts.</summary>
        public void Clear()
        {
            foreach (Corpse corpse in _corpses)
            {
                _views.Release(corpse.View);
            }

            _corpses.Clear();
        }
    }
}
