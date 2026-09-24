using System;

namespace ArenaSurvivor.Core.Progression
{
    /// <summary>
    /// The player's level and XP in the current run. XP beyond the next level carries over, so one big gem
    /// can raise several levels at once; <see cref="LeveledUp"/> is raised once per level.
    /// </summary>
    public sealed class Experience
    {
        private readonly LevelingConfig _config;

        public Experience(LevelingConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            Reset();
        }

        /// <summary>Current level, starting at 1.</summary>
        public int Level { get; private set; }

        /// <summary>XP collected towards the next level.</summary>
        public int Current { get; private set; }

        /// <summary>XP needed from the start of this level to the next.</summary>
        public int ToNext => _config.ExperienceToNext(Level);

        /// <summary>Progress to the next level from 0 to 1, for the XP bar.</summary>
        public float Normalized => (float)Current / ToNext;

        /// <summary>Raised with the new level, once for every level gained.</summary>
        public event Action<int> LeveledUp;

        public void Add(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "XP cannot be negative.");
            }

            Current += amount;
            while (Current >= ToNext)
            {
                Current -= ToNext;
                Level++;
                LeveledUp?.Invoke(Level);
            }
        }

        /// <summary>Back to level 1 with no XP. Used when a run starts.</summary>
        public void Reset()
        {
            Level = 1;
            Current = 0;
        }
    }
}
