using System;
using System.IO;
using UnityEngine;

namespace ArenaSurvivor.Core.Save
{
    /// <summary>
    /// Stores <see cref="SaveData"/> as a JSON file.
    /// Writes are atomic: data goes to a temp file first, then replaces the real file,
    /// so a crash mid-write can never leave a half-written save behind.
    /// </summary>
    public sealed class JsonFileSaveService : ISaveService
    {
        private readonly string _filePath;
        private readonly string _tempPath;

        /// <param name="filePath">Full path of the save file, e.g. Application.persistentDataPath + "/save.json".</param>
        public JsonFileSaveService(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("Save file path must not be empty.", nameof(filePath));
            }

            _filePath = filePath;
            _tempPath = filePath + ".tmp";
        }

        public SaveData Load()
        {
            if (!File.Exists(_filePath))
            {
                return new SaveData();
            }

            try
            {
                string json = File.ReadAllText(_filePath);
                SaveData data = JsonUtility.FromJson<SaveData>(json);
                return data == null ? new SaveData() : Sanitize(data);
            }
            catch (Exception e)
            {
                // A corrupt or unreadable save must not block the game; start from defaults instead.
                Debug.LogWarning($"Save file could not be read, using defaults. {e.Message}");
                return new SaveData();
            }
        }

        public void Save(SaveData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            string directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_tempPath, JsonUtility.ToJson(data));

            if (File.Exists(_filePath))
            {
                File.Replace(_tempPath, _filePath, null);
            }
            else
            {
                File.Move(_tempPath, _filePath);
            }
        }

        private static SaveData Sanitize(SaveData data)
        {
            // Guards against hand-edited or tampered files.
            if (data.totalKills < 0)
            {
                data.totalKills = 0;
            }

            return data;
        }
    }
}
