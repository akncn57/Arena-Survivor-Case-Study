using UnityEngine;

namespace ArenaSurvivor.Core.Endless
{
    /// <summary>Asset wrapper for <see cref="EndlessConfig"/>, so the endless mode is tuned in the Inspector. Holds no logic.</summary>
    [CreateAssetMenu(fileName = "Endless", menuName = "Arena Survivor/Endless Settings")]
    public sealed class EndlessSettings : ScriptableObject
    {
        [SerializeField] private EndlessConfig config = new EndlessConfig();

        public EndlessConfig Config => config;
    }
}
