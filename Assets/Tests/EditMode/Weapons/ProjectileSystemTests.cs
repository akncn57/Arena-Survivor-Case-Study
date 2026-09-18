using System;
using ArenaSurvivor.Core.Combat;
using ArenaSurvivor.Core.Enemies;
using ArenaSurvivor.Core.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace ArenaSurvivor.Tests.EditMode.Weapons
{
    public class ProjectileSystemTests
    {
        private const int EnemyHealth = 2;
        private const float HitRadius = 0.5f;

        private EnemySystem _enemies;
        private ProjectileSystem _projectiles;

        [SetUp]
        public void SetUp()
        {
            _enemies = new EnemySystem(new EnemyConfig(EnemyHealth, 0f, 10, 1f, 1f), new Health(100));
            _projectiles = new ProjectileSystem(_enemies, HitRadius);
        }

        private Projectile FireForward(float speed = 10f, float distance = 20f, int damage = 1)
        {
            return _projectiles.Fire(Vector3.zero, Vector3.forward, speed, distance, damage);
        }

        [Test]
        public void Fire_AddsActiveProjectile()
        {
            Projectile spawnedEvent = null;
            _projectiles.Spawned += p => spawnedEvent = p;

            Projectile projectile = FireForward();

            Assert.That(_projectiles.ActiveCount, Is.EqualTo(1));
            Assert.That(projectile.IsActive, Is.True);
            Assert.That(spawnedEvent, Is.SameAs(projectile));
        }

        [Test]
        public void Tick_MovesBySpeed()
        {
            Projectile projectile = FireForward(speed: 10f);

            _projectiles.Tick(0.5f);

            Assert.That(projectile.Position.z, Is.EqualTo(5f).Within(1e-4f));
        }

        [Test]
        public void Tick_AfterTravelDistance_Despawns()
        {
            int despawned = 0;
            _projectiles.Despawned += _ => despawned++;
            FireForward(speed: 10f, distance: 3f);

            _projectiles.Tick(1f);

            Assert.That(_projectiles.ActiveCount, Is.EqualTo(0));
            Assert.That(despawned, Is.EqualTo(1));
        }

        [Test]
        public void Tick_HittingEnemy_DamagesAndDespawns()
        {
            Enemy enemy = _enemies.Spawn(new Vector3(0f, 0f, 3f));
            Vector3? hitPoint = null;
            _projectiles.Hit += p => hitPoint = p;
            FireForward(speed: 10f);

            _projectiles.Tick(0.5f);

            Assert.That(enemy.Health.Current, Is.EqualTo(EnemyHealth - 1));
            Assert.That(_projectiles.ActiveCount, Is.EqualTo(0));
            Assert.That(hitPoint.HasValue, Is.True);
            Assert.That(hitPoint.Value.z, Is.EqualTo(3f).Within(1e-4f));
        }

        [Test]
        public void Tick_FastProjectile_DoesNotTunnelThroughEnemy()
        {
            // 100 units in one frame; a point check at the end position would miss the enemy at z = 3.
            Enemy enemy = _enemies.Spawn(new Vector3(0f, 0f, 3f));
            FireForward(speed: 100f, distance: 200f);

            _projectiles.Tick(1f);

            Assert.That(enemy.Health.Current, Is.EqualTo(EnemyHealth - 1));
        }

        [Test]
        public void Tick_EnemyBesidePath_IsNotHit()
        {
            Enemy enemy = _enemies.Spawn(new Vector3(HitRadius + 0.1f, 0f, 3f));
            FireForward(speed: 10f);

            _projectiles.Tick(1f);

            Assert.That(enemy.Health.Current, Is.EqualTo(EnemyHealth));
        }

        [Test]
        public void Tick_LethalHit_KillsEnemy()
        {
            int deaths = 0;
            _enemies.Died += _ => deaths++;
            _enemies.Spawn(new Vector3(0f, 0f, 3f));
            FireForward(damage: EnemyHealth);

            _projectiles.Tick(1f);

            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(_enemies.AliveCount, Is.EqualTo(0));
        }

        [Test]
        public void Tick_TwoProjectilesOneEnemy_SecondKillsThenFliesOn()
        {
            _enemies.Spawn(new Vector3(0f, 0f, 3f));
            FireForward(speed: 10f);
            FireForward(speed: 10f);

            _projectiles.Tick(0.5f);

            Assert.That(_enemies.AliveCount, Is.EqualTo(0), "Two hits of 1 damage kill a 2 HP enemy.");
            Assert.That(_projectiles.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void Tick_SeveralProjectiles_OnlyExpiredOnesDespawn()
        {
            Projectile shortRange = FireForward(speed: 10f, distance: 1f);
            Projectile longRange = FireForward(speed: 10f, distance: 20f);

            _projectiles.Tick(0.5f);

            Assert.That(shortRange.IsActive, Is.False);
            Assert.That(longRange.IsActive, Is.True);
            Assert.That(_projectiles.Active, Is.EquivalentTo(new[] { longRange }));
        }

        [Test]
        public void Clear_RemovesAll()
        {
            FireForward();
            FireForward();

            _projectiles.Clear();

            Assert.That(_projectiles.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void Fire_AfterDespawn_ReusesPooledProjectile()
        {
            Projectile first = FireForward(distance: 1f);
            _projectiles.Tick(1f);

            Projectile second = FireForward();

            Assert.That(second, Is.SameAs(first));
            Assert.That(second.Position, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Constructor_WithNonPositiveRadius_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectileSystem(_enemies, 0f));
        }
    }
}
