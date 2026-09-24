using System.Collections.Generic;
using ArenaSurvivor.Core.Difficulty;
using ArenaSurvivor.Core.Endless;
using ArenaSurvivor.Core.Enemies;
using ArenaSurvivor.Core.Player;
using ArenaSurvivor.Core.Progression;
using ArenaSurvivor.Core.Save;
using ArenaSurvivor.Core.Session;
using ArenaSurvivor.Core.Upgrades;
using ArenaSurvivor.Core.Weapons;
using ArenaSurvivor.Core.World;
using ArenaSurvivor.Tests.EditMode.TestDoubles;
using NUnit.Framework;
using UnityEngine;

namespace ArenaSurvivor.Tests.EditMode.World
{
    /// <summary>Integration tests for the endless mode: drops, XP, level up cards, pause, scaling and records.</summary>
    public class GameWorldEndlessTests
    {
        private const float Frame = 1f / 60f;

        private InMemorySaveService _save;

        [SetUp]
        public void SetUp()
        {
            _save = new InMemorySaveService();
        }

        /// <param name="firstLevelXp">XP for the first level up. Every kill drops 1 XP.</param>
        private GameWorld CreateWorld(float spawnRadius = 5f, float enemySpeed = 0f, int enemyHealth = 1,
            int playerHealth = 100, int firstLevelXp = 1000, float healthDropChance = 0f,
            EnemyScalingConfig scaling = null, List<UpgradeEntry> upgrades = null, float magnetRadius = 50f)
        {
            var endless = new EndlessConfig(
                new DifficultyConfig(1f, 1f, 2, 2, 50),
                rampSeconds: 60f,
                scaling ?? new EnemyScalingConfig(0f, 0f, 1f, 0f),
                new PickupConfig(1, healthDropChance, 20, collectRadius: 0.8f, magnetRadius, magnetSpeed: 30f, maxActive: 500),
                new LevelingConfig(firstLevelXp, 0),
                upgrades ?? UpgradeEntry.CreateDefaults());

            return new GameWorld(
                new WorldConfig(10f, arenaHalfSize: 50f, spawnRadius, projectileHitRadius: 0.6f),
                new PlayerConfig(playerHealth, moveSpeed: 5f),
                new EnemyConfig(enemyHealth, enemySpeed, 10, attackInterval: 1f, attackRange: 1f),
                new WeaponConfig(damage: 1, fireInterval: 0.2f, range: 8f, projectileSpeed: 30f),
                new ProgressService(_save),
                new System.Random(42),
                endless);
        }

        private static void RunFrames(GameWorld world, float seconds, bool autoPick = false)
        {
            int frames = Mathf.CeilToInt(seconds / Frame);
            for (int i = 0; i < frames && world.Session.IsPlaying; i++)
            {
                if (autoPick && world.IsChoosingUpgrade)
                {
                    world.ChooseUpgrade(0);
                }

                world.Tick(Frame, Vector2.zero);
            }
        }

        [Test]
        public void Endless_DoesNotEndWhenTimedDurationPasses()
        {
            GameWorld world = CreateWorld();
            world.StartEndless();

            RunFrames(world, 30f);

            Assert.That(world.IsEndless, Is.True);
            Assert.That(world.Session.IsPlaying, Is.True);
            Assert.That(world.Session.Elapsed, Is.GreaterThan(29f));
        }

        [Test]
        public void Endless_KillsDropExperienceThatIsCollected()
        {
            GameWorld world = CreateWorld();
            world.StartEndless();

            RunFrames(world, 5f);

            Assert.That(world.Session.Kills, Is.GreaterThan(0));
            Assert.That(world.Experience.Current, Is.EqualTo(world.Session.Kills - world.Pickups.ActiveCount));
        }

        [Test]
        public void TimedRun_HasNoDrops()
        {
            GameWorld world = CreateWorld();
            world.StartRun(new DifficultyConfig(1f, 1f, 2, 2, 50));

            RunFrames(world, 5f);

            Assert.That(world.Session.Kills, Is.GreaterThan(0));
            Assert.That(world.Pickups.ActiveCount, Is.EqualTo(0));
            Assert.That(world.Experience.Current, Is.EqualTo(0));
            Assert.That(world.Experience.Level, Is.EqualTo(1));
        }

        [Test]
        public void HealthDrops_HealThePlayer()
        {
            GameWorld world = CreateWorld(healthDropChance: 1f);
            world.StartEndless();
            world.Player.Health.TakeDamage(60);

            RunFrames(world, 5f);

            Assert.That(world.Player.Health.Current, Is.EqualTo(100));
        }

        [Test]
        public void LevelUp_OffersCardsAndPausesSimulation()
        {
            GameWorld world = CreateWorld(firstLevelXp: 1);
            int offered = 0;
            world.UpgradeOffered += () => offered++;
            world.StartEndless();

            RunFrames(world, 5f);

            Assert.That(offered, Is.EqualTo(1));
            Assert.That(world.IsChoosingUpgrade, Is.True);
            Assert.That(world.UpgradeOffer.Count, Is.EqualTo(3));
            Assert.That(world.Experience.Level, Is.EqualTo(2));

            float elapsed = world.Session.Elapsed;
            int enemies = world.Enemies.AliveCount;
            world.Tick(Frame, Vector2.up);

            Assert.That(world.Session.Elapsed, Is.EqualTo(elapsed), "Timer is paused while choosing.");
            Assert.That(world.Enemies.AliveCount, Is.EqualTo(enemies));
            Assert.That(world.Player.Position, Is.EqualTo(Vector3.zero), "Player cannot move while choosing.");
        }

        [Test]
        public void ChooseUpgrade_AppliesEffectAndResumes()
        {
            var damage = new UpgradeEntry(UpgradeKind.Damage, "Damage", "", 1f, 5);
            GameWorld world = CreateWorld(firstLevelXp: 1, upgrades: new List<UpgradeEntry> { damage });
            UpgradeEntry chosen = null;
            world.UpgradeChosen += e => chosen = e;
            world.StartEndless();
            RunFrames(world, 5f);

            world.ChooseUpgrade(0);

            Assert.That(chosen, Is.SameAs(damage));
            Assert.That(world.Weapon.DamageMultiplier, Is.EqualTo(2f));
            Assert.That(world.IsChoosingUpgrade, Is.False);

            float elapsed = world.Session.Elapsed;
            world.Tick(Frame, Vector2.zero);
            Assert.That(world.Session.Elapsed, Is.GreaterThan(elapsed));
        }

        [Test]
        public void SeveralLevelUpsAtOnce_AreOfferedOneAfterAnother()
        {
            GameWorld world = CreateWorld(firstLevelXp: 1);
            int offered = 0;
            world.UpgradeOffered += () => offered++;
            world.StartEndless();

            // One big gem worth three levels.
            world.Pickups.Spawn(PickupKind.Experience, Vector3.zero, 3);
            world.Tick(Frame, Vector2.zero);

            Assert.That(world.Experience.Level, Is.EqualTo(4));
            Assert.That(offered, Is.EqualTo(1));

            world.ChooseUpgrade(0);
            Assert.That(world.IsChoosingUpgrade, Is.True);
            world.ChooseUpgrade(0);
            Assert.That(world.IsChoosingUpgrade, Is.True);
            world.ChooseUpgrade(0);

            Assert.That(world.IsChoosingUpgrade, Is.False);
            Assert.That(offered, Is.EqualTo(3));
        }

        [Test]
        public void ChooseUpgrade_InvalidIndex_Throws()
        {
            GameWorld world = CreateWorld();
            world.StartEndless();

            Assert.Throws<System.ArgumentOutOfRangeException>(() => world.ChooseUpgrade(0));
        }

        [Test]
        public void EnemiesGrowStrongerOverTime()
        {
            var scaling = new EnemyScalingConfig(healthPerMinute: 1f, speedPerMinute: 0.5f, maxSpeedMultiplier: 1.25f, damagePerMinute: 1f);
            GameWorld world = CreateWorld(spawnRadius: 20f, enemyHealth: 10, scaling: scaling);
            world.StartEndless();

            RunFrames(world, 60f);

            Assert.That(world.Enemies.HealthMultiplier, Is.EqualTo(2f).Within(0.01f));
            Assert.That(world.Enemies.SpeedMultiplier, Is.EqualTo(1.25f));
            Assert.That(world.Enemies.DamageMultiplier, Is.EqualTo(2f).Within(0.01f));
            Assert.That(world.Enemies.SpawnHealth, Is.EqualTo(20));
        }

        [Test]
        public void Death_RecordsBestTimeAndLevel()
        {
            GameWorld world = CreateWorld(spawnRadius: 20f, enemySpeed: 10f, enemyHealth: 1000, playerHealth: 30);
            world.StartEndless();

            RunFrames(world, 60f);

            Assert.That(world.Session.State, Is.EqualTo(GameState.Lost));
            Assert.That(world.LastRunIsRecord, Is.True);
            Assert.That(world.Progress.BestEndlessSeconds, Is.EqualTo(world.LastResult.Value.SurvivedSeconds));
            Assert.That(world.Progress.BestEndlessLevel, Is.EqualTo(1));
            Assert.That(_save.Stored.bestEndlessSeconds, Is.GreaterThan(0f));
        }

        [Test]
        public void Replay_RestartsEndlessWithEverythingReset()
        {
            GameWorld world = CreateWorld(firstLevelXp: 1, enemyHealth: 1);
            world.StartEndless();
            RunFrames(world, 20f, autoPick: true);
            Assert.That(world.Experience.Level, Is.GreaterThan(1));

            world.Replay();

            Assert.That(world.IsEndless, Is.True);
            Assert.That(world.Session.IsEndless, Is.True);
            Assert.That(world.Experience.Level, Is.EqualTo(1));
            Assert.That(world.Pickups.ActiveCount, Is.EqualTo(0));
            Assert.That(world.IsChoosingUpgrade, Is.False);
            Assert.That(world.Weapon.DamageMultiplier, Is.EqualTo(1f));
            Assert.That(world.Weapon.ProjectilesPerShot, Is.EqualTo(1));
            Assert.That(world.Player.MoveSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(world.Player.Health.Max, Is.EqualTo(100));
            Assert.That(world.Enemies.HealthMultiplier, Is.EqualTo(1f));
            foreach (UpgradeEntry entry in world.Endless.Upgrades)
            {
                Assert.That(world.Upgrades.GetLevel(entry), Is.EqualTo(0));
            }
        }

        [Test]
        public void TimedRunAfterEndless_HasNoEndlessLeftovers()
        {
            GameWorld world = CreateWorld(firstLevelXp: 1);
            world.StartEndless();
            RunFrames(world, 20f, autoPick: true);

            world.StartRun(new DifficultyConfig(1f, 1f, 2, 2, 50));

            Assert.That(world.IsEndless, Is.False);
            Assert.That(world.Session.IsEndless, Is.False);
            Assert.That(world.Enemies.SpeedMultiplier, Is.EqualTo(1f));
            Assert.That(world.Weapon.ProjectilesPerShot, Is.EqualTo(1));
        }

        [Test]
        public void ReturnToMenu_WhileChoosing_ClearsOffer()
        {
            GameWorld world = CreateWorld(firstLevelXp: 1);
            world.StartEndless();
            RunFrames(world, 5f);
            Assert.That(world.IsChoosingUpgrade, Is.True);

            world.ReturnToMenu();

            Assert.That(world.IsChoosingUpgrade, Is.False);
            Assert.That(world.Pickups.ActiveCount, Is.EqualTo(0));
            Assert.That(world.Session.State, Is.EqualTo(GameState.Idle));
        }

        [Test]
        public void EveryPickupSpawn_HasADespawn()
        {
            GameWorld world = CreateWorld(firstLevelXp: 3, magnetRadius: 2f, healthDropChance: 0.5f);
            var visible = new HashSet<Pickup>();
            world.Pickups.Spawned += p => Assert.That(visible.Add(p), Is.True);
            world.Pickups.Despawned += p => Assert.That(visible.Remove(p), Is.True);
            world.StartEndless();

            RunFrames(world, 15f, autoPick: true);
            world.ReturnToMenu();

            Assert.That(visible, Is.Empty);
        }

        [Test]
        public void LongEndlessRun_WithAutoPick_Smoke()
        {
            // Default endless tuning with the real enemy speed: runs 5 minutes or until the player dies.
            var world = new GameWorld(
                new WorldConfig(180f, 20f, 18f, 0.6f, enemyPrewarm: 50, projectilePrewarm: 16),
                new PlayerConfig(100, 5f),
                new EnemyConfig(30, 2.5f, 10, 1f, 1.2f, 1.2f, 0.5f),
                new WeaponConfig(10, 0.35f, 8f, 25f),
                new ProgressService(_save),
                new System.Random(1));
            world.StartEndless();

            RunFrames(world, 300f, autoPick: true);

            Assert.That(world.Session.Kills, Is.GreaterThan(0));
            Assert.That(world.Experience.Level, Is.GreaterThan(1));
            TestContext.WriteLine($"Endless smoke: {world.Session.Elapsed:F0} s, {world.Session.Kills} kills, level {world.Experience.Level}, state {world.Session.State}");
        }
    }
}
