using System.IO;
using ArenaSurvivor.Core.Difficulty;
using ArenaSurvivor.Core.Enemies;
using ArenaSurvivor.Core.Player;
using ArenaSurvivor.Core.Save;
using ArenaSurvivor.Core.Session;
using ArenaSurvivor.Core.Weapons;
using ArenaSurvivor.Core.World;
using ArenaSurvivor.Unity.Cameras;
using ArenaSurvivor.Unity.Input;
using ArenaSurvivor.Unity.UI;
using ArenaSurvivor.Unity.Views;
using UnityEngine;

namespace ArenaSurvivor.Unity
{
    /// <summary>
    /// Composition root: the only MonoBehaviour with an Update(). Builds the <see cref="GameWorld"/>
    /// from the data assets, connects the visual layer to its events, and every frame feeds it input,
    /// ticks it and syncs the views and the camera.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private const string SaveFileName = "save.json";

        [Header("Data")]
        [SerializeField] private WorldConfig world = new WorldConfig();
        [SerializeField] private PlayerDefinition player;
        [SerializeField] private EnemyDefinition enemy;
        [SerializeField] private WeaponDefinition weapon;

        [Tooltip("Difficulties offered in the menu, in button order (Easy, Normal, Hard).")]
        [SerializeField] private DifficultySettings[] difficulties;

        [Header("Views")]
        [SerializeField] private PlayerView playerView;
        [SerializeField] private EnemyView enemyPrefab;
        [SerializeField] private Transform projectilePrefab;

        [Tooltip("Height above the ground at which bullets are drawn (roughly the rifle muzzle).")]
        [SerializeField] private float projectileHeight = 1.2f;

        [Tooltip("Seconds a killed enemy stays on screen for its death animation.")]
        [SerializeField, Min(0f)] private float corpseSeconds = 2.2f;

        [Header("Camera and input")]
        [SerializeField] private Camera gameCamera;
        [SerializeField] private FollowCamera.Settings cameraSettings = new FollowCamera.Settings();
        [SerializeField] private VirtualJoystick joystick;

        [Header("UI")]
        [SerializeField] private MenuScreen menuScreen;
        [SerializeField] private HudScreen hudScreen;
        [SerializeField] private ResultScreen resultScreen;
        [SerializeField] private DamageFlash damageFlash;

        private GameWorld _world;
        private GameFlow _flow;
        private MoveInput _input;
        private FollowCamera _camera;
        private ViewRegistry<Enemy, EnemyView> _enemyViews;
        private ViewRegistry<Projectile, Transform> _projectileViews;
        private EnemyDeathViews _enemyDeaths;

        // Cached once: passing a method group directly would allocate a new delegate every frame.
        private System.Action<Enemy, EnemyView> _syncEnemy;
        private System.Action<Projectile, Transform> _syncProjectile;
        private System.Action<Enemy, EnemyView> _freezeEnemy;

        public GameWorld World => _world;

        private void Awake()
        {
            string savePath = Path.Combine(Application.persistentDataPath, SaveFileName);
            var progress = new ProgressService(new JsonFileSaveService(savePath));

            _world = new GameWorld(world, player.Config, enemy.Config, weapon.Config, progress, new System.Random());
            _input = new MoveInput(joystick);
            _camera = new FollowCamera(gameCamera.transform, cameraSettings);

            var viewRoot = new GameObject("Views").transform;
            _enemyViews = new ViewRegistry<Enemy, EnemyView>(enemyPrefab, viewRoot, world.EnemyPrewarm);
            _projectileViews = new ViewRegistry<Projectile, Transform>(projectilePrefab, viewRoot, world.ProjectilePrewarm);

            _world.Enemies.Spawned += _enemyViews.Show;
            _world.Enemies.Despawned += _enemyViews.Hide;
            _world.Projectiles.Spawned += _projectileViews.Show;
            _world.Projectiles.Despawned += _projectileViews.Hide;

            _syncEnemy = (model, view) => view.Sync(model);
            _syncProjectile = SyncProjectile;

            // Set up a freshly shown view before it is drawn, so it never flashes at its old position or pose.
            float attackInterval = enemy.Config.AttackInterval;
            _enemyViews.Shown += (model, view) => view.Begin(model, attackInterval);
            _projectileViews.Shown += _syncProjectile;

            // Death animations: enemies linger as corpses for a moment, the player falls over.
            _enemyDeaths = new EnemyDeathViews(_enemyViews, corpseSeconds);
            _world.Enemies.Died += _enemyDeaths.OnEnemyDied;
            _world.Player.Health.Died += playerView.PlayDeath;

            // The simulation freezes when a run ends; freeze the enemy animations with it.
            _freezeEnemy = (model, view) => view.SetFrozen(true);
            _world.Session.Ended += FreezeEnemies;

            _flow = new GameFlow(_world, difficulties, menuScreen, hudScreen, resultScreen, damageFlash, OnRunStarted);
        }

        private void Start()
        {
            _flow.ShowMenu();
            OnRunStarted();
        }

        private void OnDestroy()
        {
            if (_world == null)
            {
                return;
            }

            _flow.Dispose();
            _world.Enemies.Spawned -= _enemyViews.Show;
            _world.Enemies.Despawned -= _enemyViews.Hide;
            _world.Projectiles.Spawned -= _projectileViews.Show;
            _world.Projectiles.Despawned -= _projectileViews.Hide;
            _world.Enemies.Died -= _enemyDeaths.OnEnemyDied;
            _world.Player.Health.Died -= playerView.PlayDeath;
            _world.Session.Ended -= FreezeEnemies;
        }

        private void FreezeEnemies(RunResult result)
        {
            _enemyViews.Sync(_freezeEnemy);
            _enemyDeaths.Freeze();
        }

        /// <summary>
        /// Called when a run (re)starts: removes leftover corpses and places the player model and camera
        /// without smoothing, so they jump to the start instead of sliding there.
        /// </summary>
        private void OnRunStarted()
        {
            _enemyDeaths.Clear();
            playerView.Snap(_world.Player);
            _camera.Snap(_world.Player.Position);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            _world.Tick(deltaTime, _input.Read());

            playerView.Sync(_world.Player, deltaTime);
            _enemyViews.Sync(_syncEnemy);

            // Corpses fade out only during a run; they stay frozen behind the result screen
            // and are removed when the player goes back to the menu.
            if (_world.Session.IsPlaying)
            {
                _enemyDeaths.Tick(deltaTime);
            }
            else if (_world.Session.State == GameState.Idle)
            {
                _enemyDeaths.Clear();
            }

            _projectileViews.Sync(_syncProjectile);
            _camera.Follow(_world.Player.Position, deltaTime);
            _flow.Tick(deltaTime);
        }

        private void SyncProjectile(Projectile projectile, Transform view)
        {
            Vector3 position = projectile.Position;
            position.y = projectileHeight;
            view.SetPositionAndRotation(position, Quaternion.LookRotation(projectile.Direction, Vector3.up));
        }
    }
}
