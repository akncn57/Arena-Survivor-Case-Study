using ArenaSurvivor.Core.Combat;
using ArenaSurvivor.Core.Enemies;
using ArenaSurvivor.Core.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace ArenaSurvivor.Tests.EditMode.Weapons
{
    public class TargetingTests
    {
        private EnemySystem _enemies;

        [SetUp]
        public void SetUp()
        {
            _enemies = new EnemySystem(new EnemyConfig(), new Health(100));
        }

        [Test]
        public void NoEnemies_ReturnsNull()
        {
            Assert.That(Targeting.FindNearest(_enemies.Active, Vector3.zero, 10f), Is.Null);
        }

        [Test]
        public void ReturnsNearestEnemy()
        {
            _enemies.Spawn(new Vector3(6f, 0f, 0f));
            Enemy near = _enemies.Spawn(new Vector3(0f, 0f, -3f));
            _enemies.Spawn(new Vector3(-5f, 0f, 5f));

            Assert.That(Targeting.FindNearest(_enemies.Active, Vector3.zero, 10f), Is.SameAs(near));
        }

        [Test]
        public void IgnoresEnemiesOutOfRange()
        {
            _enemies.Spawn(new Vector3(11f, 0f, 0f));

            Assert.That(Targeting.FindNearest(_enemies.Active, Vector3.zero, 10f), Is.Null);
        }

        [Test]
        public void EnemyExactlyAtRange_IsTargeted()
        {
            Enemy edge = _enemies.Spawn(new Vector3(10f, 0f, 0f));

            Assert.That(Targeting.FindNearest(_enemies.Active, Vector3.zero, 10f), Is.SameAs(edge));
        }

        [Test]
        public void MeasuresFromOrigin()
        {
            _enemies.Spawn(new Vector3(0f, 0f, 0f));
            Enemy nearOrigin = _enemies.Spawn(new Vector3(20f, 0f, 1f));

            Assert.That(Targeting.FindNearest(_enemies.Active, new Vector3(20f, 0f, 0f), 5f), Is.SameAs(nearOrigin));
        }
    }
}
