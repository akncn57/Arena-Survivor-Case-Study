using System;
using System.Collections.Generic;
using ArenaSurvivor.Core.Difficulty;
using ArenaSurvivor.Core.Session;
using ArenaSurvivor.Core.Upgrades;
using ArenaSurvivor.Core.World;
using ArenaSurvivor.Unity.Benchmark;
using UnityEngine;

namespace ArenaSurvivor.Unity.UI
{
    /// <summary>
    /// Which screen is visible, and what the buttons do.
    ///
    ///   Menu --difficulty--> Playing (HUD) --run ends--> Result --replay--> Playing
    ///                                                           --menu----> Menu
    ///   Menu --endless-----> Playing (HUD + XP bar) --level up--> Level up cards (paused) --card--> Playing
    ///                                               --death-----> Result (endless) --replay/menu
    ///   Menu --benchmark---> Benchmark run (HUD, no input) --ends--> Benchmark result --menu--> Menu
    ///
    /// While the level up cards are shown the game is paused with <c>Time.timeScale = 0</c>: the simulation
    /// already stops by itself (<see cref="GameWorld.IsChoosingUpgrade"/>), and the time scale also freezes what
    /// the Unity side animates (enemy animators, particles, camera smoothing, bobbing pickups).
    /// </summary>
    public sealed class GameFlow : IDisposable
    {
        private readonly GameWorld _world;
        private readonly IReadOnlyList<DifficultySettings> _difficulties;
        private readonly MenuScreen _menu;
        private readonly HudScreen _hud;
        private readonly ResultScreen _result;
        private readonly DamageFlash _damageFlash;
        private readonly BenchmarkScreen _benchmarkScreen;
        private readonly LevelUpScreen _levelUp;
        private readonly Func<UpgradeEntry, int> _upgradeLevelOf;
        private readonly BenchmarkSettings _benchmark;
        private readonly BenchmarkRecorder _recorder = new BenchmarkRecorder();
        private readonly int _gameFrameRate;
        private readonly Action _runStarted;

        /// <param name="gameFrameRate">Frame rate cap for normal play (Android defaults to 30 otherwise).</param>
        /// <param name="runStarted">Called after a run (re)starts, so the bootstrap can snap views and camera.</param>
        public GameFlow(GameWorld world, IReadOnlyList<DifficultySettings> difficulties,
            MenuScreen menu, HudScreen hud, ResultScreen result, DamageFlash damageFlash,
            BenchmarkScreen benchmarkScreen, BenchmarkSettings benchmark, int gameFrameRate, Action runStarted,
            LevelUpScreen levelUp = null, bool endlessAvailable = false)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _difficulties = difficulties ?? throw new ArgumentNullException(nameof(difficulties));
            _menu = menu;
            _hud = hud;
            _result = result;
            _damageFlash = damageFlash;
            _benchmarkScreen = benchmarkScreen;
            _benchmark = benchmark ?? throw new ArgumentNullException(nameof(benchmark));
            _gameFrameRate = gameFrameRate;
            _runStarted = runStarted;
            _levelUp = levelUp;
            _upgradeLevelOf = entry => _world.Upgrades.GetLevel(entry);

            // The endless mode needs the level up screen; without it the menu hides the endless button.
            _menu.Bind(difficulties, endlessAvailable && _levelUp != null);
            _menu.DifficultySelected += OnDifficultySelected;
            _menu.BenchmarkSelected += OnBenchmarkSelected;
            _menu.EndlessSelected += OnEndlessSelected;
            _world.UpgradeOffered += OnUpgradeOffered;
            if (_levelUp != null)
            {
                _levelUp.CardChosen += OnCardChosen;
            }

            _result.ReplayClicked += OnReplayClicked;
            _result.MenuClicked += ShowMenu;
            _benchmarkScreen.MenuClicked += ShowMenu;
            // GameWorld subscribed to Ended in its constructor, before this, so the lifetime kills
            // are already saved when OnRunEnded reads them.
            _world.Session.Ended += OnRunEnded;
            _world.Player.Health.Damaged += OnPlayerDamaged;
        }

        /// <summary>True while the benchmark plays; the bootstrap then ignores player input.</summary>
        public bool IsBenchmarkRunning => _recorder.IsRecording;

        public void ShowMenu()
        {
            Application.targetFrameRate = _gameFrameRate;
            _world.ReturnToMenu();
            SetPaused(false);
            _hud.Hide();
            _result.Hide();
            _benchmarkScreen.Hide();
            _levelUp?.Hide();
            _damageFlash.Clear();
            _menu.Show(_world.Progress.TotalKills, _world.Progress.BestEndlessSeconds, _world.Progress.BestEndlessLevel);
        }

        /// <summary>Per-frame UI update, called by the bootstrap after the world has ticked.</summary>
        public void Tick(float deltaTime)
        {
            if (_world.Session.IsPlaying)
            {
                RefreshHud();
                _recorder.Tick(Time.unscaledDeltaTime, _world.Enemies.AliveCount);
            }

            _damageFlash.Tick(deltaTime);
            _levelUp?.Tick(Time.unscaledDeltaTime);
        }

        public void Dispose()
        {
            _menu.DifficultySelected -= OnDifficultySelected;
            _menu.BenchmarkSelected -= OnBenchmarkSelected;
            _menu.EndlessSelected -= OnEndlessSelected;
            _world.UpgradeOffered -= OnUpgradeOffered;
            if (_levelUp != null)
            {
                _levelUp.CardChosen -= OnCardChosen;
            }

            _result.ReplayClicked -= OnReplayClicked;
            _result.MenuClicked -= ShowMenu;
            _benchmarkScreen.MenuClicked -= ShowMenu;
            _world.Session.Ended -= OnRunEnded;
            _world.Player.Health.Damaged -= OnPlayerDamaged;
        }

        private void OnDifficultySelected(int index)
        {
            Application.targetFrameRate = _gameFrameRate;
            _world.StartRun(_difficulties[index].Config);
            EnterPlaying();
        }

        private void OnEndlessSelected()
        {
            Application.targetFrameRate = _gameFrameRate;
            _world.StartEndless();
            EnterPlaying();
        }

        private void OnUpgradeOffered()
        {
            SetPaused(true);
            RefreshHud(); // The XP bar shows the level up that caused the pause.
            _levelUp.Show(_world.Experience.Level, _world.UpgradeOffer, _upgradeLevelOf);
        }

        private void OnCardChosen(int index)
        {
            _levelUp.Hide();
            _world.ChooseUpgrade(index); // May raise UpgradeOffered again for the next waiting level up.

            if (!_world.IsChoosingUpgrade)
            {
                SetPaused(false);
            }
        }

        private void OnBenchmarkSelected()
        {
            // Identical conditions every time: fixed seed, a player that cannot die or move,
            // fixed length, and the result does not touch the save file.
            var options = new RunOptions
            {
                Seed = _benchmark.seed,
                Invulnerable = true,
                Duration = _benchmark.warmupSeconds + _benchmark.measureSeconds,
                SkipProgress = true,
            };

            Application.targetFrameRate = _benchmark.targetFrameRate;
            _world.StartRun(_benchmark.difficulty.Config, options);
            _recorder.Begin(_benchmark.warmupSeconds);
            EnterPlaying();
        }

        private void OnReplayClicked()
        {
            _world.Replay();
            EnterPlaying();
        }

        private void EnterPlaying()
        {
            SetPaused(false);
            _menu.Hide();
            _result.Hide();
            _levelUp?.Hide();
            _damageFlash.Clear();
            _hud.Show(_world.IsEndless);
            RefreshHud();
            _runStarted?.Invoke();
        }

        private void RefreshHud()
        {
            _hud.Refresh(_world.Session, _world.Player.Health, _world.IsEndless ? _world.Experience : null);
        }

        private static void SetPaused(bool paused)
        {
            Time.timeScale = paused ? 0f : 1f;
        }

        private void OnRunEnded(RunResult result)
        {
            // Hiding the HUD also disables the joystick, so a held finger does not keep steering.
            _hud.Hide();
            SetPaused(false);

            if (_recorder.IsRecording)
            {
                BenchmarkResult benchmark = _recorder.Finish(result.Kills);
                BenchmarkRecorder.Save(benchmark);
                Application.targetFrameRate = _gameFrameRate;
                _benchmarkScreen.Show(benchmark.ToDisplayText());
                return;
            }

            if (_world.IsEndless)
            {
                // GameWorld subscribed to Ended first, so the best run is already recorded here.
                _result.ShowEndless(result, _world.Experience.Level, _world.Progress.TotalKills, _world.LastRunIsRecord,
                    _world.Progress.BestEndlessSeconds, _world.Progress.BestEndlessLevel);
                return;
            }

            _result.Show(result, _world.Progress.TotalKills);
        }

        private void OnPlayerDamaged(int amount)
        {
            _damageFlash.Flash();
        }
    }
}
