using System;
using ArenaSurvivor.Core.Endless;
using NUnit.Framework;

namespace ArenaSurvivor.Tests.EditMode.Endless
{
    public class EndlessConfigTests
    {
        [Test]
        public void Scaling_StartsAtOneAndGrowsPerMinute()
        {
            var scaling = new EnemyScalingConfig(healthPerMinute: 0.3f, speedPerMinute: 0.1f, maxSpeedMultiplier: 1.5f, damagePerMinute: 0.2f);

            Assert.That(scaling.HealthMultiplier(0f), Is.EqualTo(1f));
            Assert.That(scaling.SpeedMultiplier(0f), Is.EqualTo(1f));
            Assert.That(scaling.DamageMultiplier(0f), Is.EqualTo(1f));

            Assert.That(scaling.HealthMultiplier(120f), Is.EqualTo(1.6f).Within(1e-5f));
            Assert.That(scaling.SpeedMultiplier(120f), Is.EqualTo(1.2f).Within(1e-5f));
            Assert.That(scaling.DamageMultiplier(120f), Is.EqualTo(1.4f).Within(1e-5f));
        }

        [Test]
        public void Speed_IsCapped_HealthAndDamageAreNot()
        {
            var scaling = new EnemyScalingConfig(0.3f, 0.1f, 1.5f, 0.2f);

            Assert.That(scaling.SpeedMultiplier(3600f), Is.EqualTo(1.5f));
            Assert.That(scaling.HealthMultiplier(3600f), Is.EqualTo(19f).Within(1e-3f));
            Assert.That(scaling.DamageMultiplier(3600f), Is.EqualTo(13f).Within(1e-3f));
        }

        [Test]
        public void SpawnProgress_RampsOverRampSecondsThenStays()
        {
            var config = new EndlessConfig();

            Assert.That(config.SpawnProgress(0f), Is.EqualTo(0f));
            Assert.That(config.SpawnProgress(config.RampSeconds * 0.5f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(config.SpawnProgress(config.RampSeconds * 3f), Is.EqualTo(1f));
        }

        [Test]
        public void Defaults_HaveUpgradesAndSaneValues()
        {
            var config = new EndlessConfig();

            Assert.That(config.Upgrades.Count, Is.GreaterThanOrEqualTo(config.CardsPerLevel));
            Assert.That(config.CardsPerLevel, Is.EqualTo(3));
            Assert.That(config.Spawn.MaxAliveEnemies, Is.GreaterThan(0));
        }

        [Test]
        public void InvalidValues_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemyScalingConfig(-1f, 0f, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemyScalingConfig(0f, 0f, 0.5f, 0f));
        }
    }
}
