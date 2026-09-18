using ArenaSurvivor.Core.Enemies;
using UnityEngine;

namespace ArenaSurvivor.Unity.Views
{
    /// <summary>
    /// Draws one enemy. Pooled by <see cref="ViewRegistry{TModel,TView}"/> and synced by the bootstrap;
    /// has no Update() of its own.
    /// </summary>
    public sealed class EnemyView : MonoBehaviour
    {
        public void Sync(Enemy enemy)
        {
            transform.SetPositionAndRotation(enemy.Position, Quaternion.LookRotation(enemy.Forward, Vector3.up));
        }
    }
}
