using System;

namespace ArenaSurvivor.Core.Save
{
    /// <summary>
    /// Game-facing API for persistent progress. Loads once on creation,
    /// keeps the data in memory and saves whenever it changes.
    /// </summary>
    public sealed class ProgressService
    {
        private readonly ISaveService _saveService;
        private readonly SaveData _data;

        public ProgressService(ISaveService saveService)
        {
            _saveService = saveService ?? throw new ArgumentNullException(nameof(saveService));
            _data = _saveService.Load();
        }

        public int TotalKills => _data.totalKills;

        /// <summary>Longest endless run in seconds, 0 if none yet.</summary>
        public float BestEndlessSeconds => _data.bestEndlessSeconds;

        /// <summary>Highest level reached in an endless run, 0 if none yet.</summary>
        public int BestEndlessLevel => _data.bestEndlessLevel;

        /// <summary>Adds the kills of a finished run to the lifetime total and saves immediately.</summary>
        public void AddKills(int kills)
        {
            if (kills < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(kills), kills, "Kill count cannot be negative.");
            }

            if (kills == 0)
            {
                return;
            }

            _data.totalKills += kills;
            _saveService.Save(_data);
        }

        /// <summary>
        /// Records a finished endless run. Best time and best level are tracked separately; saves only if one improved.
        /// </summary>
        /// <returns>True if the run set a new best time.</returns>
        public bool RecordEndlessRun(float survivedSeconds, int level)
        {
            if (survivedSeconds < 0f || float.IsNaN(survivedSeconds) || level < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(survivedSeconds), "Time and level cannot be negative.");
            }

            bool newBestTime = survivedSeconds > _data.bestEndlessSeconds;
            bool newBestLevel = level > _data.bestEndlessLevel;

            if (newBestTime)
            {
                _data.bestEndlessSeconds = survivedSeconds;
            }

            if (newBestLevel)
            {
                _data.bestEndlessLevel = level;
            }

            if (newBestTime || newBestLevel)
            {
                _saveService.Save(_data);
            }

            return newBestTime;
        }
    }
}
