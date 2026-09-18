using System;
using ArenaSurvivor.Core.Difficulty;
using NUnit.Framework;

namespace ArenaSurvivor.Tests.EditMode.Difficulty
{
    public class DifficultyConfigTests
    {
        private static DifficultyConfig CreateConfig()
        {
            return new DifficultyConfig(
                startSpawnInterval: 4f, endSpawnInterval: 1f,
                startWaveSize: 2, endWaveSize: 10,
                maxAliveEnemies: 50);
        }

        [Test]
        public void AtStart_UsesStartValues()
        {
            DifficultyConfig config = CreateConfig();

            Assert.That(config.GetSpawnInterval(0f), Is.EqualTo(4f));
            Assert.That(config.GetWaveSize(0f), Is.EqualTo(2));
        }

        [Test]
        public void AtEnd_UsesEndValues()
        {
            DifficultyConfig config = CreateConfig();

            Assert.That(config.GetSpawnInterval(1f), Is.EqualTo(1f));
            Assert.That(config.GetWaveSize(1f), Is.EqualTo(10));
        }

        [Test]
        public void Midway_InterpolatesLinearly()
        {
            DifficultyConfig config = CreateConfig();

            Assert.That(config.GetSpawnInterval(0.5f), Is.EqualTo(2.5f));
            Assert.That(config.GetWaveSize(0.5f), Is.EqualTo(6));
        }

        [TestCase(-1f, 4f, 2)]
        [TestCase(2f, 1f, 10)]
        public void ProgressOutsideRange_IsClamped(float progress, float expectedInterval, int expectedWave)
        {
            DifficultyConfig config = CreateConfig();

            Assert.That(config.GetSpawnInterval(progress), Is.EqualTo(expectedInterval));
            Assert.That(config.GetWaveSize(progress), Is.EqualTo(expectedWave));
        }

        [Test]
        public void MaxAliveEnemies_ReturnsConfiguredValue()
        {
            Assert.That(CreateConfig().MaxAliveEnemies, Is.EqualTo(50));
        }

        [Test]
        public void Constructor_WithNonPositiveInterval_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DifficultyConfig(0f, 1f, 1, 1, 1));
        }

        [Test]
        public void Constructor_WithZeroWaveSize_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DifficultyConfig(1f, 1f, 0, 1, 1));
        }

        [Test]
        public void Constructor_WithZeroMaxAlive_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DifficultyConfig(1f, 1f, 1, 1, 0));
        }
    }
}
