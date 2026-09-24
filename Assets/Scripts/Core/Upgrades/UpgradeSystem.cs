using System;
using System.Collections.Generic;
using ArenaSurvivor.Core.Player;
using ArenaSurvivor.Core.Progression;
using ArenaSurvivor.Core.Weapons;
using UnityEngine;

namespace ArenaSurvivor.Core.Upgrades
{
    /// <summary>
    /// Level-up cards: rolls which cards are offered, remembers how often each was taken this run,
    /// and applies a chosen card to the weapon, the player or the pickup magnet.
    ///
    /// Stat upgrades are recomputed from the level ("1 + value x level") instead of multiplied up per pick,
    /// so the result never drifts and is easy to read: 3 damage cards of +25% are exactly +75%.
    /// </summary>
    public sealed class UpgradeSystem
    {
        private readonly IReadOnlyList<UpgradeEntry> _entries;
        private readonly Weapon _weapon;
        private readonly PlayerCharacter _player;
        private readonly PickupSystem _pickups;
        private readonly Dictionary<UpgradeEntry, int> _levels = new Dictionary<UpgradeEntry, int>();
        private readonly List<UpgradeEntry> _candidates = new List<UpgradeEntry>();

        public UpgradeSystem(IReadOnlyList<UpgradeEntry> entries, Weapon weapon, PlayerCharacter player, PickupSystem pickups)
        {
            _entries = entries ?? throw new ArgumentNullException(nameof(entries));
            _weapon = weapon ?? throw new ArgumentNullException(nameof(weapon));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _pickups = pickups ?? throw new ArgumentNullException(nameof(pickups));
        }

        public IReadOnlyList<UpgradeEntry> Entries => _entries;

        /// <summary>How many times the card was taken this run.</summary>
        public int GetLevel(UpgradeEntry entry)
        {
            return entry != null && _levels.TryGetValue(entry, out int level) ? level : 0;
        }

        public bool IsMaxed(UpgradeEntry entry)
        {
            return !entry.IsFiller && GetLevel(entry) >= entry.MaxLevel;
        }

        /// <summary>
        /// Fills <paramref name="result"/> with up to <paramref name="count"/> different cards, picked at random from the
        /// cards that are not maxed yet. Filler cards (e.g. heal) only fill the remaining slots when fewer real cards are left.
        /// The result is empty only if there are no real cards left and no filler cards configured.
        /// </summary>
        public void RollOffer(System.Random random, int count, List<UpgradeEntry> result)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            result.Clear();
            _candidates.Clear();
            foreach (UpgradeEntry entry in _entries)
            {
                if (entry != null && !entry.IsFiller && !IsMaxed(entry))
                {
                    _candidates.Add(entry);
                }
            }

            PickRandom(random, count, result);

            if (result.Count < count)
            {
                _candidates.Clear();
                foreach (UpgradeEntry entry in _entries)
                {
                    if (entry != null && entry.IsFiller)
                    {
                        _candidates.Add(entry);
                    }
                }

                PickRandom(random, count - result.Count, result);
            }
        }

        /// <summary>Takes a card: raises its level and applies the effect.</summary>
        public void Apply(UpgradeEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            if (IsMaxed(entry))
            {
                throw new InvalidOperationException($"Upgrade '{entry.Title}' is already at its max level.");
            }

            int level = GetLevel(entry) + 1;
            _levels[entry] = level;
            float value = entry.ValuePerLevel;

            switch (entry.Kind)
            {
                case UpgradeKind.Damage:
                    _weapon.DamageMultiplier = 1f + value * level;
                    break;
                case UpgradeKind.AttackSpeed:
                    _weapon.FireRateMultiplier = 1f + value * level;
                    break;
                case UpgradeKind.Multishot:
                    _weapon.ExtraProjectiles = Mathf.RoundToInt(value * level);
                    break;
                case UpgradeKind.Range:
                    _weapon.RangeMultiplier = 1f + value * level;
                    break;
                case UpgradeKind.MaxHealth:
                    _player.Health.IncreaseMax(Math.Max(1, Mathf.RoundToInt(value)));
                    break;
                case UpgradeKind.MoveSpeed:
                    _player.MoveSpeedMultiplier = 1f + value * level;
                    break;
                case UpgradeKind.Magnet:
                    _pickups.MagnetMultiplier = 1f + value * level;
                    break;
                case UpgradeKind.Heal:
                    _player.Health.Heal(Math.Max(1, Mathf.RoundToInt(_player.Health.Max * value)));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(entry), entry.Kind, "Unknown upgrade kind.");
            }
        }

        /// <summary>
        /// Forgets every card taken. The stats themselves are reset by their owners
        /// (<see cref="Weapon.Reset"/>, <see cref="PlayerCharacter.Reset"/>, <see cref="PickupSystem.ResetModifiers"/>).
        /// </summary>
        public void Reset()
        {
            _levels.Clear();
        }

        // Partial Fisher-Yates shuffle over _candidates: every card has the same chance and none is picked twice.
        private void PickRandom(System.Random random, int count, List<UpgradeEntry> result)
        {
            for (int i = 0; i < count && i < _candidates.Count; i++)
            {
                int j = random.Next(i, _candidates.Count);
                (_candidates[i], _candidates[j]) = (_candidates[j], _candidates[i]);
                result.Add(_candidates[i]);
            }
        }
    }
}
