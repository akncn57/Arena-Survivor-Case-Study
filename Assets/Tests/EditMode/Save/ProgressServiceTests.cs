using System;
using ArenaSurvivor.Core.Save;
using NUnit.Framework;

namespace ArenaSurvivor.Tests.EditMode.Save
{
    public class ProgressServiceTests
    {
        /// <summary>In-memory ISaveService that records how often Save was called.</summary>
        private sealed class FakeSaveService : ISaveService
        {
            public SaveData Stored = new SaveData();
            public int SaveCount;

            public SaveData Load() => Stored;

            public void Save(SaveData data)
            {
                Stored = data;
                SaveCount++;
            }
        }

        [Test]
        public void TotalKills_StartsFromLoadedData()
        {
            var fake = new FakeSaveService { Stored = new SaveData { totalKills = 10 } };

            var progress = new ProgressService(fake);

            Assert.That(progress.TotalKills, Is.EqualTo(10));
        }

        [Test]
        public void AddKills_IncreasesTotalAndSaves()
        {
            var fake = new FakeSaveService { Stored = new SaveData { totalKills = 10 } };
            var progress = new ProgressService(fake);

            progress.AddKills(5);

            Assert.That(progress.TotalKills, Is.EqualTo(15));
            Assert.That(fake.Stored.totalKills, Is.EqualTo(15));
            Assert.That(fake.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public void AddKills_WithZero_DoesNotSave()
        {
            var fake = new FakeSaveService();
            var progress = new ProgressService(fake);

            progress.AddKills(0);

            Assert.That(fake.SaveCount, Is.EqualTo(0));
        }

        [Test]
        public void AddKills_WithNegative_Throws()
        {
            var progress = new ProgressService(new FakeSaveService());

            Assert.Throws<ArgumentOutOfRangeException>(() => progress.AddKills(-1));
        }

        [Test]
        public void Constructor_WithNullService_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ProgressService(null));
        }
    }
}
