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
