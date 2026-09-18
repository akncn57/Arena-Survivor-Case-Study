namespace ArenaSurvivor.Core.Session
{
    public enum GameState
    {
        /// <summary>No run in progress (difficulty selection).</summary>
        Idle,
        Playing,
        /// <summary>The player survived until the timer ran out.</summary>
        Won,
        /// <summary>The player died before the timer ran out.</summary>
        Lost
    }
}
