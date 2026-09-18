using System.IO;
using ArenaSurvivor.Core.Difficulty;
using ArenaSurvivor.Core.Enemies;
using ArenaSurvivor.Core.Player;
using ArenaSurvivor.Core.Save;
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

        // Cached once: passing a method group directly would allocate a new delegate every frame.
        private System.Action<Enemy, EnemyView> _syncEnemy;
        private System.Action<Projectile, Transform> _syncProjectile;

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

            // Place a freshly shown view before it is drawn, so it never flashes at its old position.
            _enemyViews.Shown += _syncEnemy;
            _projectileViews.Shown += _syncProjectile;

            _flow = new GameFlow(_world, difficulties, menuScreen, hudScreen, resultScreen, damageFlash, SnapToPlayer);
        }

        private void Start()
        {
            _flow.ShowMenu();
            SnapToPlayer();
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
        }

        /// <summary>Places the player model and camera without smoothing, e.g. when a run starts.</summary>
        private void SnapToPlayer()
        {
            playerView.Snap(_world.Player);
            _camera.Snap(_world.Player.Position);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            _world.Tick(deltaTime, _input.Read());

            playerView.Sync(_world.Player, deltaTime);
            _enemyViews.Sync(_syncEnemy);
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
