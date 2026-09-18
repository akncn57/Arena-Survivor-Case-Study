using System;

namespace ArenaSurvivor.Core.Session
{
    /// <summary>
    /// State of a single run: the survival timer, the kill count and the win/lose outcome.
    /// Time only advances through <see cref="Tick"/>, so the class has no dependency on Unity's clock
    /// and tests can simulate a whole run instantly.
    /// </summary>
    public sealed class GameSession
    {
        public GameState State { get; private set; } = GameState.Idle;
        public float Duration { get; private set; }
        public float Elapsed { get; private set; }
        public int Kills { get; private set; }

        public float Remaining => Math.Max(0f, Duration - Elapsed);

        /// <summary>Run progress from 0 (start) to 1 (timer finished). Drives difficulty ramp-up.</summary>
        public float Progress => Duration > 0f ? Math.Min(1f, Elapsed / Duration) : 0f;

        public bool IsPlaying => State == GameState.Playing;

        public event Action Started;
        public event Action<RunResult> Ended;

        /// <summary>Starts a new run. Also used for replay: all run values are reset.</summary>
        public void Start(float durationSeconds)
        {
            if (durationSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(durationSeconds), durationSeconds, "Duration must be positive.");
            }

            Duration = durationSeconds;
            Elapsed = 0f;
            Kills = 0;
            State = GameState.Playing;
            Started?.Invoke();
        }

        /// <summary>Advances the timer. Ends the run as won once the duration is reached.</summary>
        public void Tick(float deltaTime)
        {
            if (!IsPlaying || deltaTime <= 0f)
            {
                return;
            }

            Elapsed = Math.Min(Duration, Elapsed + deltaTime);

            if (Elapsed >= Duration)
            {
                End(GameState.Won);
            }
        }

        /// <summary>Counts a kill. Ignored outside a run, e.g. a projectile landing after the timer ended.</summary>
        public void RegisterKill()
        {
            if (IsPlaying)
            {
                Kills++;
            }
        }

        /// <summary>Ends the run as lost. Ignored if the run already ended (the player cannot lose after winning).</summary>
        public void NotifyPlayerDied()
        {
            if (IsPlaying)
            {
                End(GameState.Lost);
            }
        }

        /// <summary>Returns to <see cref="GameState.Idle"/>, e.g. when going back to difficulty selection.</summary>
        public void ReturnToIdle()
        {
            State = GameState.Idle;
        }

        private void End(GameState outcome)
        {
            State = outcome;
            Ended?.Invoke(new RunResult(outcome, Kills, Elapsed));
        }
    }
}
