using UnityEngine;

namespace ArenaSurvivor.Core.Enemies
{
    /// <summary>
    /// Asset wrapper for one enemy type, so stats are edited in the Inspector without touching code.
    /// Holds no logic; see <see cref="EnemyConfig"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "Enemy", menuName = "Arena Survivor/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [SerializeField] private EnemyConfig config = new EnemyConfig();

        public EnemyConfig Config => config;
    }
}
