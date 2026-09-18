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
    }
}
