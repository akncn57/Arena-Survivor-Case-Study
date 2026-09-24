using System;
using UnityEngine;

namespace ArenaSurvivor.Core.Progression
{
    /// <summary>
    /// The XP curve: how many XP points each level needs. Grows linearly, so every level takes a bit longer
    /// than the one before. Plain serializable class, part of <see cref="Endless.EndlessConfig"/>.
    /// </summary>
    [Serializable]
    public sealed class LevelingConfig
    {
        [Tooltip("XP needed to go from level 1 to level 2.")]
        [SerializeField, Min(1)] private int firstLevelExperience = 4;

        [Tooltip("Extra XP every further level needs compared with the previous one.")]
        [SerializeField, Min(0)] private int experienceGrowthPerLevel = 2;

        public LevelingConfig()
        {
        }

        public LevelingConfig(int firstLevelExperience, int experienceGrowthPerLevel)
        {
            if (firstLevelExperience < 1 || experienceGrowthPerLevel < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(firstLevelExperience), "First level XP must be at least 1, growth not negative.");
            }

            this.firstLevelExperience = firstLevelExperience;
            this.experienceGrowthPerLevel = experienceGrowthPerLevel;
        }

        /// <summary>XP needed to go from <paramref name="level"/> to the next level.</summary>
        public int ExperienceToNext(int level)
        {
            if (level < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "Levels start at 1.");
            }

            return firstLevelExperience + experienceGrowthPerLevel * (level - 1);
        }
    }
}
