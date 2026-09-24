using System;
using System.Collections.Generic;
using ArenaSurvivor.Core.Difficulty;
using ArenaSurvivor.Core.Progression;
using ArenaSurvivor.Core.Upgrades;
using UnityEngine;

namespace ArenaSurvivor.Core.Endless
{
    /// <summary>
    /// Everything that defines the endless mode: no timer, enemies drop XP and health, the player levels up and
    /// picks upgrade cards, and the arena gets harder the longer the run lasts.
    ///
    /// Two independent ramps make it harder over time:
    /// - <b>More enemies:</b> the spawn settings reuse <see cref="DifficultyConfig"/>; its "start" to "end" ramp is
    ///   stretched over <see cref="RampSeconds"/> and then stays at the "end" values.
    /// - <b>Stronger enemies:</b> <see cref="EnemyScalingConfig"/> raises health, speed and damage per minute, without end.
    ///
    /// Plain serializable class: edited in the Inspector through <see cref="EndlessSettings"/>, constructed in tests.
    /// </summary>
    [Serializable]
    public sealed class EndlessConfig
    {
        [Tooltip("Spawn ramp. \"Start\" values at 0:00, \"end\" values after Ramp Seconds.")]
        [SerializeField] private DifficultyConfig spawn = new DifficultyConfig(3f, 0.7f, 2, 12, 100);

        [Tooltip("Seconds until the spawn settings reach their \"end\" values.")]
        [SerializeField, Min(1f)] private float rampSeconds = 420f;

        [SerializeField] private EnemyScalingConfig enemyScaling = new EnemyScalingConfig();
        [SerializeField] private PickupConfig pickups = new PickupConfig();
        [SerializeField] private LevelingConfig leveling = new LevelingConfig();

        [Tooltip("Cards shown on every level up. The player picks one.")]
        [SerializeField, Range(1, 4)] private int cardsPerLevel = 3;

        [SerializeField] private List<UpgradeEntry> upgrades = UpgradeEntry.CreateDefaults();

        public EndlessConfig()
        {
        }

        public EndlessConfig(DifficultyConfig spawn, float rampSeconds, EnemyScalingConfig enemyScaling,
            PickupConfig pickups, LevelingConfig leveling, IEnumerable<UpgradeEntry> upgrades, int cardsPerLevel = 3)
        {
            if (rampSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(rampSeconds), rampSeconds, "Ramp must be positive.");
            }

            if (cardsPerLevel < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(cardsPerLevel), cardsPerLevel, "At least one card per level.");
            }

            this.spawn = spawn ?? throw new ArgumentNullException(nameof(spawn));
            this.rampSeconds = rampSeconds;
            this.enemyScaling = enemyScaling ?? throw new ArgumentNullException(nameof(enemyScaling));
            this.pickups = pickups ?? throw new ArgumentNullException(nameof(pickups));
            this.leveling = leveling ?? throw new ArgumentNullException(nameof(leveling));
            this.upgrades = new List<UpgradeEntry>(upgrades ?? throw new ArgumentNullException(nameof(upgrades)));
            this.cardsPerLevel = cardsPerLevel;
        }

        public DifficultyConfig Spawn => spawn;
        public float RampSeconds => rampSeconds;
        public EnemyScalingConfig EnemyScaling => enemyScaling;
        public PickupConfig Pickups => pickups;
        public LevelingConfig Leveling => leveling;
        public int CardsPerLevel => cardsPerLevel;
        public IReadOnlyList<UpgradeEntry> Upgrades => upgrades;

        /// <summary>Progress (0..1) of the spawn ramp after <paramref name="elapsedSeconds"/> of an endless run.</summary>
        public float SpawnProgress(float elapsedSeconds) => Mathf.Clamp01(elapsedSeconds / rampSeconds);
    }
}
