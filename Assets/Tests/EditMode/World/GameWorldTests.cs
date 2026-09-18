using ArenaSurvivor.Core.Difficulty;
using ArenaSurvivor.Core.Enemies;
using ArenaSurvivor.Core.Player;
using ArenaSurvivor.Core.Save;
using ArenaSurvivor.Core.Session;
using ArenaSurvivor.Core.Weapons;
using ArenaSurvivor.Core.World;
using ArenaSurvivor.Tests.EditMode.TestDoubles;
using NUnit.Framework;
using UnityEngine;

namespace ArenaSurvivor.Tests.EditMode.World
{
    /// <summary>
    /// Integration tests: all Core systems together, driven the same way the Unity side will drive them.
    /// </summary>
    public class GameWorldTests
    {
        private const float Frame = 1f / 60f;
        private const float Duration = 10f;
        private const float WeaponRange = 8f;

        private InMemorySaveService _save;

        [SetUp]
        public void SetUp()
        {
            _save = new InMemorySaveService();
        }

        private GameWorld CreateWorld(float spawnRadius, float enemySpeed = 0f, int enemyHealth = 1,
            int playerHealth = 100, int enemyDamage = 10, float duration = Duration)
        {
            return new GameWorld(
                new WorldConfig(duration, arenaHalfSize: 50f, spawnRadius, projectileHitRadius: 0.6f),
                new PlayerConfig(playerHealth, moveSpeed: 5f),
                new EnemyConfig(enemyHealth, enemySpeed, enemyDamage, attackInterval: 1f, attackRange: 1f),
                new WeaponConfig(damage: 1, fireInterval: 0.2f, WeaponRange, projectileSpeed: 30f),
                new ProgressService(_save),
                new System.Random(42));
        }

        private static DifficultyConfig Difficulty(float interval = 1f, int waveSize = 2, int maxAlive = 50)
        {
            return new DifficultyConfig(interval, interval, waveSize, waveSize, maxAlive);
        }

        private static void RunFrames(GameWorld world, float seconds, Vector2 input = default)
        {
            int frames = Mathf.CeilToInt(seconds / Frame);
            for (int i = 0; i < frames && world.Session.IsPlaying; i++)
            {
                world.Tick(Frame, input);
            }
        }

        [Test]
        public void BeforeStart_TickDoesNothing()
        {
            GameWorld world = CreateWorld(spawnRadius: 5f);

            world.Tick(Frame, Vector2.up);

            Assert.That(world.Session.State, Is.EqualTo(GameState.Idle));
            Assert.That(world.Player.Position, Is.EqualTo(Vector3.zero));
            Assert.That(world.Enemies.AliveCount, Is.EqualTo(0));
        }

        [Test]
        public void StartRun_FirstTickSpawnsFirstWave()
        {
            GameWorld world = CreateWorld(spawnRadius: 20f);
            world.StartRun(Difficulty(waveSize: 3));

            world.Tick(Frame, Vector2.zero);

            Assert.That(world.Session.IsPlaying, Is.True);
            Assert.That(world.Enemies.AliveCount, Is.EqualTo(3));
        }

        [Test]
        public void EnemiesInRange_AreShotAndCountedAsKills()
        {
            // Stationary enemies spawn inside weapon range and die in one hit.
            GameWorld world = CreateWorld(spawnRadius: 5f);
            world.StartRun(Difficulty(interval: 1f, waveSize: 2));

            RunFrames(world, 3f);

            Assert.That(world.Session.Kills, Is.GreaterThan(0));
        }

        [Test]
        public void Player_FacesCurrentTarget()
        {
            GameWorld world = CreateWorld(spawnRadius: 5f, enemyHealth: 1000);
            world.StartRun(Difficulty(waveSize: 1));

            world.Tick(Frame, Vector2.zero);

            Vector3 toTarget = (world.Weapon.CurrentTarget.Position - world.Player.Position).normalized;
            Assert.That(Vector3.Dot(world.Player.Forward, toTarget), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void SurvivingFullDuration_WinsAndSavesKills()
        {
            GameWorld world = CreateWorld(spawnRadius: 5f);
            world.StartRun(Difficulty(interval: 1f, waveSize: 2));

            RunFrames(world, Duration + 1f);

            Assert.That(world.Session.State, Is.EqualTo(GameState.Won));
            Assert.That(world.LastResult.HasValue, Is.True);
            Assert.That(world.LastResult.Value.Outcome, Is.EqualTo(GameState.Won));
            Assert.That(world.Progress.TotalKills, Is.EqualTo(world.LastResult.Value.Kills));
            Assert.That(_save.Stored.totalKills, Is.EqualTo(world.LastResult.Value.Kills));
        }

        [Test]
        public void PlayerKilled_LosesAndSavesKills()
        {
            // Fast, tanky enemies that the rifle cannot kill in time.
            GameWorld world = CreateWorld(spawnRadius: 20f, enemySpeed: 10f, enemyHealth: 1000,
                playerHealth: 30, enemyDamage: 10);
            world.StartRun(Difficulty(interval: 0.5f, waveSize: 5));

            RunFrames(world, Duration);

            Assert.That(world.Session.State, Is.EqualTo(GameState.Lost));
            Assert.That(world.Player.Health.IsDead, Is.True);
            Assert.That(world.LastResult.Value.Outcome, Is.EqualTo(GameState.Lost));
            Assert.That(world.LastResult.Value.SurvivedSeconds, Is.LessThan(Duration));
        }

        [Test]
        public void AfterRunEnds_ArenaIsFrozen()
        {
            GameWorld world = CreateWorld(spawnRadius: 20f, enemySpeed: 10f, enemyHealth: 1000, playerHealth: 10);
            world.StartRun(Difficulty(interval: 0.5f, waveSize: 3));
            RunFrames(world, Duration);
            Vector3 enemyPosition = world.Enemies.Active[0].Position;
            int alive = world.Enemies.AliveCount;

            world.Tick(Frame, Vector2.up);

            Assert.That(world.Enemies.Active[0].Position, Is.EqualTo(enemyPosition));
            Assert.That(world.Enemies.AliveCount, Is.EqualTo(alive));
        }

        [Test]
        public void Replay_ResetsRunButKeepsTotalKills()
        {
            GameWorld world = CreateWorld(spawnRadius: 5f);
            world.StartRun(Difficulty(interval: 1f, waveSize: 2));
            RunFrames(world, Duration + 1f);
            int totalAfterFirstRun = world.Progress.TotalKills;

            world.Replay();

            Assert.That(world.Session.State, Is.EqualTo(GameState.Playing));
            Assert.That(world.Session.Kills, Is.EqualTo(0));
            Assert.That(world.Session.Elapsed, Is.EqualTo(0f));
            Assert.That(world.Enemies.AliveCount, Is.EqualTo(0));
            Assert.That(world.Projectiles.ActiveCount, Is.EqualTo(0));
            Assert.That(world.Player.Health.Current, Is.EqualTo(world.Player.Health.Max));
            Assert.That(world.Player.Position, Is.EqualTo(Vector3.zero));
            Assert.That(world.LastResult.HasValue, Is.False);
            Assert.That(world.Progress.TotalKills, Is.EqualTo(totalAfterFirstRun));
        }

        [Test]
        public void TwoRuns_TotalKillsAddUp()
        {
            GameWorld world = CreateWorld(spawnRadius: 5f);
            world.StartRun(Difficulty(interval: 1f, waveSize: 2));
            RunFrames(world, Duration + 1f);
            int firstRunKills = world.LastResult.Value.Kills;

            world.Replay();
            RunFrames(world, Duration + 1f);
            int secondRunKills = world.LastResult.Value.Kills;

            Assert.That(world.Progress.TotalKills, Is.EqualTo(firstRunKills + secondRunKills));
        }

        [Test]
        public void ReturnToMenu_ClearsArenaAndGoesIdle()
        {
            GameWorld world = CreateWorld(spawnRadius: 20f);
            world.StartRun(Difficulty(waveSize: 5));
            RunFrames(world, 1f);

            world.ReturnToMenu();

            Assert.That(world.Session.State, Is.EqualTo(GameState.Idle));
            Assert.That(world.Enemies.AliveCount, Is.EqualTo(0));
            Assert.That(world.Projectiles.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void ViewEvents_StayBalanced()
        {
            // Every Spawned must be matched by a Despawned, otherwise the Unity side would leak models.
            GameWorld world = CreateWorld(spawnRadius: 5f, enemyHealth: 2);
            int enemyBalance = 0;
            int projectileBalance = 0;
            world.Enemies.Spawned += _ => enemyBalance++;
            world.Enemies.Despawned += _ => enemyBalance--;
            world.Projectiles.Spawned += _ => projectileBalance++;
            world.Projectiles.Despawned += _ => projectileBalance--;

            world.StartRun(Difficulty(interval: 0.5f, waveSize: 3));
            RunFrames(world, 5f);
            Assert.That(enemyBalance, Is.EqualTo(world.Enemies.AliveCount));
            Assert.That(projectileBalance, Is.EqualTo(world.Projectiles.ActiveCount));

            world.Replay();
            Assert.That(enemyBalance, Is.EqualTo(0));
            Assert.That(projectileBalance, Is.EqualTo(0));
        }

        [Test]
        public void FullRunWithDefaultTuning_CompletesConsistently()
        {
            // Smoke test: the real default configs for a full 3-minute run at 60 FPS, with the player
            // circling the arena. Checks that the run ends and the bookkeeping adds up.
            var world = new GameWorld(new WorldConfig(), new PlayerConfig(), new EnemyConfig(), new WeaponConfig(),
                new ProgressService(_save), new System.Random(7));
            world.StartRun(new DifficultyConfig(2.5f, 1f, 3, 10, 80));

            float time = 0f;
            for (int i = 0; i < 180 * 60 + 10 && world.Session.IsPlaying; i++)
            {
                time += Frame;
                world.Tick(Frame, new Vector2(Mathf.Cos(time * 0.5f), Mathf.Sin(time * 0.5f)));
            }

            Assert.That(world.Session.IsPlaying, Is.False);
            Assert.That(world.LastResult.HasValue, Is.True);
            Assert.That(world.Progress.TotalKills, Is.EqualTo(world.LastResult.Value.Kills));
            Assert.That(world.Enemies.AliveCount, Is.LessThanOrEqualTo(80));
        }
    }
}
