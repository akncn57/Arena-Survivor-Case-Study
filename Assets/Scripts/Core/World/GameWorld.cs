using System;
using System.Collections.Generic;
using ArenaSurvivor.Core.Difficulty;
using ArenaSurvivor.Core.Endless;
using ArenaSurvivor.Core.Enemies;
using ArenaSurvivor.Core.Player;
using ArenaSurvivor.Core.Progression;
using ArenaSurvivor.Core.Save;
using ArenaSurvivor.Core.Session;
using ArenaSurvivor.Core.Upgrades;
using ArenaSurvivor.Core.Weapons;
using UnityEngine;

namespace ArenaSurvivor.Core.World
{
    /// <summary>
    /// The whole game simulation in one object. Creates all gameplay systems, connects their events,
    /// runs them in a fixed order every frame and handles starting, replaying and leaving a run.
    /// The Unity side only feeds it joystick input and delta time, and draws what it exposes.
    ///
    /// Two modes share the same systems:
    /// - <b>Timed</b> (<see cref="StartRun"/>): the case study game. Survive the timer on a chosen difficulty.
    /// - <b>Endless</b> (<see cref="StartEndless"/>): no timer; enemies drop XP and health, level ups offer upgrade
    ///   cards, and enemies grow stronger over time. Drops, XP and cards only exist in this mode.
    /// </summary>
    public sealed class GameWorld
    {
        private readonly WorldConfig _world;
        private readonly WaveSpawner _spawner;
        private readonly System.Random _random;
        private readonly List<UpgradeEntry> _offer = new List<UpgradeEntry>();
        private int _pendingLevelUps;
        private bool _hasStarted;

        /// <param name="endless">Endless mode settings. Null uses the defaults of <see cref="EndlessConfig"/>.</param>
        public GameWorld(WorldConfig world, PlayerConfig player, EnemyConfig enemy, WeaponConfig weapon,
            ProgressService progress, System.Random random, EndlessConfig endless = null)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            Progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            Endless = endless ?? new EndlessConfig();

            Session = new GameSession();
            Player = new PlayerCharacter(player, world.ArenaHalfSize);
            Enemies = new EnemySystem(enemy, Player.Health, world.ArenaHalfSize);
            Projectiles = new ProjectileSystem(Enemies, world.ProjectileHitRadius);
            Weapon = new Weapon(weapon, Projectiles);
            _spawner = new WaveSpawner(Enemies, world.SpawnRadius, world.ArenaHalfSize, random);
            Pickups = new PickupSystem(Endless.Pickups);
            Experience = new Experience(Endless.Leveling);
            Upgrades = new UpgradeSystem(Endless.Upgrades, Weapon, Player, Pickups);

            Enemies.Prewarm(world.EnemyPrewarm);
            Projectiles.Prewarm(world.ProjectilePrewarm);
            Pickups.Prewarm(world.PickupPrewarm);

            // Event wiring: this is the only place where systems are connected to each other.
            Enemies.Died += _ => Session.RegisterKill();
            Enemies.Died += OnEnemyDied;
            Pickups.Collected += OnPickupCollected;
            Experience.LeveledUp += OnLeveledUp;
            Player.Health.Died += Session.NotifyPlayerDied;
            Session.Ended += OnRunEnded;
        }

        public GameSession Session { get; }
        public PlayerCharacter Player { get; }
        public EnemySystem Enemies { get; }
        public ProjectileSystem Projectiles { get; }
        public Weapon Weapon { get; }
        public ProgressService Progress { get; }
        public PickupSystem Pickups { get; }
        public Experience Experience { get; }
        public UpgradeSystem Upgrades { get; }
        public EndlessConfig Endless { get; }

        /// <summary>True if the current or last run is an endless run.</summary>
        public bool IsEndless { get; private set; }

        /// <summary>
        /// Cards offered for the current level up, empty if none is waiting. While a choice is waiting the simulation
        /// is paused: <see cref="Tick"/> does nothing until <see cref="ChooseUpgrade"/> is called.
        /// </summary>
        public IReadOnlyList<UpgradeEntry> UpgradeOffer => _offer;

        public bool IsChoosingUpgrade => _offer.Count > 0;

        /// <summary>True if the last finished endless run set a new best time.</summary>
        public bool LastRunIsRecord { get; private set; }

        /// <summary>New cards are waiting in <see cref="UpgradeOffer"/>. The Unity side shows the card screen.</summary>
        public event Action UpgradeOffered;

        /// <summary>A card was taken. Raised after its effect is applied and before any next offer.</summary>
        public event Action<UpgradeEntry> UpgradeChosen;

        /// <summary>Difficulty of the current or last run. Replay uses it again.</summary>
        public DifficultyConfig CurrentDifficulty { get; private set; }

        /// <summary>Result of the last finished run, for the result screen. Null until a run ends.</summary>
        public RunResult? LastResult { get; private set; }

        /// <summary>Options of the current or last run. Replay uses them again.</summary>
        public RunOptions CurrentOptions { get; private set; }

        /// <summary>Starts a run. Also used for replay: everything from the previous run is cleared first.</summary>
        /// <param name="options">Overrides for special runs such as the benchmark. Default: a normal game.</param>
        public void StartRun(DifficultyConfig difficulty, RunOptions options = default)
        {
            CurrentDifficulty = difficulty ?? throw new ArgumentNullException(nameof(difficulty));
            IsEndless = false;
            PrepareRun(difficulty, options);
            Session.Start(options.Duration ?? _world.RunDuration);
        }

        /// <summary>
        /// Starts an endless run: no timer, XP and health drops, level up cards, enemies that grow stronger over time.
        /// Also used for replay. <see cref="RunOptions.Duration"/> is ignored.
        /// </summary>
        public void StartEndless(RunOptions options = default)
        {
            CurrentDifficulty = null;
            IsEndless = true;
            PrepareRun(Endless.Spawn, options);
            Session.StartEndless();
        }

        /// <summary>Starts a new run with the same mode, difficulty and options.</summary>
        public void Replay()
        {
            if (!_hasStarted)
            {
                throw new InvalidOperationException("No run has been started yet.");
            }

            if (IsEndless)
            {
                StartEndless(CurrentOptions);
            }
            else
            {
                StartRun(CurrentDifficulty, CurrentOptions);
            }
        }

        /// <summary>Leaves the run and clears the arena, e.g. to pick another difficulty.</summary>
        public void ReturnToMenu()
        {
            ClearArena();
            ClearUpgradeOffer();
            Session.ReturnToIdle();
        }

        /// <summary>Takes one of the offered cards and resumes the run (or shows the next offer if more level ups are waiting).</summary>
        /// <param name="index">Index into <see cref="UpgradeOffer"/>.</param>
        public void ChooseUpgrade(int index)
        {
            if (index < 0 || index >= _offer.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "No such card is offered.");
            }

            UpgradeEntry chosen = _offer[index];
            Upgrades.Apply(chosen);
            _pendingLevelUps--;
            _offer.Clear();
            UpgradeChosen?.Invoke(chosen);

            if (_pendingLevelUps > 0)
            {
                OfferUpgrade();
            }
        }

        /// <summary>
        /// Advances the simulation by one frame. Does nothing outside a run, so after a win or loss
        /// the arena stays frozen behind the result screen.
        /// </summary>
        public void Tick(float deltaTime, Vector2 moveInput)
        {
            // A waiting level up card choice pauses the whole simulation.
            if (!Session.IsPlaying || deltaTime <= 0f || IsChoosingUpgrade)
            {
                return;
            }

            // 1. The player moves first, so everything else reacts to the new position.
            Player.Move(deltaTime, moveInput);

            // 2. New enemies appear around the player. In endless mode enemies also grow stronger over time.
            if (IsEndless)
            {
                ScaleEnemies();
            }

            float spawnProgress = IsEndless ? Endless.SpawnProgress(Session.Elapsed) : Session.Progress;
            _spawner.Tick(deltaTime, spawnProgress, Player.Position);

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

            // 6. Endless only: dropped XP and health fly to the player and are collected. A level up here
            //    offers cards and pauses the simulation from the next frame on.
            Pickups.Tick(deltaTime, Player.Position);

            // 7. The timer runs last, so kills made in the final frame still count before a win.
            Session.Tick(deltaTime);
        }

        private void PrepareRun(DifficultyConfig spawn, RunOptions options)
        {
            _hasStarted = true;
            CurrentOptions = options;

            ClearArena();
            ClearUpgradeOffer();
            Player.Reset(Vector3.zero);
            Player.Health.IsInvulnerable = options.Invulnerable;
            Weapon.Reset();
            Enemies.ResetModifiers();
            Pickups.ResetModifiers();
            Experience.Reset();
            Upgrades.Reset();
            _spawner.Begin(spawn, options.Seed.HasValue ? new System.Random(options.Seed.Value) : null);
            LastResult = null;
            LastRunIsRecord = false;
        }

        private void ScaleEnemies()
        {
            float elapsed = Session.Elapsed;
            EnemyScalingConfig scaling = Endless.EnemyScaling;
            Enemies.HealthMultiplier = scaling.HealthMultiplier(elapsed);
            Enemies.SpeedMultiplier = scaling.SpeedMultiplier(elapsed);
            Enemies.DamageMultiplier = scaling.DamageMultiplier(elapsed);
        }

        private void OnEnemyDied(Enemy enemy)
        {
            if (!IsEndless || !Session.IsPlaying)
            {
                return;
            }

            PickupConfig drops = Endless.Pickups;
            Pickups.Spawn(PickupKind.Experience, enemy.Position, drops.ExperiencePerKill);

            if (drops.HealthDropChance > 0f && _random.NextDouble() < drops.HealthDropChance)
            {
                // Slightly beside the gem, so both stay visible.
                Pickups.Spawn(PickupKind.Health, enemy.Position + new Vector3(0.5f, 0f, 0f), drops.HealthPerPack);
            }
        }

        private void OnPickupCollected(Pickup pickup)
        {
            switch (pickup.Kind)
            {
                case PickupKind.Experience:
                    Experience.Add(pickup.Value);
                    break;
                case PickupKind.Health:
                    Player.Health.Heal(pickup.Value);
                    break;
            }
        }

        private void OnLeveledUp(int level)
        {
            _pendingLevelUps++;
            if (!IsChoosingUpgrade)
            {
                OfferUpgrade();
            }
        }

        private void OfferUpgrade()
        {
            Upgrades.RollOffer(_random, Endless.CardsPerLevel, _offer);
            if (_offer.Count == 0)
            {
                // Nothing left to offer (every card maxed and no filler card configured): keep playing.
                _pendingLevelUps = 0;
                return;
            }

            UpgradeOffered?.Invoke();
        }

        private void ClearUpgradeOffer()
        {
            _offer.Clear();
            _pendingLevelUps = 0;
        }

        private void OnRunEnded(RunResult result)
        {
            LastResult = result;

            if (CurrentOptions.SkipProgress)
            {
                return;
            }

            Progress.AddKills(result.Kills);
            if (IsEndless)
            {
                LastRunIsRecord = Progress.RecordEndlessRun(result.SurvivedSeconds, Experience.Level);
            }
        }

        private void ClearArena()
        {
            Enemies.Clear();
            Projectiles.Clear();
            Pickups.Clear();
        }
    }
}
