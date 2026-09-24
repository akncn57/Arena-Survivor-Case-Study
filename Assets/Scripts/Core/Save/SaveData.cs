using System;

namespace ArenaSurvivor.Core.Save
{
    /// <summary>
    /// Persistent player progress, serialized to JSON with JsonUtility.
    /// New fields must have safe defaults so older save files still load.
    /// Bump <see cref="CurrentVersion"/> only when a field changes meaning and needs migration.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        // Public lowercase fields: JsonUtility serializes public fields, and the names become the JSON keys.
        public int version = CurrentVersion;
        public int totalKills;

        /// <summary>Longest endless run, in seconds. Added in the endless update; old files load it as 0.</summary>
        public float bestEndlessSeconds;

        /// <summary>Highest level reached in an endless run (may come from a different run than the best time).</summary>
        public int bestEndlessLevel;
    }
}
