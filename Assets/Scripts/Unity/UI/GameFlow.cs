using System;
using System.Collections.Generic;
using ArenaSurvivor.Core.Difficulty;
using ArenaSurvivor.Core.Session;
using ArenaSurvivor.Core.World;

namespace ArenaSurvivor.Unity.UI
{
    /// <summary>
    /// Which screen is visible, and what the buttons do.
    ///
    ///   Menu --difficulty--> Playing (HUD) --run ends--> Result --replay--> Playing
    ///                                                           --menu----> Menu
    /// </summary>
    public sealed class GameFlow : IDisposable
    {
        private readonly GameWorld _world;
        private readonly IReadOnlyList<DifficultySettings> _difficulties;
        private readonly MenuScreen _menu;
        private readonly HudScreen _hud;
        private readonly ResultScreen _result;
        private readonly DamageFlash _damageFlash;
        private readonly Action _runStarted;

        /// <param name="runStarted">Called after a run (re)starts, so the bootstrap can snap views and camera.</param>
        public GameFlow(GameWorld world, IReadOnlyList<DifficultySettings> difficulties,
            MenuScreen menu, HudScreen hud, ResultScreen result, DamageFlash damageFlash, Action runStarted)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _difficulties = difficulties ?? throw new ArgumentNullException(nameof(difficulties));
            _menu = menu;
            _hud = hud;
            _result = result;
            _damageFlash = damageFlash;
            _runStarted = runStarted;

            _menu.Bind(difficulties);
            _menu.DifficultySelected += OnDifficultySelected;
            _result.ReplayClicked += OnReplayClicked;
            _result.MenuClicked += ShowMenu;
            // GameWorld subscribed to Ended in its constructor, before this, so the lifetime kills
            // are already saved when OnRunEnded reads them.
            _world.Session.Ended += OnRunEnded;
            _world.Player.Health.Damaged += OnPlayerDamaged;
        }

        public void ShowMenu()
        {
            _world.ReturnToMenu();
            _hud.Hide();
            _result.Hide();
            _damageFlash.Clear();
            _menu.Show(_world.Progress.TotalKills);
        }

        /// <summary>Per-frame UI update, called by the bootstrap after the world has ticked.</summary>
        public void Tick(float deltaTime)
        {
            if (_world.Session.IsPlaying)
            {
                _hud.Refresh(_world.Session, _world.Player.Health);
            }

            _damageFlash.Tick(deltaTime);
        }

        public void Dispose()
        {
            _menu.DifficultySelected -= OnDifficultySelected;
            _result.ReplayClicked -= OnReplayClicked;
            _result.MenuClicked -= ShowMenu;
            _world.Session.Ended -= OnRunEnded;
            _world.Player.Health.Damaged -= OnPlayerDamaged;
        }

        private void OnDifficultySelected(int index)
        {
            _world.StartRun(_difficulties[index].Config);
            EnterPlaying();
        }

        private void OnReplayClicked()
        {
            _world.Replay();
            EnterPlaying();
        }

        private void EnterPlaying()
        {
            _menu.Hide();
            _result.Hide();
            _damageFlash.Clear();
            _hud.Show();
            _hud.Refresh(_world.Session, _world.Player.Health);
            _runStarted?.Invoke();
        }

        private void OnRunEnded(RunResult result)
        {
            // Hiding the HUD also disables the joystick, so a held finger does not keep steering.
            _hud.Hide();
            _result.Show(result, _world.Progress.TotalKills);
        }

        private void OnPlayerDamaged(int amount)
        {
            _damageFlash.Flash();
        }
    }
}
