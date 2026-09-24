using System;
using System.IO;
using ArenaSurvivor.Core.Save;
using NUnit.Framework;

namespace ArenaSurvivor.Tests.EditMode.Save
{
    public class JsonFileSaveServiceTests
    {
        private string _directory;
        private string _filePath;

        [SetUp]
        public void SetUp()
        {
            // Every test gets its own folder so tests never see each other's files.
            _directory = Path.Combine(Path.GetTempPath(), "ArenaSurvivorTests", Guid.NewGuid().ToString("N"));
            _filePath = Path.Combine(_directory, "save.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        [Test]
        public void Load_WhenFileMissing_ReturnsDefaults()
        {
            var service = new JsonFileSaveService(_filePath);

            SaveData data = service.Load();

            Assert.That(data, Is.Not.Null);
            Assert.That(data.totalKills, Is.EqualTo(0));
            Assert.That(data.version, Is.EqualTo(SaveData.CurrentVersion));
        }

        [Test]
        public void SaveThenLoad_ReturnsSameValues()
        {
            var service = new JsonFileSaveService(_filePath);

            service.Save(new SaveData { totalKills = 42 });
            SaveData loaded = new JsonFileSaveService(_filePath).Load();

            Assert.That(loaded.totalKills, Is.EqualTo(42));
            Assert.That(loaded.version, Is.EqualTo(SaveData.CurrentVersion));
        }

        [Test]
        public void Save_CreatesMissingDirectory()
        {
            var service = new JsonFileSaveService(_filePath);

            service.Save(new SaveData());

            Assert.That(File.Exists(_filePath), Is.True);
        }

        [Test]
        public void Save_OverwritesExistingFile_AndLeavesNoTempFile()
        {
            var service = new JsonFileSaveService(_filePath);

            service.Save(new SaveData { totalKills = 1 });
            service.Save(new SaveData { totalKills = 2 });

            Assert.That(service.Load().totalKills, Is.EqualTo(2));
            Assert.That(File.Exists(_filePath + ".tmp"), Is.False);
        }

        [Test]
        public void Load_WhenFileCorrupt_ReturnsDefaults()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_filePath, "{ this is not json");

            SaveData data = new JsonFileSaveService(_filePath).Load();

            Assert.That(data.totalKills, Is.EqualTo(0));
        }

        [Test]
        public void Load_WhenFileEmpty_ReturnsDefaults()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_filePath, string.Empty);

            SaveData data = new JsonFileSaveService(_filePath).Load();

            Assert.That(data, Is.Not.Null);
            Assert.That(data.totalKills, Is.EqualTo(0));
        }

        [Test]
        public void Load_WhenKillsNegative_ClampsToZero()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_filePath, "{\"version\":1,\"totalKills\":-5}");

            SaveData data = new JsonFileSaveService(_filePath).Load();

            Assert.That(data.totalKills, Is.EqualTo(0));
        }

        [Test]
        public void Load_WhenFieldMissing_UsesFieldDefault()
        {
            // Simulates an older save written before totalKills existed.
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_filePath, "{\"version\":1}");

            SaveData data = new JsonFileSaveService(_filePath).Load();

            Assert.That(data.totalKills, Is.EqualTo(0));
        }

        [Test]
        public void Save_WithNull_Throws()
        {
            var service = new JsonFileSaveService(_filePath);

            Assert.Throws<ArgumentNullException>(() => service.Save(null));
        }

        [TestCase(null)]
        [TestCase("")]
        public void Constructor_WithEmptyPath_Throws(string path)
        {
            Assert.Throws<ArgumentException>(() => new JsonFileSaveService(path));
        }

        [Test]
        public void Load_OldFileWithoutEndlessFields_DefaultsToZero()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_filePath, "{\"version\":1,\"totalKills\":12}");

            SaveData data = new JsonFileSaveService(_filePath).Load();

            Assert.That(data.totalKills, Is.EqualTo(12));
            Assert.That(data.bestEndlessSeconds, Is.EqualTo(0f));
            Assert.That(data.bestEndlessLevel, Is.EqualTo(0));
        }

        [Test]
        public void Load_NegativeEndlessRecords_AreClampedToZero()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_filePath, "{\"version\":1,\"totalKills\":1,\"bestEndlessSeconds\":-5.0,\"bestEndlessLevel\":-2}");

            SaveData data = new JsonFileSaveService(_filePath).Load();

            Assert.That(data.bestEndlessSeconds, Is.EqualTo(0f));
            Assert.That(data.bestEndlessLevel, Is.EqualTo(0));
        }
    }
}
