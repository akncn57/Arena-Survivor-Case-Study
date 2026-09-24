using System;
using ArenaSurvivor.Core.Save;
using ArenaSurvivor.Tests.EditMode.TestDoubles;
using NUnit.Framework;

namespace ArenaSurvivor.Tests.EditMode.Save
{
    public class ProgressServiceTests
    {
        [Test]
        public void TotalKills_StartsFromLoadedData()
        {
            var fake = new InMemorySaveService { Stored = new SaveData { totalKills = 10 } };

            var progress = new ProgressService(fake);

            Assert.That(progress.TotalKills, Is.EqualTo(10));
        }

        [Test]
        public void AddKills_IncreasesTotalAndSaves()
        {
            var fake = new InMemorySaveService { Stored = new SaveData { totalKills = 10 } };
            var progress = new ProgressService(fake);

            progress.AddKills(5);

            Assert.That(progress.TotalKills, Is.EqualTo(15));
            Assert.That(fake.Stored.totalKills, Is.EqualTo(15));
            Assert.That(fake.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public void AddKills_WithZero_DoesNotSave()
        {
            var fake = new InMemorySaveService();
            var progress = new ProgressService(fake);

            progress.AddKills(0);

            Assert.That(fake.SaveCount, Is.EqualTo(0));
        }

        [Test]
        public void AddKills_WithNegative_Throws()
        {
            var progress = new ProgressService(new InMemorySaveService());

            Assert.Throws<ArgumentOutOfRangeException>(() => progress.AddKills(-1));
        }

        [Test]
        public void Constructor_WithNullService_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ProgressService(null));
        }

        [Test]
        public void RecordEndlessRun_KeepsBestTimeAndLevelSeparately()
        {
            var save = new InMemorySaveService();
            var progress = new ProgressService(save);

            Assert.That(progress.RecordEndlessRun(120f, 8), Is.True);
            Assert.That(progress.RecordEndlessRun(90f, 10), Is.False, "Higher level but shorter run: not a new best time.");

            Assert.That(progress.BestEndlessSeconds, Is.EqualTo(120f));
            Assert.That(progress.BestEndlessLevel, Is.EqualTo(10));
            Assert.That(save.Stored.bestEndlessSeconds, Is.EqualTo(120f));
            Assert.That(save.Stored.bestEndlessLevel, Is.EqualTo(10));
        }

        [Test]
        public void RecordEndlessRun_NoImprovement_DoesNotSave()
        {
            var save = new InMemorySaveService();
            var progress = new ProgressService(save);
            progress.RecordEndlessRun(120f, 8);
            int saves = save.SaveCount;

            bool record = progress.RecordEndlessRun(60f, 3);

            Assert.That(record, Is.False);
            Assert.That(save.SaveCount, Is.EqualTo(saves));
        }

        [Test]
        public void RecordEndlessRun_Negative_Throws()
        {
            var progress = new ProgressService(new InMemorySaveService());

            Assert.Throws<ArgumentOutOfRangeException>(() => progress.RecordEndlessRun(-1f, 1));
        }
    }
}
