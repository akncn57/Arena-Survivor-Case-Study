using System.Collections.Generic;
using ArenaSurvivor.Core.Combat;
using ArenaSurvivor.Core.Enemies;
using NUnit.Framework;
using UnityEngine;

namespace ArenaSurvivor.Tests.EditMode.Enemies
{
    public class EnemySystemTests
    {
        private const int EnemyHealth = 3;
        private const float Speed = 2f;
        private const int Damage = 10;
        private const float AttackInterval = 1f;
        private const float Range = 1f;

        private Health _player;
        private EnemySystem _system;

        [SetUp]
        public void SetUp()
        {
            _player = new Health(100);
            _system = new EnemySystem(new EnemyConfig(EnemyHealth, Speed, Damage, AttackInterval, Range), _player);
        }

        [Test]
        public void Spawn_AddsActiveEnemyOnGround()
        {
            Enemy spawnedEvent = null;
            _system.Spawned += e => spawnedEvent = e;

            Enemy enemy = _system.Spawn(new Vector3(5f, 3f, 2f));

            Assert.That(_system.AliveCount, Is.EqualTo(1));
            Assert.That(enemy.IsActive, Is.True);
            Assert.That(enemy.Position, Is.EqualTo(new Vector3(5f, 0f, 2f)));
            Assert.That(enemy.Health.Current, Is.EqualTo(EnemyHealth));
            Assert.That(spawnedEvent, Is.SameAs(enemy));
        }

        [Test]
        public void Tick_MovesTowardsTargetBySpeed()
        {
            Enemy enemy = _system.Spawn(new Vector3(10f, 0f, 0f));

            _system.Tick(1f, Vector3.zero);

            Assert.That(enemy.Position.x, Is.EqualTo(8f).Within(1e-4f));
            Assert.That(enemy.Forward.x, Is.EqualTo(-1f).Within(1e-4f));
            Assert.That(enemy.IsInAttackRange, Is.False);
        }

        [Test]
        public void Tick_StopsAtAttackRange()
        {
            Enemy enemy = _system.Spawn(new Vector3(2f, 0f, 0f));

            _system.Tick(5f, Vector3.zero);

            Assert.That(enemy.Position.x, Is.EqualTo(Range).Within(1e-4f));
            Assert.That(enemy.IsInAttackRange, Is.True);
        }

        [Test]
        public void Tick_ArrivingInRange_AttacksSameFrame()
        {
            _system.Spawn(new Vector3(2f, 0f, 0f));

            _system.Tick(1f, Vector3.zero);

            Assert.That(_player.Current, Is.EqualTo(100 - Damage));
        }

        [Test]
        public void Tick_InRange_AttacksImmediatelyThenWaitsForInterval()
        {
            _system.Spawn(new Vector3(0.5f, 0f, 0f));

            _system.Tick(0.1f, Vector3.zero);
            Assert.That(_player.Current, Is.EqualTo(100 - Damage), "First contact should hit.");

            _system.Tick(0.5f, Vector3.zero);
            Assert.That(_player.Current, Is.EqualTo(100 - Damage), "Still on cooldown.");

            _system.Tick(0.5f, Vector3.zero);
            Assert.That(_player.Current, Is.EqualTo(100 - Damage * 2), "Cooldown elapsed.");
        }

        [Test]
        public void Tick_SeveralEnemiesInRange_DamageAddsUp()
        {
            _system.Spawn(new Vector3(0.5f, 0f, 0f));
            _system.Spawn(new Vector3(-0.5f, 0f, 0f));
            _system.Spawn(new Vector3(0f, 0f, 0.5f));

            _system.Tick(0.1f, Vector3.zero);

            Assert.That(_player.Current, Is.EqualTo(100 - Damage * 3));
        }

        [Test]
        public void Tick_OutOfRange_DoesNotDamage()
        {
            _system.Spawn(new Vector3(20f, 0f, 0f));

            _system.Tick(0.1f, Vector3.zero);

            Assert.That(_player.Current, Is.EqualTo(100));
        }

        [Test]
        public void Tick_WhenPlayerDiesAndListenerClears_DoesNotThrow()
        {
            // Regression guard: the player's death may clear all enemies while the system is ticking.
            var weakPlayer = new Health(Damage);
            var system = new EnemySystem(new EnemyConfig(EnemyHealth, Speed, Damage, AttackInterval, Range), weakPlayer);
            weakPlayer.Died += system.Clear;
            system.Spawn(new Vector3(0.5f, 0f, 0f));
            system.Spawn(new Vector3(-0.5f, 0f, 0f));

            Assert.DoesNotThrow(() => system.Tick(0.1f, Vector3.zero));
            Assert.That(system.AliveCount, Is.EqualTo(0));
        }

        [Test]
        public void ApplyDamage_NotLethal_KeepsEnemy()
        {
            Enemy enemy = _system.Spawn(Vector3.zero);

            _system.ApplyDamage(enemy, 1);

            Assert.That(enemy.Health.Current, Is.EqualTo(EnemyHealth - 1));
            Assert.That(_system.AliveCount, Is.EqualTo(1));
        }

        [Test]
        public void ApplyDamage_Lethal_RaisesDiedThenDespawned()
        {
            Enemy enemy = _system.Spawn(Vector3.zero);
            var events = new List<string>();
            _system.Died += _ => events.Add("died");
            _system.Despawned += _ => events.Add("despawned");

            _system.ApplyDamage(enemy, EnemyHealth);

            Assert.That(events, Is.EqualTo(new[] { "died", "despawned" }));
            Assert.That(enemy.IsActive, Is.False);
            Assert.That(_system.AliveCount, Is.EqualTo(0));
        }

        [Test]
        public void ApplyDamage_ToDespawnedEnemy_IsIgnored()
        {
            Enemy enemy = _system.Spawn(Vector3.zero);
            _system.ApplyDamage(enemy, EnemyHealth);
            int diedCount = 0;
            _system.Died += _ => diedCount++;

            _system.ApplyDamage(enemy, EnemyHealth);

            Assert.That(diedCount, Is.EqualTo(0));
        }

        [Test]
        public void KillingMiddleEnemy_KeepsOthersActiveAndTicking()
        {
            Enemy a = _system.Spawn(new Vector3(10f, 0f, 0f));
            Enemy b = _system.Spawn(new Vector3(0f, 0f, 10f));
            Enemy c = _system.Spawn(new Vector3(-10f, 0f, 0f));

            _system.ApplyDamage(b, EnemyHealth);
            _system.Tick(1f, Vector3.zero);

            Assert.That(_system.Active, Is.EquivalentTo(new[] { a, c }));
            Assert.That(a.Position.x, Is.EqualTo(8f).Within(1e-4f));
            Assert.That(c.Position.x, Is.EqualTo(-8f).Within(1e-4f));
        }

        [Test]
        public void Clear_DespawnsAllWithoutKills()
        {
            _system.Spawn(Vector3.zero);
            _system.Spawn(Vector3.one);
            int diedCount = 0;
            int despawnedCount = 0;
            _system.Died += _ => diedCount++;
            _system.Despawned += _ => despawnedCount++;

            _system.Clear();

            Assert.That(_system.AliveCount, Is.EqualTo(0));
            Assert.That(diedCount, Is.EqualTo(0));
            Assert.That(despawnedCount, Is.EqualTo(2));
        }

        private static EnemySystem CreateWithSeparation(float radius = 1f, float stiffness = 0.5f, float speed = 0f)
        {
            return new EnemySystem(new EnemyConfig(EnemyHealth, speed, Damage, AttackInterval, Range, radius, stiffness),
                new Health(100), arenaHalfSize: 20f);
        }

        [Test]
        public void Separation_Disabled_OverlappingEnemiesStay()
        {
            var system = new EnemySystem(new EnemyConfig(EnemyHealth, 0f, Damage, AttackInterval, Range), new Health(100));
            Enemy a = system.Spawn(new Vector3(10f, 0f, 0f));
            Enemy b = system.Spawn(new Vector3(10.2f, 0f, 0f));

            system.Tick(0.1f, Vector3.zero);

            Assert.That(a.Position.x, Is.EqualTo(10f));
            Assert.That(b.Position.x, Is.EqualTo(10.2f));
        }

        [Test]
        public void Separation_PushesCloseEnemiesApartSymmetrically()
        {
            EnemySystem system = CreateWithSeparation();
            Enemy a = system.Spawn(new Vector3(10f, 0f, 0f));
            Enemy b = system.Spawn(new Vector3(10.5f, 0f, 0f));

            system.Tick(0.1f, new Vector3(-20f, 0f, 0f));

            Assert.That(a.Position.x, Is.LessThan(10f));
            Assert.That(b.Position.x, Is.GreaterThan(10.5f));
            Assert.That(10f - a.Position.x, Is.EqualTo(b.Position.x - 10.5f).Within(1e-5f), "Equal and opposite pushes");
        }

        [Test]
        public void Separation_IgnoresEnemiesBeyondRadius()
        {
            EnemySystem system = CreateWithSeparation(radius: 1f);
            Enemy a = system.Spawn(new Vector3(10f, 0f, 0f));
            Enemy b = system.Spawn(new Vector3(11.5f, 0f, 0f));

            system.Tick(0.1f, Vector3.zero);

            Assert.That(a.Position.x, Is.EqualTo(10f));
            Assert.That(b.Position.x, Is.EqualTo(11.5f));
        }

        [Test]
        public void Separation_StackedEnemies_SpreadOutOverTime()
        {
            // 20 enemies spawned on the same spot must end up at least roughly one radius apart.
            EnemySystem system = CreateWithSeparation(radius: 1f, stiffness: 0.5f);
            for (int i = 0; i < 20; i++)
            {
                system.Spawn(new Vector3(5f, 0f, 5f));
            }

            for (int frame = 0; frame < 300; frame++)
            {
                system.Tick(1f / 60f, new Vector3(5f, 0f, 5f));
            }

            float closest = float.MaxValue;
            for (int i = 0; i < system.AliveCount; i++)
            {
                for (int j = i + 1; j < system.AliveCount; j++)
                {
                    closest = Mathf.Min(closest, Vector3.Distance(system.Active[i].Position, system.Active[j].Position));
                }
            }

            Assert.That(closest, Is.GreaterThan(0.6f));
        }

        [Test]
        public void Separation_ChecksOnlyNeighbours()
        {
            // 150 enemies scattered over a 24 x 24 m area around the player, like a busy benchmark frame:
            // each one is compared only with the enemies in the cells around it.
            EnemySystem system = CreateWithSeparation(radius: 1f);
            var random = new System.Random(11);
            for (int i = 0; i < 150; i++)
            {
                system.Spawn(new Vector3((float)(random.NextDouble() * 24 - 12), 0f, (float)(random.NextDouble() * 24 - 12)));
            }

            system.Tick(1f / 60f, Vector3.zero);

            TestContext.WriteLine($"Separation distance checks: {system.LastSeparationChecks}, brute force would be {150 * 149}");
            Assert.That(system.LastSeparationChecks, Is.GreaterThan(0), "The crowd must actually have neighbours.");
            Assert.That(system.LastSeparationChecks, Is.LessThan(150 * 149 / 10));
        }

        [Test]
        public void Separation_FullStiffness_ResolvesOverlapInOneFrame()
        {
            EnemySystem system = CreateWithSeparation(radius: 1f, stiffness: 1f);
            Enemy a = system.Spawn(new Vector3(10f, 0f, 0f));
            Enemy b = system.Spawn(new Vector3(10.6f, 0f, 0f));

            system.Tick(1f / 60f, new Vector3(-20f, 0f, 0f));

            Assert.That(Vector3.Distance(a.Position, b.Position), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void Separation_CrowdWalkingIn_KeepsSpacingAroundPlayer()
        {
            // The case that a force-based push lost: 150 enemies walking into the player from all sides must end up
            // spread around the player instead of squeezed into one blob by the ones walking in behind them.
            // Uses the game's tuning (radius 1.2 m, stiffness 1).
            var system = new EnemySystem(new EnemyConfig(1000, 2.5f, 1, 1f, 1.2f, 1.2f, 1f), new Health(1000000), 20f);
            var random = new System.Random(4);
            for (int i = 0; i < 150; i++)
            {
                float angle = (float)(random.NextDouble() * Mathf.PI * 2);
                float distance = 10f + (float)random.NextDouble() * 8f;
                system.Spawn(new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance));
            }

            for (int frame = 0; frame < 900; frame++)
            {
                system.Tick(1f / 60f, Vector3.zero);
            }

            // Average distance to the nearest other enemy; a single closest pair is too noisy to judge a crowd.
            float sumNearest = 0f;
            for (int i = 0; i < system.AliveCount; i++)
            {
                float nearest = float.MaxValue;
                for (int j = 0; j < system.AliveCount; j++)
                {
                    if (j != i)
                    {
                        nearest = Mathf.Min(nearest, Vector3.Distance(system.Active[i].Position, system.Active[j].Position));
                    }
                }

                sumNearest += nearest;
            }

            float averageNearest = sumNearest / system.AliveCount;
            TestContext.WriteLine($"Average nearest neighbour after 15 s: {averageNearest:F2} m");
            Assert.That(averageNearest, Is.GreaterThan(0.75f));
        }

        [TestCase(-1f, 0.5f)]
        [TestCase(1f, 1.5f)]
        public void Constructor_WithInvalidSeparation_Throws(float radius, float stiffness)
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new EnemyConfig(1, 1f, 1, 1f, 1f, radius, stiffness));
        }

        [Test]
        public void Spawn_AfterKill_ReusesPooledEnemyWithFullHealth()
        {
            Enemy first = _system.Spawn(Vector3.zero);
            _system.ApplyDamage(first, EnemyHealth);

            Enemy second = _system.Spawn(Vector3.zero);

            Assert.That(second, Is.SameAs(first), "The pooled instance should be reused.");
            Assert.That(second.Health.Current, Is.EqualTo(EnemyHealth));
            Assert.That(second.IsActive, Is.True);
        }
    }
}
