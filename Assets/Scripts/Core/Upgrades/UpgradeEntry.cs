using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArenaSurvivor.Core.Upgrades
{
    /// <summary>
    /// One upgrade card: what it changes, by how much per level, how often it can be taken, and its card texts.
    /// Plain serializable class, listed in <see cref="Endless.EndlessConfig"/> so cards are tuned in the Inspector.
    /// </summary>
    [Serializable]
    public sealed class UpgradeEntry
    {
        [SerializeField] private UpgradeKind kind;
        [SerializeField] private string title = "";

        [Tooltip("Card text. {0} is replaced by the value per level, e.g. \"+{0}% damage\" with value 0.25 -> \"+25% damage\".")]
        [SerializeField] private string description = "";

        [Tooltip("Effect of one level. Fractions for percentage upgrades (0.25 = 25%), points otherwise.")]
        [SerializeField] private float valuePerLevel;

        [Tooltip("How many times the card can be taken in one run. 0 = filler card: unlimited, only shown when too few other cards are left.")]
        [SerializeField, Min(0)] private int maxLevel = 5;

        public UpgradeEntry()
        {
        }

        public UpgradeEntry(UpgradeKind kind, string title, string description, float valuePerLevel, int maxLevel)
        {
            if (maxLevel < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxLevel), maxLevel, "Max level cannot be negative.");
            }

            this.kind = kind;
            this.title = title ?? "";
            this.description = description ?? "";
            this.valuePerLevel = valuePerLevel;
            this.maxLevel = maxLevel;
        }

        public UpgradeKind Kind => kind;
        public string Title => title;
        public float ValuePerLevel => valuePerLevel;
        public int MaxLevel => maxLevel;

        /// <summary>True for a filler card (unlimited, shown only when the other cards run out).</summary>
        public bool IsFiller => maxLevel == 0;

        /// <summary>Card text with the value filled in; percentage kinds show the fraction as a percent.</summary>
        public string Description
        {
            get
            {
                float shown = IsPercentage(kind) ? valuePerLevel * 100f : valuePerLevel;
                return string.Format(System.Globalization.CultureInfo.InvariantCulture, description, Mathf.RoundToInt(shown));
            }
        }

        /// <summary>The cards of the default endless setup. Used as the Inspector default and in tests.</summary>
        public static List<UpgradeEntry> CreateDefaults()
        {
            return new List<UpgradeEntry>
            {
                new UpgradeEntry(UpgradeKind.Damage, "Hollow Points", "+{0}% damage", 0.25f, 5),
                new UpgradeEntry(UpgradeKind.AttackSpeed, "Rapid Fire", "+{0}% attack speed", 0.2f, 5),
                new UpgradeEntry(UpgradeKind.Multishot, "Multishot", "+{0} projectile per shot", 1f, 4),
                new UpgradeEntry(UpgradeKind.Range, "Long Barrel", "+{0}% range", 0.15f, 3),
                new UpgradeEntry(UpgradeKind.MaxHealth, "Tough Skin", "+{0} max health", 25f, 5),
                new UpgradeEntry(UpgradeKind.MoveSpeed, "Swift Boots", "+{0}% move speed", 0.1f, 4),
                new UpgradeEntry(UpgradeKind.Magnet, "Magnet", "+{0}% pickup range", 0.5f, 3),
                new UpgradeEntry(UpgradeKind.Heal, "First Aid", "Restore {0}% health", 0.4f, 0),
            };
        }

        private static bool IsPercentage(UpgradeKind kind)
        {
            return kind != UpgradeKind.Multishot && kind != UpgradeKind.MaxHealth;
        }
    }
}
