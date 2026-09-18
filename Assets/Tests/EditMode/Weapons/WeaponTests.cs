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
    }
}
