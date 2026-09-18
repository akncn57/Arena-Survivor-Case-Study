using System;
using ArenaSurvivor.Core.Difficulty;
using ArenaSurvivor.Core.Enemies;
using ArenaSurvivor.Core.Player;
using ArenaSurvivor.Core.Save;
using ArenaSurvivor.Core.Session;
using ArenaSurvivor.Core.Weapons;
using UnityEngine;

namespace ArenaSurvivor.Core.World
{
    /// <summary>
    /// The whole game simulation in one object. Creates all gameplay systems, connects their events,
    /// runs them in a fixed order every frame and handles starting, replaying and leaving a run.
    /// The Unity side only feeds it joystick input and delta time, and draws what it exposes.
    /// </summary>
    public sealed class GameWorld
    {
        private readonly WorldConfig _world;
        private readonly WaveSpawner _spawner;

        public GameWorld(WorldConfig world, PlayerConfig player, EnemyConfig enemy, WeaponConfig weapon,
            ProgressService progress, System.Random random)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            Progress = progress ?? throw new ArgumentNullException(nameof(progress));

            Session = new GameSession();
            Player = new PlayerCharacter(player, world.ArenaHalfSize);
            Enemies = new EnemySystem(enemy, Player.Health);
            Projectiles = new ProjectileSystem(Enemies, world.ProjectileHitRadius);
            Weapon = new Weapon(weapon, Projectiles);
            _spawner = new WaveSpawner(Enemies, world.SpawnRadius, world.ArenaHalfSize, random);

            Enemies.Prewarm(world.EnemyPrewarm);
            Projectiles.Prewarm(world.ProjectilePrewarm);

            // Event wiring: this is the only place where systems are connected to each other.
            Enemies.Died += _ => Session.RegisterKill();
            Player.Health.Died += Session.NotifyPlayerDied;
            Session.Ended += OnRunEnded;
        }

        public GameSession Session { get; }
        public PlayerCharacter Player { get; }
        public EnemySystem Enemies { get; }
        public ProjectileSystem Projectiles { get; }
        public Weapon Weapon { get; }
        public ProgressService Progress { get; }

        /// <summary>Difficulty of the current or last run. Replay uses it again.</summary>
        public DifficultyConfig CurrentDifficulty { get; private set; }

        /// <summary>Result of the last finished run, for the result screen. Null until a run ends.</summary>
        public RunResult? LastResult { get; private set; }

        /// <summary>Starts a run. Also used for replay: everything from the previous run is cleared first.</summary>
        public void StartRun(DifficultyConfig difficulty)
        {
            CurrentDifficulty = difficulty ?? throw new ArgumentNullException(nameof(difficulty));

            ClearArena();
            Player.Reset(Vector3.zero);
            Weapon.Reset();
            _spawner.Begin(difficulty);
            LastResult = null;
            Session.Start(_world.RunDuration);
        }

        /// <summary>Starts a new run with the same difficulty.</summary>
        public void Replay()
        {
            if (CurrentDifficulty == null)
            {
                throw new InvalidOperationException("No run has been started yet.");
            }

            StartRun(CurrentDifficulty);
        }

        /// <summary>Leaves the run and clears the arena, e.g. to pick another difficulty.</summary>
        public void ReturnToMenu()
        {
            ClearArena();
            Session.ReturnToIdle();
        }

        /// <summary>
        /// Advances the simulation by one frame. Does nothing outside a run, so after a win or loss
        /// the arena stays frozen behind the result screen.
        /// </summary>
        public void Tick(float deltaTime, Vector2 moveInput)
        {
            if (!Session.IsPlaying || deltaTime <= 0f)
            {
                return;
            }

            // 1. The player moves first, so everything else reacts to the new position.
            Player.Move(deltaTime, moveInput);

            // 2. New enemies appear around the player.
            _spawner.Tick(deltaTime, Session.Progress, Player.Position);

            // 3. Enemies chase and attack. This may kill the player and end the run.
            Enemies.Tick(deltaTime, Player.Position);
            if (!Session.IsPlaying)
            {
                return;
            }

            // 4. The rifle picks a target and fires; the player turns to face the target.
            Weapon.Tick(deltaTime, Player.Position, Enemies.Active);
            if (Weapon.CurrentTarget != null)
            {
                Player.AimAt(Weapon.CurrentTarget.Position);
            }

            // 5. Bullets fly and hit enemies. Kills are counted through the Enemies.Died event.
            Projectiles.Tick(deltaTime);

            // 6. The timer runs last, so kills made in the final frame still count before a win.
            Session.Tick(deltaTime);
        }

        private void OnRunEnded(RunResult result)
        {
            LastResult = result;
            Progress.AddKills(result.Kills);
        }

        private void ClearArena()
        {
            Enemies.Clear();
            Projectiles.Clear();
        }
    }
}
