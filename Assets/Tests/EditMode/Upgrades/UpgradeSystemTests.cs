using System;
using System.Collections.Generic;
using System.Linq;
using ArenaSurvivor.Core.Combat;
using ArenaSurvivor.Core.Enemies;
using ArenaSurvivor.Core.Player;
using ArenaSurvivor.Core.Progression;
using ArenaSurvivor.Core.Upgrades;
using ArenaSurvivor.Core.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace ArenaSurvivor.Tests.EditMode.Upgrades
{
    public class UpgradeSystemTests
    {
        private Weapon _weapon;
        private PlayerCharacter _player;
        private PickupSystem _pickups;

        [SetUp]
        public void SetUp()
        {
            var enemies = new EnemySystem(new EnemyConfig(), new Health(100));
            _weapon = new Weapon(new WeaponConfig(10, 0.5f, 8f, 20f), new ProjectileSystem(enemies, 0.5f));
            _player = new PlayerCharacter(new PlayerConfig(100, 5f), 20f);
            _pickups = new PickupSystem(new PickupConfig());
        }

        private UpgradeSystem Create(List<UpgradeEntry> entries) => new UpgradeSystem(entries, _weapon, _player, _pickups);

        private static UpgradeEntry Entry(UpgradeKind kind, float value, int maxLevel = 5) =>
            new UpgradeEntry(kind, kind.ToString(), "", value, maxLevel);

        [Test]
        public void RollOffer_ReturnsDistinctCards()
        {
            UpgradeSystem system = Create(UpgradeEntry.CreateDefaults());
            var offer = new List<UpgradeEntry>();
            var random = new System.Random(1);

            for (int i = 0; i < 50; i++)
            {
                system.RollOffer(random, 3, offer);

                Assert.That(offer.Count, Is.EqualTo(3));
                Assert.That(offer.Distinct().Count(), Is.EqualTo(3));
                Assert.That(offer.Any(e => e.IsFiller), Is.False, "Filler cards only when real cards run out.");
            }
        }

        [Test]
        public void RollOffer_EveryCardCanAppear()
        {
            List<UpgradeEntry> entries = UpgradeEntry.CreateDefaults();
            UpgradeSystem system = Create(entries);
            var offer = new List<UpgradeEntry>();
            var seen = new HashSet<UpgradeEntry>();
            var random = new System.Random(7);

            for (int i = 0; i < 200; i++)
            {
                system.RollOffer(random, 3, offer);
                seen.UnionWith(offer);
            }

            Assert.That(seen.Count, Is.EqualTo(entries.Count(e => !e.IsFiller)));
        }

        [Test]
        public void MaxedCards_AreNoLongerOffered_FillerFillsTheGap()
        {
            UpgradeEntry damage = Entry(UpgradeKind.Damage, 0.25f, maxLevel: 1);
            UpgradeEntry speed = Entry(UpgradeKind.MoveSpeed, 0.1f, maxLevel: 2);
            UpgradeEntry heal = Entry(UpgradeKind.Heal, 0.4f, maxLevel: 0);
            UpgradeSystem system = Create(new List<UpgradeEntry> { damage, speed, heal });
            var offer = new List<UpgradeEntry>();

            system.Apply(damage);
            system.RollOffer(new System.Random(3), 3, offer);

            Assert.That(system.IsMaxed(damage), Is.True);
            Assert.That(offer, Is.EquivalentTo(new[] { speed, heal }));
        }

        [Test]
        public void EverythingMaxedWithoutFiller_EmptyOffer()
        {
            UpgradeEntry damage = Entry(UpgradeKind.Damage, 0.25f, maxLevel: 1);
            UpgradeSystem system = Create(new List<UpgradeEntry> { damage });
            var offer = new List<UpgradeEntry>();

            system.Apply(damage);
            system.RollOffer(new System.Random(3), 3, offer);

            Assert.That(offer, Is.Empty);
        }

        [Test]
        public void Apply_BeyondMaxLevel_Throws()
        {
            UpgradeEntry damage = Entry(UpgradeKind.Damage, 0.25f, maxLevel: 1);
            UpgradeSystem system = Create(new List<UpgradeEntry> { damage });
            system.Apply(damage);

            Assert.Throws<InvalidOperationException>(() => system.Apply(damage));
        }

        [Test]
        public void WeaponUpgrades_AddUpLinearly()
        {
            UpgradeEntry damage = Entry(UpgradeKind.Damage, 0.25f);
            UpgradeEntry rate = Entry(UpgradeKind.AttackSpeed, 0.2f);
            UpgradeEntry multi = Entry(UpgradeKind.Multishot, 1f);
            UpgradeEntry range = Entry(UpgradeKind.Range, 0.15f);
            UpgradeSystem system = Create(new List<UpgradeEntry> { damage, rate, multi, range });

            for (int i = 0; i < 3; i++)
            {
                system.Apply(damage);
            }

            system.Apply(rate);
            system.Apply(rate);
            system.Apply(multi);
            system.Apply(range);

            Assert.That(system.GetLevel(damage), Is.EqualTo(3));
            Assert.That(_weapon.DamageMultiplier, Is.EqualTo(1.75f).Within(1e-5f));
            Assert.That(_weapon.Damage, Is.EqualTo(18)); // 17.5 rounds to even
            Assert.That(_weapon.FireRateMultiplier, Is.EqualTo(1.4f).Within(1e-5f));
            Assert.That(_weapon.ProjectilesPerShot, Is.EqualTo(2));
            Assert.That(_weapon.Range, Is.EqualTo(8f * 1.15f).Within(1e-4f));
        }

        [Test]
        public void PlayerUpgrades_ChangeHealthSpeedAndMagnet()
        {
            UpgradeEntry health = Entry(UpgradeKind.MaxHealth, 25f);
            UpgradeEntry speed = Entry(UpgradeKind.MoveSpeed, 0.1f);
            UpgradeEntry magnet = Entry(UpgradeKind.Magnet, 0.5f);
            UpgradeSystem system = Create(new List<UpgradeEntry> { health, speed, magnet });
            _player.Health.TakeDamage(50);

            system.Apply(health);
            system.Apply(speed);
            system.Apply(magnet);

            Assert.That(_player.Health.Max, Is.EqualTo(125));
            Assert.That(_player.Health.Current, Is.EqualTo(75));
            Assert.That(_player.MoveSpeedMultiplier, Is.EqualTo(1.1f).Within(1e-5f));
            Assert.That(_pickups.MagnetMultiplier, Is.EqualTo(1.5f).Within(1e-5f));
        }

        [Test]
        public void Heal_RestoresFractionOfMaxAndCanBeTakenAgain()
        {
            UpgradeEntry heal = Entry(UpgradeKind.Heal, 0.4f, maxLevel: 0);
            UpgradeSystem system = Create(new List<UpgradeEntry> { heal });
            _player.Health.TakeDamage(90);

            system.Apply(heal);
            system.Apply(heal);

            Assert.That(_player.Health.Current, Is.EqualTo(90));
            Assert.That(system.IsMaxed(heal), Is.False);
        }

        [Test]
        public void Reset_ForgetsLevels()
        {
            UpgradeEntry damage = Entry(UpgradeKind.Damage, 0.25f, maxLevel: 1);
            UpgradeSystem system = Create(new List<UpgradeEntry> { damage });
            system.Apply(damage);

            system.Reset();

            Assert.That(system.GetLevel(damage), Is.EqualTo(0));
            Assert.That(system.IsMaxed(damage), Is.False);
        }

        [Test]
        public void Description_FormatsValue()
        {
            Assert.That(new UpgradeEntry(UpgradeKind.Damage, "", "+{0}% damage", 0.25f, 5).Description, Is.EqualTo("+25% damage"));
            Assert.That(new UpgradeEntry(UpgradeKind.Multishot, "", "+{0} projectile", 1f, 4).Description, Is.EqualTo("+1 projectile"));
            Assert.That(new UpgradeEntry(UpgradeKind.MaxHealth, "", "+{0} max health", 25f, 5).Description, Is.EqualTo("+25 max health"));
        }

        [Test]
        public void Defaults_AreValid()
        {
            List<UpgradeEntry> defaults = UpgradeEntry.CreateDefaults();

            Assert.That(defaults.Select(e => e.Kind).Distinct().Count(), Is.EqualTo(defaults.Count));
            Assert.That(defaults.Count(e => e.IsFiller), Is.EqualTo(1));
            Assert.That(defaults.All(e => !string.IsNullOrEmpty(e.Title) && !string.IsNullOrEmpty(e.Description)), Is.True);
        }
    }
}
