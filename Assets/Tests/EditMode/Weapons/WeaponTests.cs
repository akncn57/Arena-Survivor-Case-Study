using ArenaSurvivor.Core.Combat;
using ArenaSurvivor.Core.Enemies;
using ArenaSurvivor.Core.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace ArenaSurvivor.Tests.EditMode.Weapons
{
    public class WeaponTests
    {
        private const float FireInterval = 0.5f;
        private const float Range = 8f;

        private EnemySystem _enemies;
        private ProjectileSystem _projectiles;
        private Weapon _weapon;
        private int _firedCount;

        [SetUp]
        public void SetUp()
        {
            _enemies = new EnemySystem(new EnemyConfig(), new Health(100));
            _projectiles = new ProjectileSystem(_enemies, 0.5f);
            _weapon = new Weapon(new WeaponConfig(1, FireInterval, Range, 20f), _projectiles);
            _firedCount = 0;
            _weapon.Fired += () => _firedCount++;
        }

        [Test]
        public void NoTarget_DoesNotFire()
        {
            _weapon.Tick(0.1f, Vector3.zero, _enemies.Active);

            Assert.That(_weapon.CurrentTarget, Is.Null);
            Assert.That(_projectiles.ActiveCount, Is.EqualTo(0));
            Assert.That(_firedCount, Is.EqualTo(0));
        }

        [Test]
        public void TargetInRange_FiresTowardsIt()
        {
            Enemy enemy = _enemies.Spawn(new Vector3(0f, 0f, 5f));

            _weapon.Tick(0.1f, Vector3.zero, _enemies.Active);

            Assert.That(_weapon.CurrentTarget, Is.SameAs(enemy));
            Assert.That(_projectiles.ActiveCount, Is.EqualTo(1));
            Assert.That(_projectiles.Active[0].Direction, Is.EqualTo(Vector3.forward));
            Assert.That(_firedCount, Is.EqualTo(1));
        }

        [Test]
        public void FiresOncePerInterval()
        {
            _enemies.Spawn(new Vector3(0f, 0f, 7f));

            // 1 second in 0.1 s steps with a 0.5 s interval: shots at 0.1 and 0.6 s (the first is immediate).
            for (int i = 0; i < 10; i++)
            {
                _weapon.Tick(0.1f, Vector3.zero, _enemies.Active);
            }

            Assert.That(_firedCount, Is.EqualTo(2));
        }

        [Test]
        public void TargetOutOfRange_DoesNotFire()
        {
            _enemies.Spawn(new Vector3(0f, 0f, Range + 1f));

            _weapon.Tick(0.1f, Vector3.zero, _enemies.Active);

            Assert.That(_firedCount, Is.EqualTo(0));
        }

        [Test]
        public void TargetOnTopOfPlayer_StillFires()
        {
            _enemies.Spawn(Vector3.zero);

            _weapon.Tick(0.1f, Vector3.zero, _enemies.Active);

            Assert.That(_firedCount, Is.EqualTo(1));
        }

        [Test]
        public void Reset_AllowsImmediateShot()
        {
            _enemies.Spawn(new Vector3(0f, 0f, 5f));
            _weapon.Tick(0.1f, Vector3.zero, _enemies.Active);

            _weapon.Reset();
            _weapon.Tick(0.01f, Vector3.zero, _enemies.Active);

            Assert.That(_firedCount, Is.EqualTo(2));
        }

        [Test]
        public void Multishot_FiresFanCentredOnTarget()
        {
            _enemies.Spawn(new Vector3(0f, 0f, 5f));
            _weapon.ExtraProjectiles = 2;

            _weapon.Tick(0.1f, Vector3.zero, _enemies.Active);

            Assert.That(_projectiles.ActiveCount, Is.EqualTo(3));
            Assert.That(_firedCount, Is.EqualTo(1), "One shot event per volley, not per projectile.");

            // Default spread 12 degrees: one straight at the target, one on each side.
            float[] angles = new float[3];
            for (int i = 0; i < 3; i++)
            {
                Vector3 d = _projectiles.Active[i].Direction;
                angles[i] = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                Assert.That(d.magnitude, Is.EqualTo(1f).Within(1e-4f));
            }

            System.Array.Sort(angles);
            Assert.That(angles[0], Is.EqualTo(-12f).Within(1e-3f));
            Assert.That(angles[1], Is.EqualTo(0f).Within(1e-3f));
            Assert.That(angles[2], Is.EqualTo(12f).Within(1e-3f));
        }

        [Test]
        public void EvenMultishot_StraddlesTarget()
        {
            _enemies.Spawn(new Vector3(0f, 0f, 5f));
            _weapon.ExtraProjectiles = 1;

            _weapon.Tick(0.1f, Vector3.zero, _enemies.Active);

            float sum = 0f;
            foreach (Projectile p in _projectiles.Active)
            {
                sum += Mathf.Atan2(p.Direction.x, p.Direction.z) * Mathf.Rad2Deg;
            }

            Assert.That(_projectiles.ActiveCount, Is.EqualTo(2));
            Assert.That(sum, Is.EqualTo(0f).Within(1e-3f), "Two projectiles at -6 and +6 degrees.");
        }

        [Test]
        public void FireRateMultiplier_ShortensInterval()
        {
            _enemies.Spawn(new Vector3(0f, 0f, 7f));
            _weapon.FireRateMultiplier = 2f; // 0.5 s -> 0.25 s

            for (int i = 0; i < 10; i++)
            {
                _weapon.Tick(0.1f, Vector3.zero, _enemies.Active);
            }

            Assert.That(_weapon.FireInterval, Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(_firedCount, Is.EqualTo(4), "Shots at 0.1, 0.4, 0.7 and 1.0 s.");
        }

        [Test]
        public void DamageMultiplier_RoundsAndNeverDropsBelowOne()
        {
            var weapon = new Weapon(new WeaponConfig(10, FireInterval, Range, 20f), _projectiles);

            weapon.DamageMultiplier = 1.3f;
            Assert.That(weapon.Damage, Is.EqualTo(13));
            weapon.DamageMultiplier = 0.01f;
            Assert.That(weapon.Damage, Is.EqualTo(1));
        }

        [Test]
        public void RangeMultiplier_TargetsFartherEnemies()
        {
            _enemies.Spawn(new Vector3(0f, 0f, Range + 2f));

            _weapon.Tick(0.1f, Vector3.zero, _enemies.Active);
            Assert.That(_weapon.CurrentTarget, Is.Null);

            _weapon.RangeMultiplier = 1.5f;
            _weapon.Tick(0.1f, Vector3.zero, _enemies.Active);
            Assert.That(_weapon.CurrentTarget, Is.Not.Null);
        }

        [Test]
        public void Reset_ClearsUpgrades()
        {
            _weapon.DamageMultiplier = 2f;
            _weapon.FireRateMultiplier = 2f;
            _weapon.RangeMultiplier = 2f;
            _weapon.ExtraProjectiles = 3;

            _weapon.Reset();

            Assert.That(_weapon.DamageMultiplier, Is.EqualTo(1f));
            Assert.That(_weapon.FireRateMultiplier, Is.EqualTo(1f));
            Assert.That(_weapon.RangeMultiplier, Is.EqualTo(1f));
            Assert.That(_weapon.ProjectilesPerShot, Is.EqualTo(1));
        }
    }
}
