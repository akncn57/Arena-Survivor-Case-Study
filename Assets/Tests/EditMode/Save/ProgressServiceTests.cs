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
    }
}
