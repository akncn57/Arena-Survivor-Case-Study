using System;
using ArenaSurvivor.Core.Combat;
using ArenaSurvivor.Core.Difficulty;
using ArenaSurvivor.Core.Enemies;
using NUnit.Framework;
using UnityEngine;

namespace ArenaSurvivor.Tests.EditMode.Enemies
{
    public class WaveSpawnerTests
    {
        private const float SpawnRadius = 10f;
        private const float ArenaHalfSize = 30f;

        private EnemySystem _enemies;
        private WaveSpawner _spawner;

        [SetUp]
        public void SetUp()
        {
            _enemies = new EnemySystem(new EnemyConfig(), new Health(100));
            _spawner = new WaveSpawner(_enemies, SpawnRadius, ArenaHalfSize, new System.Random(1234));
        }

        private static DifficultyConfig Difficulty(float interval = 2f, int waveSize = 3, int maxAlive = 100)
        {
            return new DifficultyConfig(interval, interval, waveSize, waveSize, maxAlive);
        }

        [Test]
        public void FirstTick_SpawnsFirstWaveImmediately()
        {
            _spawner.Begin(Difficulty(waveSize: 3));

            int spawned = _spawner.Tick(0.016f, 0f, Vector3.zero);

            Assert.That(spawned, Is.EqualTo(3));
            Assert.That(_enemies.AliveCount, Is.EqualTo(3));
        }

        [Test]
        public void NextWave_WaitsForSpawnInterval()
        {
            _spawner.Begin(Difficulty(interval: 2f, waveSize: 3));
            _spawner.Tick(0.1f, 0f, Vector3.zero);

            int beforeInterval = _spawner.Tick(1.5f, 0f, Vector3.zero);
            int afterInterval = _spawner.Tick(0.5f, 0f, Vector3.zero);

            Assert.That(beforeInterval, Is.EqualTo(0));
            Assert.That(afterInterval, Is.EqualTo(3));
            Assert.That(_enemies.AliveCount, Is.EqualTo(6));
        }

        [Test]
        public void Wave_IsTrimmedToMaxAlive()
        {
            _spawner.Begin(Difficulty(interval: 1f, waveSize: 4, maxAlive: 6));

            _spawner.Tick(0.1f, 0f, Vector3.zero);
            int second = _spawner.Tick(1f, 0f, Vector3.zero);
            int third = _spawner.Tick(1f, 0f, Vector3.zero);

            Assert.That(second, Is.EqualTo(2));
            Assert.That(third, Is.EqualTo(0));
            Assert.That(_enemies.AliveCount, Is.EqualTo(6));
        }

        [Test]
        public void WaveSize_FollowsRunProgress()
        {
            _spawner.Begin(new DifficultyConfig(1f, 1f, 2, 10, 100));

            int atEnd = _spawner.Tick(0.1f, 1f, Vector3.zero);

            Assert.That(atEnd, Is.EqualTo(10));
        }

        [Test]
        public void Enemies_SpawnOnRingAroundPlayer()
        {
            var player = new Vector3(3f, 0f, -2f);
            _spawner.Begin(Difficulty(waveSize: 20));

            _spawner.Tick(0.1f, 0f, player);

            foreach (Enemy enemy in _enemies.Active)
            {
                Assert.That(Vector3.Distance(enemy.Position, player), Is.EqualTo(SpawnRadius).Within(1e-3f));
            }
        }

        [Test]
        public void Enemies_NearArenaEdge_AreClampedInside()
        {
            var playerAtCorner = new Vector3(ArenaHalfSize, 0f, ArenaHalfSize);
            _spawner.Begin(Difficulty(waveSize: 20));

            _spawner.Tick(0.1f, 0f, playerAtCorner);

            foreach (Enemy enemy in _enemies.Active)
            {
                Assert.That(Mathf.Abs(enemy.Position.x), Is.LessThanOrEqualTo(ArenaHalfSize));
                Assert.That(Mathf.Abs(enemy.Position.z), Is.LessThanOrEqualTo(ArenaHalfSize));
            }
        }

        [Test]
        public void Begin_ResetsTimerForNewRun()
        {
            _spawner.Begin(Difficulty(interval: 5f));
            _spawner.Tick(0.1f, 0f, Vector3.zero);
            _enemies.Clear();

            _spawner.Begin(Difficulty(interval: 5f, waveSize: 2));
            int spawned = _spawner.Tick(0.1f, 0f, Vector3.zero);

            Assert.That(spawned, Is.EqualTo(2));
        }

        [Test]
        public void Tick_BeforeBegin_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _spawner.Tick(0.1f, 0f, Vector3.zero));
        }
    }
}
