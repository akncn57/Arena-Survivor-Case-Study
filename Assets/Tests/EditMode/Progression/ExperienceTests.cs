using System;
using System.Collections.Generic;
using ArenaSurvivor.Core.Progression;
using NUnit.Framework;

namespace ArenaSurvivor.Tests.EditMode.Progression
{
    public class ExperienceTests
    {
        // Level 1 -> 2 needs 5 XP, then 8, 11, ...
        private static Experience Create() => new Experience(new LevelingConfig(5, 3));

        [Test]
        public void Curve_GrowsLinearly()
        {
            var config = new LevelingConfig(5, 3);

            Assert.That(config.ExperienceToNext(1), Is.EqualTo(5));
            Assert.That(config.ExperienceToNext(2), Is.EqualTo(8));
            Assert.That(config.ExperienceToNext(10), Is.EqualTo(32));
        }

        [Test]
        public void NewExperience_IsLevelOneEmpty()
        {
            Experience xp = Create();

            Assert.That(xp.Level, Is.EqualTo(1));
            Assert.That(xp.Current, Is.EqualTo(0));
            Assert.That(xp.Normalized, Is.EqualTo(0f));
        }

        [Test]
        public void Add_BelowThreshold_OnlyFillsBar()
        {
            Experience xp = Create();
            int levelUps = 0;
            xp.LeveledUp += _ => levelUps++;

            xp.Add(4);

            Assert.That(xp.Level, Is.EqualTo(1));
            Assert.That(xp.Normalized, Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(levelUps, Is.EqualTo(0));
        }

        [Test]
        public void Add_ReachingThreshold_LevelsUpAndCarriesOverRest()
        {
            Experience xp = Create();
            var levels = new List<int>();
            xp.LeveledUp += levels.Add;

            xp.Add(7);

            Assert.That(xp.Level, Is.EqualTo(2));
            Assert.That(xp.Current, Is.EqualTo(2));
            Assert.That(xp.ToNext, Is.EqualTo(8));
            Assert.That(levels, Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void Add_LargeAmount_RaisesEveryLevelOnce()
        {
            Experience xp = Create();
            var levels = new List<int>();
            xp.LeveledUp += levels.Add;

            xp.Add(5 + 8 + 11 + 1);

            Assert.That(xp.Level, Is.EqualTo(4));
            Assert.That(xp.Current, Is.EqualTo(1));
            Assert.That(levels, Is.EqualTo(new[] { 2, 3, 4 }));
        }

        [Test]
        public void Reset_BackToLevelOne()
        {
            Experience xp = Create();
            xp.Add(30);

            xp.Reset();

            Assert.That(xp.Level, Is.EqualTo(1));
            Assert.That(xp.Current, Is.EqualTo(0));
        }

        [Test]
        public void InvalidValues_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Create().Add(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LevelingConfig(0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LevelingConfig(5, 3).ExperienceToNext(0));
        }
    }
}
