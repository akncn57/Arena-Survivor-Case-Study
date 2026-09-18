# Arena Survivor: Technical Overview

How the project is structured and what each system does. Updated as each system is added.

## Architecture

The game logic is plain C# classes. MonoBehaviours are kept to a thin layer that
connects that logic to Unity (reading input, moving transforms, playing animations, showing UI).

Why:
- **Testable.** Plain classes can be created in an EditMode unit test with `new`, with no scene or play mode.
- **Fast.** One system updates all enemies in a single loop, instead of hundreds of `Update()` calls
  on individual MonoBehaviours.
- **Explicit.** Dependencies are passed through constructors, so it is visible what each class needs.

No dependency injection framework is used; dependencies are passed through constructors.
Wiring happens in two places:
- `GameWorld` (Core) creates all gameplay systems and connects their events. Being plain C#, the whole
  game can be simulated in a test.
- A bootstrap MonoBehaviour in the scene (Unity side) creates the `GameWorld` from the data assets,
  feeds it input every frame and connects the visual layer to its events.

A container such as VContainer was considered; at this project size manual wiring is shorter, has no
package dependency and keeps every connection visible in one file. Classes already use constructor
injection, so switching later would only change the wiring code.

### Assemblies

| Assembly | Folder | Contents | References |
|----------|--------|----------|------------|
| `ArenaSurvivor.Core` | `Assets/Scripts/Core` | Game logic, plain C#, no MonoBehaviours | UnityEngine only (math types, JsonUtility) |
| `ArenaSurvivor.Unity` | `Assets/Scripts/Unity` | Thin Unity layer: bootstrap, views, input, camera | Core, Input System, uGUI |
| `ArenaSurvivor.Tests.EditMode` | `Assets/Tests/EditMode` | NUnit EditMode tests for Core | Core, Unity Test Framework |

Assembly definitions keep compile times short and enforce the direction of dependencies:
Core cannot reference the Unity-side layer or tests.

### Running the tests

Unity: **Window > General > Test Runner > EditMode > Run All**.
Test folders mirror the source folders (`Core/Save` -> `Tests/EditMode/Save`).

## Systems

### Save (`Core/Save`)

Keeps the lifetime kill count across app restarts.

| Type | Role |
|------|------|
| `SaveData` | The data that is stored. Serializable class with a `version` field and `totalKills`. |
| `ISaveService` | Interface for loading and saving `SaveData`. Game code only knows this interface. |
| `JsonFileSaveService` | Stores `SaveData` as JSON in a file (in the game: `Application.persistentDataPath/save.json`). |
| `ProgressService` | What the game uses: `TotalKills` and `AddKills(int)`. Loads once, saves on every change. |

Flow:

```
Game start:  ProgressService(new JsonFileSaveService(path))  -> Load() -> data kept in memory
Run ends:    progress.AddKills(runKills)                     -> Save() -> file written
```

Behaviour details:
- **Atomic write.** `Save` writes to `save.json.tmp` first, then swaps it into place with `File.Replace`
  (or `File.Move` the first time). If the app is killed mid-write, the old save is still intact.
- **Never blocks the game.** A missing, empty or corrupt file loads as defaults (0 kills) and logs a warning.
- **Sanitized.** A negative kill count from a hand-edited file is clamped to 0.
- **Versioned.** `version` lets a future release migrate old files. New fields just need safe defaults,
  since `JsonUtility` leaves missing keys at their field initializer values.

Tests: `JsonFileSaveServiceTests` (real files in a per-test temp folder) and
`ProgressServiceTests` (in-memory fake `ISaveService`, no disk access).

### Session (`Core/Session`)

One run of the game: the 3-minute survival timer, the kill count and the outcome.

| Type | Role |
|------|------|
| `GameState` | `Idle` (difficulty selection), `Playing`, `Won`, `Lost`. |
| `GameSession` | Owns the timer and kill count, decides win/lose, raises `Started` and `Ended`. |
| `RunResult` | Passed with `Ended`: outcome, kills, survived seconds. Used by the result screen. |

State flow:

```
Idle --Start(180)--> Playing --timer reaches 180s--> Won
                        |
                        +--NotifyPlayerDied()------> Lost
Won / Lost --Start(180)--> Playing   (replay, all values reset)
Won / Lost --ReturnToIdle()--> Idle  (back to difficulty selection)
```

Behaviour details:
- **Time comes from outside.** `Tick(deltaTime)` is called by the Unity layer each frame. The session never reads
  `Time.deltaTime` itself, so a test can simulate a full 3-minute run in microseconds.
- **Ends exactly once.** After `Won` or `Lost`, further ticks, kills and deaths are ignored. A projectile that hits
  after the timer ended does not count, and the player cannot die after winning.
- **`Progress` (0..1)** tells other systems how far the run is. Spawning uses it to ramp up difficulty.
- **Persisting kills is not the session's job.** The composition root subscribes `Ended` to
  `ProgressService.AddKills`, which keeps the session free of save logic.

Tests: `GameSessionTests` (state transitions, timer, kills, replay reset, a simulated 60 FPS run).

### Difficulty (`Core/Difficulty`)

Three difficulty levels in the same scene, differing only in enemy count and spawn rate.

| Type | Role |
|------|------|
| `DifficultyConfig` | Plain serializable class with the tuning values and the ramp-up math. |
| `DifficultySettings` | ScriptableObject asset wrapping one `DifficultyConfig` plus a display name. No logic. |

Each value ramps linearly from its start value to its end value over the run, using `GameSession.Progress`:

| Asset (`Assets/Data/Difficulty`) | Spawn interval (s) | Wave size | Max alive |
|------|------|------|------|
| `Difficulty_Easy` | 3.0 -> 1.5 | 2 -> 6 | 40 |
| `Difficulty_Normal` | 2.5 -> 1.0 | 3 -> 10 | 80 |
| `Difficulty_Hard` | 2.0 -> 0.6 | 4 -> 16 | 150 |

`Max alive` is a hard cap: waves are trimmed so the enemy count never exceeds it. It bounds the worst-case
CPU/GPU load, which also makes it the main knob for the performance tests. The values are a first pass
and will be tuned by playtesting.

Why two types instead of one ScriptableObject: a ScriptableObject can only be created through Unity
(`CreateInstance`) and its fields are set through the Inspector. Keeping the logic in a plain class lets
tests build a config with `new` and check the math directly.

Tests: `DifficultyConfigTests` (start/end/midway values, clamping, validation).

### Combat: Health (`Core/Combat`)

`Health` holds hit points for the player and for every enemy.
- `TakeDamage` ignores zero/negative amounts and clamps at 0.
- `Damaged(amount)` fires on every hit (used for hit feedback). `Died` fires **exactly once**; a dead target ignores further damage.
- `Reset()` refills it for a new life (pooled enemy reused, player on replay).

Tests: `HealthTests`.

### Enemies (`Core/Enemies`)

Spawning, chasing, attacking and dying for all enemies.

| Type | Role |
|------|------|
| `EnemyConfig` | Stats: health, speed, contact damage, attack interval, attack range. |
| `EnemyDefinition` | ScriptableObject wrapper for `EnemyConfig` (`Assets/Data/Enemies/Enemy_Zombie`). |
| `Enemy` | State of one enemy: position, facing, health, "in attack range" flag. Plain data, no behaviour. |
| `EnemySystem` | Owns all enemies. Spawns from a pool, updates all of them in one loop, applies damage, despawns. |
| `WaveSpawner` | Decides when and where a wave appears, based on difficulty and run progress. |

**One system, one loop.** There is no MonoBehaviour per enemy. `EnemySystem.Tick` walks one list and,
for each enemy: turns towards the player, moves by `speed * deltaTime` but stops at the attack range edge,
and attacks when in range and its cooldown is over. With 150 enemies this is one method call per frame
instead of 150 `Update()` calls, and the data is laid out in one place for the profiler to measure.

**Talking to the Unity side with events.** The system does not know about GameObjects:
- `Spawned(enemy)`: the view layer takes an enemy model from its own pool and follows this enemy.
- `Died(enemy)`: killed by the player. The composition root forwards it to `GameSession.RegisterKill`.
- `Despawned(enemy)`: the enemy left (killed or cleared). The view layer returns the model to its pool.

**Pooling.** `Enemy` objects come from `UnityEngine.Pool.ObjectPool<T>` (built into Unity, no package).
A killed enemy goes back to the pool and is reset on the next spawn, so after warm-up (`Prewarm`) no
allocations happen during play. Removing from the active list is O(1): the last enemy is moved into the
freed slot (`ActiveIndex` remembers each enemy's slot).

**Damage to the player is applied once per tick, after the loop.** The total from all attacking enemies is
summed and applied at the end. The player dying can trigger listeners that clear all enemies; doing that
in the middle of the loop would break the iteration. A test guards this case.

**Wave spawning.**
- The first wave appears on the first tick of a run, then one wave every `GetSpawnInterval(progress)` seconds.
- Wave size is `GetWaveSize(progress)`, trimmed so `AliveCount` never exceeds `MaxAliveEnemies`.
- Enemies appear on a ring of `spawnRadius` around the player (chosen to be outside the camera view),
  at a random angle, clamped inside the arena. Near the arena edge clamping can bring a spawn closer
  than the ring; accepted as a minor trade-off for simplicity.
- Randomness comes from an injected `System.Random`, so tests use a fixed seed and are repeatable.

Known limitation (planned for the optimization phase): enemies do not push each other apart, so they
can overlap when crowding the player. Separation needs neighbour lookups, which is exactly the kind of
cost that should be measured first and then solved with a spatial grid.

Tests: `EnemySystemTests` (movement, range, attack cooldown, damage stacking, death events, pool reuse,
clearing during the player's death) and `WaveSpawnerTests` (timing, max alive cap, progress ramp,
spawn ring, arena clamping, restart).

### Player (`Core/Player`)

| Type | Role |
|------|------|
| `PlayerConfig` | Stats: max health, move speed. |
| `PlayerDefinition` | ScriptableObject wrapper (`Assets/Data/Player/Player_Default`). |
| `PlayerCharacter` | Position, facing, velocity and `Health` of the player. |

(The class is `PlayerCharacter`, not `Player`, because a type with the same name as its namespace
forces awkward fully-qualified names everywhere else.)

- **Input.** `Move(deltaTime, Vector2 input)` takes the joystick vector. `x` maps to world X and `y` to world Z.
  The camera never rotates around the vertical axis, so "joystick up" always means "up the screen".
- **Analog speed.** Half tilt moves at half speed. Input longer than 1 is clamped, so diagonals are not faster.
- **Arena bounds.** The position is clamped inside the arena.
- **Facing.** Moving turns the player towards the movement direction. `AimAt(point)` is called afterwards
  when there is a target, so the rifle points at the enemy being shot, even while running the other way.
- **Dead players do not move.** `Reset(position)` restores position and health for a new run.

Tests: `PlayerCharacterTests`.

### Weapons (`Core/Weapons`)

| Type | Role |
|------|------|
| `WeaponConfig` | Rifle stats: damage, fire interval, range, projectile speed. |
| `WeaponDefinition` | ScriptableObject wrapper (`Assets/Data/Weapons/Weapon_Rifle`). |
| `Targeting` | `FindNearest(enemies, origin, range)`: nearest enemy within range, or null. |
| `Weapon` | Auto-fire: picks the target every tick and fires when the cooldown is over. |
| `Projectile` | State of one bullet: position, direction, remaining distance, damage. |
| `ProjectileSystem` | Owns all bullets: pooled, one update loop, hit detection, events for the view. |

Frame flow:

```
Weapon.Tick      -> Targeting.FindNearest -> CurrentTarget (player aims at it)
                 -> cooldown over?        -> ProjectileSystem.Fire(...) + Fired event
ProjectileSystem.Tick -> move each bullet -> hit an enemy?  -> EnemySystem.ApplyDamage + Hit event, despawn
                                          -> flew too far?  -> despawn
```

Behaviour details:
- **Auto-attack.** The weapon always shoots the nearest enemy in range (8 units by default). The first shot is
  immediate when a target appears, then one shot per `fireInterval`.
- **Bullets are simulated, not instant hits.** They fly straight at `projectileSpeed` and can miss an enemy
  that moves out of the way. They fly `range x 1.5` before expiring, so they still reach a target that
  moved slightly out of range.
- **No tunnelling.** Hits are tested against the segment travelled this frame, not just the end point.
  On a slow frame a bullet may move further than an enemy is wide; a point check would jump over it.
  A test fires a bullet 100 units in one tick to guard this.
- **One hit per bullet.** A bullet damages the first enemy on its path and disappears (no piercing).
- **No Unity physics.** No colliders or rigidbodies: the maths are a few multiplications per bullet-enemy
  pair, the results are deterministic in tests, and Unity's physics engine does not have to run at all.
  The cost is O(bullets x enemies) per frame; bullets are few (one every 0.35 s, short-lived), so this
  stays small, and the profiler will confirm it.
- **Pooling.** Same pattern as enemies: `ObjectPool<Projectile>`, O(1) swap-remove, `Prewarm`, `Clear`.
  The update loop runs backwards, so a removal only moves an already-updated bullet into the freed slot.

Tests: `TargetingTests`, `WeaponTests`, `ProjectileSystemTests` (movement, expiry, hits, tunnelling,
misses, kills, several bullets, pool reuse).

### World (`Core/World`)

`GameWorld` is the whole simulation in one object. `WorldConfig` holds arena-wide settings
(run duration, arena size, spawn radius, bullet hit radius, pool prewarm counts).

**What it owns.** It creates `GameSession`, `PlayerCharacter`, `EnemySystem`, `ProjectileSystem`, `Weapon`
and `WaveSpawner`, and exposes them (except the spawner) so the Unity side can draw them and subscribe to events.

**Event wiring** (the only place systems are connected to each other):

| Event | Handler | Effect |
|------|------|------|
| `Enemies.Died` | `Session.RegisterKill` | Kill counter |
| `Player.Health.Died` | `Session.NotifyPlayerDied` | Run lost |
| `Session.Ended` | `Progress.AddKills` + store `LastResult` | Lifetime kills saved, result screen data |

**Frame order** in `Tick(deltaTime, joystickInput)`:

```
1. Player.Move            the player moves first; everything else reacts to the new position
2. WaveSpawner.Tick       new enemies around the player
3. Enemies.Tick           chase and attack; may kill the player -> run ends, stop here
4. Weapon.Tick + AimAt    pick target, fire, face the target
5. Projectiles.Tick       bullets fly and hit; kills counted through Enemies.Died
6. Session.Tick           timer last, so a kill in the final frame still counts before the win
```

**Run flow.**
- `StartRun(difficulty)`: clears enemies and bullets, resets player and weapon, starts the spawner and the timer.
- `Replay()`: `StartRun` with the same difficulty. Lifetime kills are kept.
- `ReturnToMenu()`: clears the arena and goes back to `Idle` for difficulty selection.
- After a win or loss `Tick` does nothing, so the arena stays frozen behind the result screen.

Tests: `GameWorldTests` are integration tests of all Core systems together: kills are counted, win and loss
both save kills, the arena freezes after the run, replay resets everything but lifetime kills, kills of two
runs add up, and every `Spawned` event is matched by a `Despawned` event (otherwise the Unity side would leak
models). A smoke test plays a full 3-minute run at 60 FPS with the default tuning.

Shared test doubles live in `Tests/EditMode/TestDoubles` (`InMemorySaveService`).

### Input maths (`Core/Input`)

`JoystickModel` is the maths of the floating on-screen joystick, kept in Core so it can be tested:
- `Press(point)` puts the joystick base where the finger touched down.
- `Drag(point)` moves the handle, clamped to `radius`. `Value` is the direction with strength 0..1.
- A **dead zone** (10% of the radius by default) ignores a resting thumb. Outside it the strength is rescaled
  to start from 0, so there is no jump when leaving the dead zone.
- `Release()` zeroes everything.

Tests: `JoystickModelTests`.

## Unity layer (`Assets/Scripts/Unity`)

Only `GameBootstrap` has an `Update()`. Every other component is either plain C# or a MonoBehaviour
that is driven by the bootstrap, so the order of everything that happens in a frame is visible in one method.

| Type | Kind | Role |
|------|------|------|
| `GameBootstrap` | MonoBehaviour | Composition root. Builds `GameWorld` from the data assets, connects views to its events, runs the frame. |
| `VirtualJoystick` | MonoBehaviour (uGUI) | Turns pointer events into `JoystickModel` calls and moves the two joystick images. |
| `MoveInput` | Plain C# | Joystick while touched, otherwise keyboard (WASD/arrows) or gamepad, for testing in the Editor. |
| `ViewRegistry<TModel, TView>` | Plain C# | Maps simulation objects to pooled GameObjects. |
| `PlayerView` | MonoBehaviour | Copies the player's position; turns smoothly towards its facing. |
| `EnemyView` | MonoBehaviour | Copies one enemy's position and facing. |
| `FollowCamera` | Plain C# | Tilted top-down camera with a fixed offset and light smoothing. |

**Frame** (`GameBootstrap.Update`):

```
input  = MoveInput.Read()
world.Tick(deltaTime, input)        simulation (see GameWorld)
playerView.Sync                     draw the player
enemyViews.Sync / projectileViews.Sync   draw every enemy and bullet
camera.Follow                       camera last, so it sees the final player position
```

**View pooling.** `ViewRegistry` is connected to a system's events: `Spawned -> Show` takes a GameObject
from a pool, activates it and remembers which model it draws; `Despawned -> Hide` deactivates it and puts it
back. Enemy and bullet GameObjects are all created at startup (prewarm), so no `Instantiate`/`Destroy`
happens during play. A freshly shown view is placed immediately, so it never flashes at its old position.

**No per-frame garbage.** The sync callbacks are cached delegates. Passing a method name directly
(`Sync(SyncProjectile)`) would create a new delegate object every frame.

### Scene (`Assets/Scenes/Arena.unity`)

Built through Unity MCP.

| Object | Contents |
|------|------|
| `Arena` | 40 x 40 ground plane and four low walls (placeholders, static-batched, no colliders). |
| `Player` | `Assets/Prefabs/Player.prefab`: `PlayerView`, the original `player.fbx`, `rifle.fbx` under `mixamorig:RightHand`. |
| `GameBootstrap` | References to the data assets, prefabs, camera and joystick. |
| `UI` | Screen-space canvas (reference 1920 x 1080, landscape) with a full-screen `JoystickArea`. |
| `EventSystem` | Uses `InputSystemUIInputModule` (the project uses the new Input System only). |

Prefabs: `Enemy.prefab` (`EnemyView` + original `enemy.fbx`), `Bullet.prefab` (small stretched sphere with an
unlit material, no collider, no shadows). The models are linked prefab instances of the original FBX files,
so optimized models can later be swapped in without touching the originals.

The game runs in landscape, locked to `LandscapeLeft` (top of the phone on the left), set in Player Settings.

**Camera framing.** The camera settings were chosen by rendering the camera at 16:9 through MCP with marker
enemies placed at the weapon range and at the spawn radius:
- Offset `(0, 20, -11.5)` (about 60 degrees down) and a 40 degree field of view. A narrow field of view from further
  away flattens perspective, so the far side of the screen shows less ground than a wide lens would.
- Visible ground around the player: about 15 units left/right, 8 behind, 12 ahead.
- The weapon range (8) fits on screen, so the player only shoots enemies the user can see.
- The spawn radius (18) is outside the view, so enemies walk in from off-screen instead of popping in.
  At the far corners of the screen the view is wider than 18, so a spawn there can occasionally be visible.
The rifle grip is a placeholder until animations are added; the characters are in T-pose until then.

### Presentation helpers (`Core/Presentation`)

`TimeFormat` turns seconds into HUD text, kept in Core so the rounding rules are tested:
- `CountdownSeconds` rounds **up** (0.2 s left still shows `0:01`; `0:00` only when time is really over).
- `ElapsedSeconds` rounds **down** (59.9 s survived is `0:59`).
- `MinutesSeconds(125)` gives `2:05`.

Tests: `TimeFormatTests`.

## UI (`Assets/Scripts/Unity/UI`)

uGUI with TextMeshPro (TMP Essential Resources imported into `Assets/TextMesh Pro`).

| Type | Kind | Role |
|------|------|------|
| `GameFlow` | Plain C# | Decides which screen is visible and what the buttons do. |
| `MenuScreen` | MonoBehaviour | One button per difficulty (labels come from `DifficultySettings.DisplayName`) and lifetime kills. |
| `HudScreen` | MonoBehaviour | Remaining time, kill count, health bar. Hosts the joystick. |
| `ResultScreen` | MonoBehaviour | "YOU SURVIVED" / "YOU DIED", kills, survived time, lifetime kills, Play Again and Menu. |
| `DamageFlash` | MonoBehaviour | Full-screen red tint when the player is hit, fading out in 0.35 s. |

Screen flow:

```
Menu --difficulty button--> HUD (playing) --run ends--> Result --Play Again--> HUD
                                                               --Menu--------> Menu
```

Behaviour details:
- **The joystick lives under the HUD.** Hiding the HUD at the end of a run disables the joystick, and its
  `OnDisable` releases it, so a finger still on the screen does not keep steering in the next run.
- **Lifetime kills are already saved when the result screen reads them.** `GameWorld` subscribes to
  `Session.Ended` in its constructor, before `GameFlow` does, and C# events call handlers in subscription order.
- **No garbage from the HUD.** The timer and kill texts are rebuilt only when the displayed number changes
  (once per second for the timer), not every frame.
- **Damage feedback.** `Player.Health.Damaged` triggers `DamageFlash`. The flash image is disabled when fully
  transparent, since a full-screen transparent image still costs GPU fill rate on mobile.
- The bootstrap passes `SnapToPlayer` to `GameFlow`, which calls it after a run (re)starts, so the camera
  and player model jump to the start position instead of sliding there.

Canvas hierarchy (`UI`, sibling order = draw order):

```
UI
|- HudScreen      JoystickArea (touch area + Background/Handle), HealthBar/Fill, Timer, Kills
|- DamageFlash
|- ResultScreen   Title, Kills, Survived, TotalKills, ReplayButton, MenuButton
|- MenuScreen     Title, Subtitle, Easy/Normal/HardButton, TotalKills
```

**Verified through MCP** in play mode: menu shows the saved lifetime kills; Hard starts a run with a
150 enemy cap; the player's death shows "YOU DIED" with kills, survived time and the updated total;
the joystick is disabled on the result screen; Play Again restores full health; Menu clears the arena;
Easy starts a run with a 40 enemy cap; taking damage shows the red flash.

## Animation (`Assets/Animations`)

Mixamo clips (FBX for Unity, without skin, 30 FPS, in place), downloaded on Mixamo's Y Bot and
retargeted to our characters through Unity's **Humanoid** system.

| Clip | Used by | Loop |
|------|------|------|
| Rifle Aiming Idle, Rifle Run | Player (`AC_Player`) | yes |
| Zombie Walk, Zombie Attack | Enemy (`AC_Enemy`) | yes |
| Zombie Death | Enemy and player death | no |

Import settings: rig Humanoid (avatar created from each file), root rotation / height / position baked into
the pose, since the code moves the characters. `player.fbx` and `enemy.fbx` were switched from Generic to
Humanoid so the clips can be retargeted; only their import settings (`.meta`) changed, the FBX files are untouched.

**Controllers** (`Assets/Animations/Controllers`, created through MCP):
- `AC_Player`: `Locomotion` 1D blend tree (Rifle Aiming Idle at `Speed` 0, Rifle Run at 1) and `Death` from Any State on the `Dead` trigger.
- `AC_Enemy`: `Walk` <-> `Attack` on the `InRange` bool, `Death` from Any State on `Dead`.
  The Attack state's speed comes from the `AttackSpeed` parameter.

**Driving the Animators** (no Animator logic in Core):
- `PlayerView` sets `Speed` from `PlayerCharacter.SpeedFraction` (0..1, joystick tilt), with a short damp.
- `EnemyView.Begin` (when taken from the pool) resets the Animator, starts `Walk` at a random point of the cycle
  (so a wave does not walk in lockstep) and sets `AttackSpeed = attackClipLength / attackInterval`,
  so one swing lasts exactly one attack interval of the simulation.
- `EnemyView.Sync` sets `InRange` only when `Enemy.IsInAttackRange` changes.
- Parameters are set by hashed ids (`AnimatorIds`), not strings.

**Death animations.** The simulation removes a killed enemy immediately. `EnemyDeathViews` listens to
`EnemySystem.Died` (raised before `Despawned`), **detaches** the model from the registry, plays `Death`
and returns the model to the pool after `corpseSeconds` (2.2 s). The following `Despawned -> Hide` finds nothing
to hide. Corpses are cleared when a new run starts. The player's `Health.Died` triggers the player's death animation.

**Rifle.** `rifle.fbx`'s embedded material has no textures, so `M_Rifle` (URP Lit with the provided albedo,
metallic/smoothness and normal maps) is assigned on the player prefab. The normal map is imported as a
normal map and the metallic map as linear data (import settings only). The rifle's offset under
`mixamorig:RightHand` was computed through MCP from the sampled Rifle Aiming Idle pose: the barrel points from the
right hand (grip) to the left hand (foregrip), then checked with rendered close-ups of idle and run.

**Cost note for the optimization phase.** Every enemy has its own Humanoid Animator (retargeting is more
expensive than Generic) with the default `Cull Update Transforms` mode, driving a 65-bone skinned mesh.
This is expected to be one of the main CPU costs at high enemy counts and will be measured first.

**Known polish items:** after a run ends the simulation freezes but the Animators keep playing (enemies walk
in place behind the result screen); in Rifle Run the left hand leaves the rifle, so the rifle tilts upwards
while running (a limitation of that clip). The first idle clip (Rifle Idle, rifle held across the body) was
replaced with Rifle Aiming Idle so the rifle points at the target while standing and shooting.
