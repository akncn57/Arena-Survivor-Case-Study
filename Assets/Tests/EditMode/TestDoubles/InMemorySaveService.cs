using ArenaSurvivor.Core.Save;

namespace ArenaSurvivor.Tests.EditMode.TestDoubles
{
    /// <summary>ISaveService that keeps data in memory and counts saves. Shared by tests that must not touch the disk.</summary>
    public sealed class InMemorySaveService : ISaveService
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
}
