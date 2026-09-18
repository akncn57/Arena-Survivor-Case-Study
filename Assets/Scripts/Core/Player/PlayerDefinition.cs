using UnityEngine;

namespace ArenaSurvivor.Core.Player
{
    /// <summary>Asset wrapper for <see cref="PlayerConfig"/>. Holds no logic.</summary>
    [CreateAssetMenu(fileName = "Player", menuName = "Arena Survivor/Player Definition")]
    public sealed class PlayerDefinition : ScriptableObject
    {
        [SerializeField] private PlayerConfig config = new PlayerConfig();

        public PlayerConfig Config => config;
    }
}
