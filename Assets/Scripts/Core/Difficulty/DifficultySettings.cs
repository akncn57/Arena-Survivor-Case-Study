using UnityEngine;

namespace ArenaSurvivor.Core.Difficulty
{
    /// <summary>
    /// Asset wrapper for one difficulty level (Easy/Normal/Hard), so tuning is edited in the Inspector
    /// without touching code. Holds no logic; all rules live in <see cref="DifficultyConfig"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "Difficulty", menuName = "Arena Survivor/Difficulty Settings")]
    public sealed class DifficultySettings : ScriptableObject
    {
        [SerializeField] private string displayName = "Normal";
        [SerializeField] private DifficultyConfig config = new DifficultyConfig();

        public string DisplayName => displayName;
        public DifficultyConfig Config => config;
    }
}
