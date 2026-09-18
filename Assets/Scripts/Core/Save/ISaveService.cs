namespace ArenaSurvivor.Core.Save
{
    /// <summary>
    /// Loads and stores <see cref="SaveData"/>. Game code depends on this interface,
    /// so tests can swap in an in-memory fake instead of touching the disk.
    /// </summary>
    public interface ISaveService
    {
        /// <summary>Returns the stored data, or fresh defaults when nothing valid is stored. Never returns null.</summary>
        SaveData Load();

        void Save(SaveData data);
    }
}
