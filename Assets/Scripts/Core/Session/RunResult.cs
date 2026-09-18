namespace ArenaSurvivor.Core.Session
{
    /// <summary>Summary of a finished run, passed to listeners of <see cref="GameSession.Ended"/>.</summary>
    public readonly struct RunResult
    {
        public RunResult(GameState outcome, int kills, float survivedSeconds)
        {
            Outcome = outcome;
            Kills = kills;
            SurvivedSeconds = survivedSeconds;
        }

        /// <summary><see cref="GameState.Won"/> or <see cref="GameState.Lost"/>.</summary>
        public GameState Outcome { get; }
        public int Kills { get; }
        public float SurvivedSeconds { get; }
    }
}
