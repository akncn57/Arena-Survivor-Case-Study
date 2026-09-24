# Arena Survivor: Technical Overview

**English** | [Türkçe](TECH.tr.md)

How the project is structured and what each system does. Updated as each system is added.

## Architecture

The game logic is plain C# classes. MonoBehaviours are kept to a thin layer that connects that logic to Unity:
reading input, moving transforms, playing animations, showing UI.

Why:
- **Testable.** Plain classes can be created in an EditMode unit test with `new`, with no scene or play mode.
- **Fast.** One system updates all enemies in a single loop, instead of hundreds of `Update()` calls on individual
  MonoBehaviours.
- **Explicit.** Dependencies are passed through constructors, so it is visible what each class needs.

No dependency injection framework is used; dependencies are passed through constructors. Wiring happens in two places:
- `GameWorld` (Core) creates all gameplay systems and connects their events. Being plain C#, the whole game can be
  simulated in a test.
- A bootstrap MonoBehaviour in the scene (Unity side) creates the `GameWorld` from the data assets, feeds it input
  every frame and connects the visual layer to its events.

A container such as VContainer was considered. At this project size manual wiring is shorter, has no package
dependency and keeps every connection visible in one file. Classes already use constructor injection, so switching
later would only change the wiring code.

### Assemblies

| Assembly | Folder | Contents | References |
|----------|--------|----------|------------|
| `ArenaSurvivor.Core` | `Assets/Scripts/Core` | Game logic, plain C#, no MonoBehaviours | UnityEngine only (math types, JsonUtility) |
| `ArenaSurvivor.Unity` | `Assets/Scripts/Unity` | Thin Unity layer: bootstrap, views, input, camera, UI | Core, Input System, uGUI, TextMeshPro |
| `ArenaSurvivor.Editor` | `Assets/Scripts/Editor` | Editor tools: clip baking, endless mode setup | Core, Unity, uGUI, TextMeshPro |
| `ArenaSurvivor.Tests.EditMode` | `Assets/Tests/EditMode` | NUnit EditMode tests for Core | Core, Unity Test Framework |

Assembly definitions keep compile times short and enforce the direction of dependencies: Core cannot reference the
Unity layer or the tests.

### Running the tests

Unity: **Window > General > Test Runner > EditMode > Run All**.
Test folders mirror the source folders (`Core/Save` -> `Tests/EditMode/Save`).

## Systems

### Save (`Core/Save`)

Keeps the lifetime kill count across app restarts.

| Type | Role |
|------|------|
| `SaveData` | The data that is stored. Serializable class with `version`, `totalKills` and the endless records (`bestEndlessSeconds`, `bestEndlessLevel`). |
| `ISaveService` | Interface for loading and saving `SaveData`. Game code only knows this interface. |
| `JsonFileSaveService` | Stores `SaveData` as JSON in a file (in the game: `Application.persistentDataPath/save.json`). |
| `ProgressService` | What the game uses: `TotalKills`, `AddKills(int)` and, for endless, `RecordEndlessRun(seconds, level)`. Loads once, saves on every change. |

Flow:

```
Game start:  ProgressService(new JsonFileSaveService(path))  -> Load() -> data kept in memory
Run ends:    progress.AddKills(runKills)                     -> Save() -> file written
```

Behaviour details:
- **Atomic write.** `Save` writes to `save.json.tmp` first, then swaps it into place with `File.Replace` (or
  `File.Move` the first time). If the app is killed mid-write, the old save is still intact.
- **Never blocks the game.** A missing, empty or corrupt file loads as defaults (0 kills) and logs a warning.
- **Sanitized.** A negative kill count or negative/invalid endless records from a hand-edited file are clamped to 0.
- **Old files stay compatible.** The endless record fields were added later; `version` was not bumped because the
  missing fields of an old file load as 0 (tested). Best time and best level are kept separately: the longest run and
  the run that reached the highest level can be different runs.
- **Versioned.** `version` lets a future release migrate old files. New fields just need safe defaults, since
  `JsonUtility` leaves missing keys at their field initializer values.

Tests: `JsonFileSaveServiceTests` (real files in a per-test temp folder; old-format file, negative records) and
`ProgressServiceTests` (in-memory fake `ISaveService`, no disk access; records kept separately, no save without an
improvement).

### Session (`Core/Session`)

One run of the game: the 3-minute survival timer, the kill count and the outcome. An endless run has no time limit.

| Type | Role |
|------|------|
| `GameState` | `Idle` (difficulty selection), `Playing`, `Won`, `Lost`. |
| `GameSession` | Owns the timer and kill count, decides win/lose, raises `Started` and `Ended`. |
| `RunResult` | Passed with `Ended`: outcome, kills, survived seconds. Used by the result screen. |

State flow:

```
Idle --Start(180)--> Playing --timer reaches 180 s--> Won
                        |
                        +--NotifyPlayerDied()-------> Lost
Won / Lost --Start(180)--> Playing   (replay, all values reset)
Won / Lost --ReturnToIdle()--> Idle  (back to difficulty selection)
Idle --StartEndless()--> Playing --NotifyPlayerDied()--> Lost   (endless: the timer never runs out, only death ends it)
```

Behaviour details:
- **Time comes from outside.** `Tick(deltaTime)` is called by the Unity layer every frame. The session never reads
  `Time.deltaTime` itself, so a test can simulate a full 3-minute run in microseconds.
- **Ends exactly once.** After `Won` or `Lost`, further ticks, kills and deaths are ignored. A projectile that hits
  after the timer ended does not count, and the player cannot die after winning.
- **`Progress` (0..1)** tells other systems how far the run is. Spawning uses it to ramp up difficulty.
- **Endless.** `StartEndless()` makes the duration infinite (`Duration = +∞`). The same code path runs: `Elapsed` never
  reaches infinity, so the run is never won, `Remaining` is infinite and `Progress` is 0; `IsEndless` tells the UI.
  In endless the difficulty ramp is computed from elapsed time instead of `Progress` (see Endless mode).
- **Persisting kills is not the session's job.** The composition root subscribes `Ended` to
  `ProgressService.AddKills`, which keeps the session free of save logic.

Tests: `GameSessionTests` (state transitions, timer, kills, replay reset, a simulated 60 FPS run, endless not ending
even after an hour and ending only by death).

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
| `Difficulty_Benchmark` | 0.5 | 10 | 150 |

`Max alive` is a hard cap: waves are trimmed so the enemy count never exceeds it. It bounds the worst-case CPU/GPU
load, which also makes it the main knob for the performance tests. The benchmark asset is not shown in the menu; only
the benchmark mode uses it.

Why two types instead of one ScriptableObject: a ScriptableObject can only be created through Unity
(`CreateInstance`) and its fields are set through the Inspector. Keeping the logic in a plain class lets tests build
a config with `new` and check the math directly.

Tests: `DifficultyConfigTests` (start/end/midway values, clamping, validation).

### Combat: Health (`Core/Combat`)

`Health` holds hit points for the player and for every enemy.
- `TakeDamage` ignores zero/negative amounts and clamps at 0.
- `Damaged(amount)` fires on every hit (used for hit feedback). `Died` fires **exactly once**; a dead target ignores
  further damage.
- `Reset()` refills it for a new life (pooled enemy reused, player on replay).
- While `IsInvulnerable` is set, damage is ignored (used by the benchmark mode).
- `Heal(amount)` heals up to `Max`, never heals the dead, and reports the amount applied through `Healed` (health
  pack, First Aid card).
- `IncreaseMax(amount)` raises both `Max` and `Current`; the missing health stays the same (Tough Skin card).

Tests: `HealthTests`.

### Enemies (`Core/Enemies`)

Spawning, chasing, attacking and dying for all enemies.

| Type | Role |
|------|------|
| `EnemyConfig` | Stats: health, speed, contact damage, attack interval, attack range. `Enemy_Zombie`: 30 health, 10 damage. |
| `EnemyDefinition` | ScriptableObject wrapper for `EnemyConfig` (`Assets/Data/Enemies/Enemy_Zombie`). |
| `Enemy` | State of one enemy: position, facing, health, "in attack range" flag. Plain data, no behaviour. |
| `EnemySystem` | Owns all enemies. Spawns from a pool, updates all of them in one loop, applies damage, despawns. |
| `WaveSpawner` | Decides when and where a wave appears, based on difficulty and run progress. |

**One system, one loop.** There is no MonoBehaviour per enemy. `EnemySystem.Tick` walks one list and, for each enemy:
turns towards the player, moves by `speed * deltaTime` but stops at the attack range edge, and attacks when in range
and its cooldown is over. With 150 enemies this is one method call per frame instead of 150 `Update()` calls.

**Talking to the Unity side with events.** The system does not know about GameObjects:
- `Spawned(enemy)`: the view layer takes an enemy model from its own pool and follows this enemy.
- `Died(enemy)`: killed by the player. The composition root forwards it to `GameSession.RegisterKill`.
- `Despawned(enemy)`: the enemy left (killed or cleared). The view layer returns the model to its pool.

**Pooling.** `Enemy` objects come from `UnityEngine.Pool.ObjectPool<T>` (built into Unity, no package). A killed
enemy goes back to the pool and is reset on the next spawn, so after warm-up (`Prewarm`) no allocations happen during
play. Removing from the active list is O(1): the last enemy is moved into the freed slot (`ActiveIndex` remembers each
enemy's slot).

**Damage to the player is applied once per tick, after the loop.** The total from all attacking enemies is summed and
applied at the end. The player dying can trigger listeners that clear all enemies; doing that in the middle of the
loop would break the iteration. A test guards this case.

**Endless multipliers.** `HealthMultiplier` (only enemies spawned from now on), `SpeedMultiplier` and
`DamageMultiplier` (all enemies) grow over time in endless; `ResetModifiers()` sets them back to 1 at the start of
every run. A pooled enemy is spawned with `Health.Reset(SpawnHealth)`, i.e. with the current multiplier.

**Wave spawning.**
- The first wave appears on the first tick of a run, then one wave every `GetSpawnInterval(progress)` seconds.
- Wave size is `GetWaveSize(progress)`, trimmed so `AliveCount` never exceeds `MaxAliveEnemies`.
- Enemies appear on a ring of `spawnRadius` around the player (outside the camera view), at a random angle, clamped
  inside the arena. Near the arena edge clamping can bring a spawn closer than the ring; accepted as a minor trade-off
  for simplicity.
- Randomness comes from an injected `System.Random`, so tests use a fixed seed and are repeatable.

**Separation.** Enemies do not walk into each other; instead of one pile on top of the player they form a crowd in
which individual enemies can be told apart. Tuned with `separationRadius` (1.2 m) and `separationStiffness` (1) in
`EnemyConfig`; it is off when either is 0.

- **Neighbour lookup: `SpatialGrid` (`Core/Spatial`).** The arena is split into cells as wide as the separation
  radius. Each cell is a linked list stored in two int arrays (`heads` per cell, `next` per element); it is rebuilt
  in O(n) every frame and allocates nothing. Each enemy is only compared with the ones in the 3 x 3 cells around it.
  For 150 enemies brute force compares every pair (150 x 149 = 22,350 distance checks); in the benchmark crowd the
  grid does about 1,600 (measured in play mode through MCP, `EnemySystem.LastSeparationChecks`).
- **A position correction, not a force.** The first attempt pushed overlapping enemies apart with a force. The rear
  ranks walking towards the player squeezed the front ranks and the crowd collapsed into a tight disc of about 4 m
  (closest pair 0.32 m, 36 enemies within 1.5 m of the player). The current solution moves every overlapping pair
  apart by half of the overlap (times the stiffness); the overlap is resolved within a few frames whatever the walking
  speed. All corrections are computed first and applied afterwards, so the result does not depend on list order.
- **Choosing the values** (150 enemies, 15 s simulation, measured through MCP):

  | Radius / stiffness | Average nearest neighbour | Crowd radius | Attacking at once | Tick |
  |------|------|------|------|------|
  | Force-based push (first attempt) | - | ~4 m | 36 | - |
  | 1.0 m / 0.5 | 0.66 m | 4.8 m | - | 0.13 ms |
  | 1.0 m / 1.0 | 0.73 m | 5.9 m | - | 0.10 ms |
  | **1.2 m / 1.0 (chosen)** | **0.90 m** | **7.1 m** | **6** | **0.11 ms** |
  | 1.4 m / 1.0 | 1.10 m | 8.5 m | 4 | 0.09 ms |

- **Effect on gameplay.** The number of enemies reaching the player at the same time is limited (about 6); the crowd
  cannot crush the player at once, it attacks in turns. Hard may therefore be easier than before; the values can be
  tuned by playtesting.

Tests: `EnemySystemTests` (movement, range, attack cooldown, damage stacking, death events, pool reuse, clearing during
the player's death, separation: symmetric push, ignoring enemies outside the radius, spreading enemies spawned on top
of each other, resolving in one frame at full stiffness, a 150-enemy crowd keeping its spacing, only neighbours being
checked), `SpatialGridTests` (the grid finds exactly the same neighbours as brute force, positions outside the arena
are clamped to edge cells, capacity grows) and `WaveSpawnerTests` (timing, max alive cap, progress ramp, spawn ring,
arena clamping, restart).

A weakness the tests caught: the first "only neighbours are checked" test placed enemies exactly 2 m apart; none of
them fell into a neighbouring cell, the check count was 0 and the test passed without measuring anything. It was
fixed by scattering the enemies like a random crowd and requiring at least one check.

### Player (`Core/Player`)

| Type | Role |
|------|------|
| `PlayerConfig` | Stats: max health, move speed. |
| `PlayerDefinition` | ScriptableObject wrapper (`Assets/Data/Player/Player_Default`). |
| `PlayerCharacter` | Position, facing, velocity and `Health` of the player. |

(The class is `PlayerCharacter`, not `Player`, because a type with the same name as its namespace forces awkward
fully-qualified names everywhere else.)

- **Input.** `Move(deltaTime, Vector2 input)` takes the joystick vector. `x` maps to world X and `y` to world Z.
  The camera never rotates around the vertical axis, so "joystick up" always means "up the screen".
- **Analog speed.** Half tilt moves at half speed. Input longer than 1 is clamped, so diagonals are not faster.
- **Arena bounds.** The position is clamped inside the arena.
- **Facing.** Moving turns the player towards the movement direction. `AimAt(point)` is called afterwards when there
  is a target, so the rifle points at the enemy being shot, even while running the other way.
- **Dead players do not move.** `Reset(position)` restores position and health for a new run.
- `SpeedFraction` (0..1) drives the idle/run animation blend.
- **Upgrades.** `MoveSpeedMultiplier` scales the move speed (`SpeedFraction` is still 1 at full tilt). `Reset` sets
  the multiplier back to 1 and the health back to the base value from the config, undoing the last run's Tough Skin
  cards.

Tests: `PlayerCharacterTests`.

### Weapons (`Core/Weapons`)

| Type | Role |
|------|------|
| `WeaponConfig` | Rifle stats: damage (10), fire interval, range, projectile speed, multishot spread angle (12°). |
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
- **Bullets are simulated, not instant hits.** They fly straight at `projectileSpeed` and can miss an enemy that moves
  out of the way. They fly `range x 1.5` before expiring, so they still reach a target that moved slightly out of range.
- **No tunnelling.** Hits are tested against the segment travelled this frame, not just the end point. On a slow
  frame a bullet may move further than an enemy is wide; a point check would jump over it. A test fires a bullet
  100 units in one tick to guard this.
- **One hit per bullet.** A bullet damages the first enemy on its path and disappears (no piercing).
- **No Unity physics.** No colliders or rigidbodies: a few multiplications per bullet-enemy pair, deterministic results
  in tests, and Unity's physics engine does not run at all. The cost is O(bullets x enemies) per frame; bullets are few
  (one every 0.35 s, short-lived), so this stays small.
- **Pooling.** Same pattern as enemies: `ObjectPool<Projectile>`, O(1) swap-remove, `Prewarm`, `Clear`. The update
  loop runs backwards, so a removal only moves an already-updated bullet into the freed slot.
- **Upgrade multipliers** (changed by the endless cards, reset by `Weapon.Reset()`): `DamageMultiplier`,
  `FireRateMultiplier` (the fire interval is divided by it), `RangeMultiplier` (targeting range and bullet distance)
  and `ExtraProjectiles`. Damage is rounded and never below 1.
- **Multishot.** One shot fires `1 + ExtraProjectiles` bullets as a fan centred on the target: with an odd count the
  middle bullet flies straight at the target, with an even count the bullets pass on both sides of it; neighbouring
  bullets are `multishotSpreadDegrees` apart. `Fired` is raised once per shot, not per bullet (the muzzle flash
  lights once).
- **Why damage 1 -> 10 and enemy health 3 -> 30.** Damage is an integer. With a base damage of 1, a +25% damage card
  rounded 1.25 back to 1, so the cards would have had no effect. Both were multiplied by 10: an enemy still dies in
  3 hits, and the timed modes and the benchmark behave exactly as before.

Tests: `TargetingTests`, `WeaponTests`, `ProjectileSystemTests` (movement, expiry, hits, tunnelling, misses, kills,
several bullets, pool reuse). `WeaponTests` also covers the fan angles (odd and even counts), fire rate, damage
rounding, range multiplier and `Reset`.

### World (`Core/World`)

`GameWorld` is the whole simulation in one object. `WorldConfig` holds arena-wide settings (run duration, arena size,
spawn radius, bullet hit radius, pool prewarm counts).

Two modes share the same systems: **timed** (`StartRun`, the game the case asks for) and **endless**
(`StartEndless`, see Endless mode). XP/health drops, levels and cards only run in endless; the timed modes and the
benchmark are exactly as before.

**What it owns.** It creates `GameSession`, `PlayerCharacter`, `EnemySystem`, `ProjectileSystem`, `Weapon`,
`WaveSpawner`, `PickupSystem`, `Experience` and `UpgradeSystem`, and exposes them (except the spawner) so the Unity
side can draw them and subscribe to their events.

**Event wiring** (the only place systems are connected to each other):

| Event | Handler | Effect |
|------|------|------|
| `Enemies.Died` | `Session.RegisterKill` | Kill counter |
| `Player.Health.Died` | `Session.NotifyPlayerDied` | Run lost |
| `Enemies.Died` | `OnEnemyDied` (endless only) | XP gem where the enemy died, a health pack by chance |
| `Pickups.Collected` | `Experience.Add` / `Player.Health.Heal` | XP or health |
| `Experience.LeveledUp` | `OfferUpgrade` | Cards are rolled, `UpgradeOffered` is raised, the simulation pauses |
| `Session.Ended` | `Progress.AddKills` (+ `RecordEndlessRun` in endless) + store `LastResult` | Lifetime kills and records saved, result screen data |

**Frame order** in `Tick(deltaTime, joystickInput)`:

```
0. If a card choice is waiting, nothing happens (the game is paused)
1. Player.Move            the player moves first; everything else reacts to the new position
2. WaveSpawner.Tick       new enemies around the player (endless: enemy multipliers are updated first)
3. Enemies.Tick           chase and attack; may kill the player -> run ends, stop here
4. Weapon.Tick + AimAt    pick target, fire, face the target
5. Projectiles.Tick       bullets fly and hit; kills counted through Enemies.Died (endless: XP drops)
6. Pickups.Tick           endless: XP and health fly to the player and are collected; a level up offers cards
7. Session.Tick           timer last, so a kill in the final frame still counts before the win
```

**Run flow.**
- `StartRun(difficulty, options)`: clears enemies and bullets, resets player and weapon, starts the spawner and the timer.
- `RunOptions` (default: a normal game): fixed `Seed`, `Invulnerable` player, `Duration` and `SkipProgress` (not added
  to lifetime kills). Used by the benchmark mode.
- `StartEndless(options)`: the same preparation (arena, player, weapon, enemy multipliers, XP and cards reset), then
  a session without a time limit.
- `Replay()`: restarts with the same mode, difficulty and options. Lifetime kills and records are kept.
- `ReturnToMenu()`: clears the arena and goes back to `Idle` for difficulty selection.
- After a win or loss `Tick` does nothing, so the arena stays frozen behind the result screen.

Tests: `GameWorldTests` are integration tests of all Core systems together: kills are counted, win and loss both save
kills, the arena freezes after the run, replay resets everything but lifetime kills, kills of two runs add up, every
`Spawned` event is matched by a `Despawned` event (otherwise the Unity side would leak models), and the same seed
produces the same spawns. A smoke test plays a full 3-minute run at 60 FPS with the default tuning.

`GameWorldEndlessTests` test the endless mode end to end (below).

Shared test doubles live in `Tests/EditMode/TestDoubles` (`InMemorySaveService`).

### Endless mode (`Core/Endless`, `Core/Progression`, `Core/Upgrades`)

A separate mode started from the **ENDLESS** button in the menu. There is no timer; the run lasts until the player
dies. It is an addition on top of the timed game the case asks for and does not change the timed modes or the benchmark.

- Every killed enemy drops an **XP gem**, and with a 3% chance a **health pack** (20 health).
- Gems fly to the player once the player comes close (magnet radius) and are collected. A full XP bar means a
  **level up**.
- On a level up the game pauses and **3 upgrade cards** appear; the player picks one and the game continues.
- Over time enemies come **in larger numbers and become tougher, faster and harder-hitting**.
- The longest time and the highest level are saved and shown in the menu and on the result screen.

| Type | Role |
|------|------|
| `EndlessConfig` / `EndlessSettings` | All endless settings (below); ScriptableObject `Assets/Data/Endless/Endless_Default`. |
| `EnemyScalingConfig` | Growth per minute of the enemy health, speed and damage multipliers; a cap for speed. |
| `Pickup`, `PickupKind`, `PickupSystem`, `PickupConfig` | XP gems and health packs on the ground: pool, one loop, magnet, collection. |
| `LevelingConfig`, `Experience` | XP curve, level and bar fill. |
| `UpgradeKind`, `UpgradeEntry`, `UpgradeSystem` | Card definitions, card rolls (random, no duplicates), the effect of a chosen card. |

**Cards** (`UpgradeEntry.CreateDefaults`, editable in the Inspector):

| Card | Effect (per level) | Max |
|------|------|------|
| Hollow Points | +25% damage | 5 |
| Rapid Fire | +20% attack speed | 5 |
| Multishot | +1 projectile (12° fan) | 4 |
| Long Barrel | +15% range | 3 |
| Tough Skin | +25 max health (and heals as much) | 5 |
| Swift Boots | +10% move speed | 4 |
| Magnet | +50% pickup radius | 3 |
| First Aid | Restores 40% of health | unlimited, "filler" card |

- **Rolling cards.** 3 different cards are picked at random from the cards not yet maxed (partial Fisher-Yates).
  "Filler" cards (`maxLevel = 0`, First Aid) only fill the gap when fewer than 3 real cards are left; even with
  everything maxed the player still gets a choice.
- **Effects are computed from the level.** A value is not multiplied up on every pick; `1 + value x level` is
  recomputed instead: 3 damage cards are exactly +75%, no floating point error builds up, and the effect is easy to read.
- **The systems own their stats.** The card system only sets the multiplier properties of `Weapon`,
  `PlayerCharacter` and `PickupSystem`; each of them returns to its base values with its own `Reset` at the start of a run.

**Levels and pausing.**
- The XP curve is linear: 4 XP for level 1 -> 2, then each level needs 2 more than the previous one (4, 6, 8, ...).
  Extra XP carries over to the next level; one big gem can raise several levels, and `LeveledUp` fires once per level.
- On a level up `GameWorld` puts the cards into `UpgradeOffer` and raises `UpgradeOffered`. While a card is waiting,
  `GameWorld.Tick` does nothing; the simulation pauses by itself (verifiable in tests without Unity).
- `ChooseUpgrade(index)` applies the card; if another level up is waiting, new cards are offered right away.
- The Unity side also sets `Time.timeScale = 0`, so enemy animations, particles, the camera and the spinning gems
  freeze too. UI buttons run on unscaled time, so the cards stay clickable.

**Difficulty ramp** (`EndlessConfig`, defaults):
- **More enemies:** the spawn settings reuse `DifficultyConfig`; the start -> end ramp advances with
  `elapsed time / rampSeconds` instead of the run's `Progress`, then stays at the end values. 2 enemies every 3 s at
  0:00, 12 enemies every 0.7 s at 7:00; at most 100 alive.
- **Stronger enemies:** per minute +30% health, +15% damage (unlimited), +8% speed (at most 1.6x; an upgraded player
  can still outrun them). The health multiplier applies to newly spawned enemies; speed and damage to all of them.
- Since health and damage grow without limit, every endless run ends eventually; that is what gives the record meaning.
- **How the values were chosen.** The defaults were picked with a simulation that runs Core without Unity: a simple
  bot that runs in circles around the arena and picks random cards. With the first values the bot died in endless as
  fast as on Normal (~1.5 min) and levelled up slowly. The start was softened (2 enemies every 3 s) and the XP curve
  made faster; the bot now lasts 2-3.5 min and reaches level 5-9. A player who picks cards on purpose lasts longer.

**Pickups** (`PickupSystem`, the same pattern as enemies and bullets: pool, one loop, O(1) swap-remove, view events):
- A gem waits where it dropped until the player enters the magnet radius (3 m, grown by the Magnet card); then it
  flies to the player at 14 m/s (and keeps following even if the player walks away) and is collected within 0.8 m.
- **Cap.** At most 250 pickups lie in the arena. At the cap new XP is added to an existing gem (XP is never lost) and a
  health pack is skipped. This bounds the view and loop cost of a very long run.
- A collected pickup raises `Collected` first (XP/health applied), then `Despawned` (the view returns to the pool).

**Unity side.** `PickupViews` keeps one `ViewRegistry` per kind; gems hover, bob and spin (the phase comes from the
position, so neighbouring gems do not bob in lockstep). The prefabs (`Pickup_Experience`: a turquoise cube tipped onto
a corner, `Pickup_Health`: a white box with a red cross) use URP Simple Lit with a little emission, GPU instancing on,
no shadows and no colliders; for many small objects real-time shadows are not worth their cost.

Tests: `PickupSystemTests` (magnet, following, collection order, merging XP at the cap, pool, many pickups collected
in one tick), `ExperienceTests` (curve, carry-over, several levels), `UpgradeSystemTests` (distinct and complete card
rolls, max level, filler card, all effects, description text), `EndlessConfigTests` (multipliers, speed cap, ramp) and
`GameWorldEndlessTests` (integration: endless does not end, kills give XP, no drops in timed mode, health packs heal,
a level up pauses the game and picking a card resumes it, consecutive level ups are offered one after another,
enemies grow stronger, death records the best run, Replay and switching to timed mode reset everything, every pickup
spawn has a despawn, a 5-minute smoke test).

### Input maths (`Core/Input`)

`JoystickModel` is the maths of the floating on-screen joystick, kept in Core so it can be tested:
- `Press(point)` puts the joystick base where the finger touched down.
- `Drag(point)` moves the handle, clamped to `radius`. `Value` is the direction with strength 0..1.
- A **dead zone** (10% of the radius by default) ignores a resting thumb. Outside it the strength is rescaled to start
  from 0, so there is no jump when leaving the dead zone.
- `Release()` zeroes everything.

Tests: `JoystickModelTests`.

### Presentation helpers (`Core/Presentation`)

`TimeFormat` turns seconds into HUD text, kept in Core so the rounding rules are tested:
- `CountdownSeconds` rounds **up** (0.2 s left still shows `0:01`; `0:00` only when time is really over).
- `ElapsedSeconds` rounds **down** (59.9 s survived is `0:59`).
- `MinutesSeconds(125)` gives `2:05`.

Tests: `TimeFormatTests`.

## Unity layer (`Assets/Scripts/Unity`)

Only `GameBootstrap` has an `Update()`. Every other component is either plain C# or a MonoBehaviour driven by the
bootstrap, so the order of everything that happens in a frame is visible in one method.

| Type | Kind | Role |
|------|------|------|
| `GameBootstrap` | MonoBehaviour | Composition root. Builds `GameWorld` from the data assets, connects views to its events, runs the frame. |
| `VirtualJoystick` | MonoBehaviour (uGUI) | Turns pointer events into `JoystickModel` calls and moves the two joystick images. |
| `MoveInput` | Plain C# | Joystick while touched, otherwise keyboard (WASD/arrows) or gamepad, for testing in the Editor. |
| `ViewRegistry<TModel, TView>` | Plain C# | Maps simulation objects to pooled GameObjects. |
| `PlayerView` | MonoBehaviour | Copies the player's position, turns smoothly towards its facing, drives the animation. |
| `EnemyView` | MonoBehaviour | Drives one enemy's position, facing and animation. |
| `EnemyDeathViews` | Plain C# | Keeps a killed enemy's model in the scene for its death animation. |
| `PickupViews` | Plain C# | Draws the endless XP gems and health packs (one `ViewRegistry` per kind). |
| `FollowCamera` | Plain C# | Tilted follow camera with a fixed offset and light smoothing. |

**Frame** (`GameBootstrap.Update`):

```
input  = MoveInput.Read()                 (zero during the benchmark)
world.Tick(deltaTime, input)              simulation (see GameWorld)
playerView.Sync                           draw the player
enemyViews.Sync / projectileViews.Sync    draw every enemy and bullet
enemyDeaths.Tick                          corpse timers
pickupViews.Tick                          endless pickups (hovering, spinning)
camera.Follow                             camera last, so it sees the final player position
flow.Tick                                 HUD, damage flash, benchmark recording
```

**View pooling.** `ViewRegistry` is connected to a system's events: `Spawned -> Show` takes a GameObject from a pool,
activates it and remembers which model it draws; `Despawned -> Hide` deactivates it and puts it back. Enemy and bullet
GameObjects are all created at startup (prewarm), so no `Instantiate`/`Destroy` happens during play. A freshly shown
view is placed immediately, so it never flashes at its old position.

**No per-frame garbage.** The sync callbacks are cached delegates. Passing a method name directly
(`Sync(SyncProjectile)`) would create a new delegate object every frame.

### Scene (`Assets/Scenes/Arena.unity`)

Built through Unity MCP.

| Object | Contents |
|------|------|
| `Arena` | 40 x 40 ground and four low walls (placeholders, static-batched, no colliders). |
| `Player` | `Assets/Prefabs/Player_Optimized.prefab`: `PlayerView`, the optimized player model, the rifle under `mixamorig:RightHand`. |
| `GameBootstrap` | References to the data assets, prefabs, camera, joystick and screens. |
| `UI` | Screen-space canvas (reference 1920 x 1080, landscape) with a full-screen `JoystickArea`. |
| `EventSystem` | Uses `InputSystemUIInputModule` (the project uses the new Input System only). |

Prefabs: `Enemy_Optimized.prefab` (used in the game), `Enemy.prefab` and `Player.prefab` (with the original models,
for comparisons), `Bullet.prefab` (small stretched sphere with an unlit material, no collider, no shadows). The models
are linked prefab instances of the original or optimized FBX files.

The game runs in landscape, locked to `LandscapeLeft` (top of the phone on the left) in Player Settings.

**Camera framing.** The camera settings were chosen by rendering the camera at 16:9 through MCP with marker enemies
placed at the weapon range and at the spawn radius:
- Offset `(0, 20, -11.5)` (about 60 degrees down) and a 40 degree field of view. A narrow field of view from further
  away flattens perspective, so the far side of the screen shows less ground than a wide lens would.
- Visible ground around the player: about 15 units left/right, 8 behind, 12 ahead.
- The weapon range (8) fits on screen, so the player only shoots enemies the user can see.
- The spawn radius (18) is outside the view, so enemies walk in from off-screen instead of popping in. At the far
  corners of the screen the view is wider than 18, so a spawn there can occasionally be visible.

## UI (`Assets/Scripts/Unity/UI`)

uGUI with TextMeshPro (TMP Essential Resources imported into `Assets/TextMesh Pro`).

| Type | Kind | Role |
|------|------|------|
| `GameFlow` | Plain C# | Decides which screen is visible and what the buttons do. |
| `MenuScreen` | MonoBehaviour | One button per difficulty (labels come from `DifficultySettings.DisplayName`), the ENDLESS button and its record, lifetime kills, the BENCHMARK button. |
| `HudScreen` | MonoBehaviour | Remaining time (elapsed time in endless), kill count, health bar; in endless also the XP bar and level. Hosts the joystick. |
| `LevelUpScreen` | MonoBehaviour | "LEVEL N!" and 3 `UpgradeCard`s. The game is paused behind it and continues when a card is picked. |
| `UpgradeCard` | MonoBehaviour | One card: title, effect text, "NEW" or "Lv 1 > 2". The whole card is the button. |
| `ResultScreen` | MonoBehaviour | "YOU SURVIVED" / "YOU DIED", kills, survived time, lifetime kills, Play Again and Menu. In endless "GAME OVER", the level and "NEW RECORD!" or the best run. |
| `BenchmarkScreen` | MonoBehaviour | Benchmark result and a Menu button. |
| `DamageFlash` | MonoBehaviour | Full-screen red tint when the player is hit, fading out in 0.35 s. |

Screen flow:

```
Menu --difficulty button--> HUD (playing) --run ends--> Result --Play Again--> HUD
                                                               --Menu--------> Menu
Menu --ENDLESS------------> HUD + XP bar --level up--> Cards (game paused) --card--> HUD
                                         --death-----> Result (endless) --Play Again / Menu
Menu --BENCHMARK----------> HUD (no input) --ends--> Benchmark result --Menu--> Menu
```

Behaviour details:
- **The joystick lives under the HUD.** Hiding the HUD at the end of a run disables the joystick, and its `OnDisable`
  releases it, so a finger still on the screen does not keep steering in the next run.
- **Lifetime kills are already saved when the result screen reads them.** `GameWorld` subscribes to `Session.Ended`
  in its constructor, before `GameFlow` does, and C# events call handlers in subscription order.
- **No garbage from the HUD.** The timer and kill texts are rebuilt only when the displayed number changes (once per
  second for the timer), not every frame.
- **The health bar moves the fill's right edge.** The fill image is stretched over the bar; the health fraction is
  written to the fill's right anchor (`anchorMax.x`) and updated only when health changes. The first version used
  `Image.fillAmount`; when the sprite was removed to fix the fill's squared-off look, the bar stayed full, because uGUI
  silently ignores `fillAmount` on an Image without a sprite. The user noticed it on the phone; the first UI check
  through MCP only read the `fillAmount` value and missed it. The fix was verified by damaging the player and
  measuring the fill width (452/460 px at 100 health, 176 px at 40) and by rendering the HUD.
- **Damage feedback.** `Player.Health.Damaged` triggers `DamageFlash`. The flash image is disabled when fully
  transparent, since a full-screen transparent image still costs GPU fill rate on mobile.
- **Cards are not picked by accident.** The card screen pops up in the middle of play; so that a finger touching the
  screen at that moment does not pick a card nobody has read, the cards ignore taps for 0.4 s (real time). The
  semi-transparent black background blocks touches to the joystick behind it.
- **The pause is always lifted.** `Time.timeScale` goes back to 1 when a card is picked, when a new run starts, when a
  run ends, when returning to the menu and when the bootstrap is destroyed; the Editor or the app never stays frozen.
- **The XP bar uses the same method as the health bar** (the fill's right anchor); it is updated only when the value
  changes and creates no garbage.
- **Optional references.** If the UI fields and prefabs added for endless are not assigned, the game works as before
  and the menu hides the ENDLESS button.
- During play the frame rate is capped at 60 (Android caps at 30 otherwise); during the benchmark at 120.

Canvas hierarchy (`UI`, sibling order = draw order):

```
UI
|- HudScreen        JoystickArea (touch area + Background/Handle), HealthBar/Fill, Timer, Kills, ExperienceBar/Fill+Level
|- DamageFlash
|- LevelUpScreen    Title, Hint, Card0..2 (Title, Description, Level)
|- ResultScreen     Title, Kills, Survived, TotalKills, Record, ReplayButton, MenuButton
|- BenchmarkScreen  Title, Result, MenuButton
|- MenuScreen       Title, Subtitle, Easy/Normal/HardButton (left column), EndlessButton/EndlessRecord (right), TotalKills, BenchmarkButton
```

**Endless setup (`Tools > Arena Survivor > Setup Endless Mode`, `Editor/EndlessModeSetup.cs`).** Builds the scene and
asset side of endless in one click: the `Endless_Default` settings asset, the pickup materials and prefabs, the XP
bar, the card screen, the ENDLESS button, the record texts and all references on the bootstrap and the screens; then
saves the scene. The new UI is built by duplicating existing, already styled objects (health bar, texts, buttons), so
fonts, sprites and colours match the other screens. Running it again is safe: existing objects are found by name and
only the references are refreshed.

**Verified through MCP** in play mode: the menu shows the saved lifetime kills; Hard starts a run with a 150 enemy cap;
the player's death shows "YOU DIED" with kills, survived time and the updated total; the joystick is disabled on the
result screen; Play Again restores full health; Menu clears the arena; Easy starts a run with a 40 enemy cap; taking
damage shows the red flash.

## Animation (`Assets/Animations`)

Mixamo clips (FBX for Unity, without skin, 30 FPS, in place), downloaded on Mixamo's Y Bot and retargeted to our
characters through Unity's **Humanoid** system.

| Clip | Used by | Loop |
|------|------|------|
| Rifle Aiming Idle, Rifle Run | Player (`AC_Player`) | yes |
| Zombie Walk, Zombie Attack | Enemy (`AC_Enemy`) | yes |
| Zombie Death | Enemy and player death | no |

Import settings: rig Humanoid (avatar created from each file), root rotation / height / position baked into the pose,
since the code moves the characters. `player.fbx` and `enemy.fbx` were switched from Generic to Humanoid so the clips
can be retargeted; only their import settings (`.meta`) changed, the FBX files are untouched.

**Controllers** (`Assets/Animations/Controllers`, created through MCP):
- `AC_Player`: `Locomotion` 1D blend tree (Rifle Aiming Idle at `Speed` 0, Rifle Run at 1) and `Death` from Any State
  on the `Dead` trigger.
- `AC_Enemy`: `Walk` <-> `Attack` on the `InRange` bool, `Death` from Any State on `Dead`. The Attack state's speed
  comes from the `AttackSpeed` parameter.

**Driving the Animators** (no Animator logic in Core):
- `PlayerView` sets `Speed` from `PlayerCharacter.SpeedFraction` (0..1, joystick tilt), with a short damp.
- `EnemyView.Begin` (when taken from the pool) resets the Animator, starts `Walk` at a random point of the cycle (so a
  wave does not walk in lockstep) and sets `AttackSpeed = attackClipLength / attackInterval`, so one swing lasts
  exactly one attack interval of the simulation.
- `EnemyView.Sync` sets `InRange` only when `Enemy.IsInAttackRange` changes.
- Parameters are set by hashed ids (`AnimatorIds`), not strings.

**Death animations.** The simulation removes a killed enemy immediately. `EnemyDeathViews` listens to
`EnemySystem.Died` (raised before `Despawned`), **detaches** the model from the registry, plays `Death` and returns the
model to the pool after `corpseSeconds` (2.2 s). The following `Despawned -> Hide` finds nothing to hide. Corpses are
cleared when a new run starts. The player's `Health.Died` triggers the player's death animation.

**Frozen arena after a run.** When `Session.Ended` fires, the bootstrap sets `animator.speed = 0` on every visible
enemy and corpse, so the arena behind the result screen is a still frame. The player keeps animating (death animation
after a loss). Corpses do not fade while frozen and are cleared on Play Again or Menu. `EnemyView.Begin` sets the
speed back to 1 when a pooled view is reused.

Verified through MCP in play mode with `EditorApplication.Step()` and a fixed `Time.captureDeltaTime` (the Editor does
not advance play mode while it is in the background): after the player's death all 16 visible enemy Animators had
speed 0 and an unchanged pose one second later, and the player was in `Death`; after Play Again new enemies animated
normally; after Menu no enemy views were left.

**Rifle.** `rifle.fbx`'s embedded material has no textures. The player prefab uses a URP Lit material with the
provided albedo, metallic/smoothness and normal maps. The rifle's offset under `mixamorig:RightHand` was computed
through MCP from the sampled Rifle Aiming Idle pose: the barrel points from the right hand (grip) to the left hand
(foregrip); checked with rendered close-ups of the idle and run poses.

**Known polish item:** in Rifle Run the left hand leaves the rifle, so the rifle tilts upwards while running (a
limitation of that clip). The first idle clip (Rifle Idle, rifle held across the body) was replaced with Rifle Aiming
Idle, so the rifle points at the target while standing and shooting.

## Benchmark mode (`Core/Benchmark`, `Unity/Benchmark`)

A fixed, repeatable performance test, started from the **BENCHMARK** button in the menu. It exists so the reference
and the optimized build are measured under exactly the same conditions, on the device, without a Profiler connection.

**Scenario** (`BenchmarkSettings` on the bootstrap, spawn tuning in `Difficulty_Benchmark`):

| Setting | Value | Why |
|------|------|------|
| Spawn | 10 enemies every 0.5 s, cap 150 | Reaches the worst case (150 alive) within the warm-up |
| Seed | 12345 | Same spawn positions every run (`RunOptions.Seed`) |
| Player | Invulnerable, no input | The run always lasts its full length, the load is identical |
| Warm-up | 10 s, not measured | The arena fills up, shaders and pools warm up |
| Measured | 60 s | |
| Frame rate cap | 120 (game: 60) | A 60 cap would hide the difference between the builds |
| Save file | Untouched (`RunOptions.SkipProgress`) | A test run must not change the lifetime kills |

**Measured values** (`BenchmarkRecorder`, `BenchmarkResult`):
- Frame time from `Time.unscaledDeltaTime`: average FPS, **1% low FPS** (from the 99th percentile frame time),
  average / p99 / max frame time. The 1% low shows hitches that an average hides.
- CPU main thread and GPU time per frame from `FrameTimingManager` ("Frame Timing Stats" is enabled in Player
  Settings, so this also works in release builds). Shown as `n/a` if the device does not report them.
- Enemies alive (max, average), kills, allocated memory and GC heap size, device, GPU and graphics API.

Recording allocates nothing: `SampleStats` keeps samples in preallocated arrays and sorts once at the end.

**Output:** the result screen, a JSON file in `persistentDataPath/benchmarks/`, and a single log line starting with
`BENCHMARK_RESULT` that can be read over USB:

```
adb logcat -s Unity | findstr BENCHMARK_RESULT
```

Core additions for the benchmark, all tested: `SampleStats` (average, max, nearest-rank percentile), `RunOptions`
(seed, invulnerable, duration, skip progress) for `GameWorld.StartRun`, `Health.IsInvulnerable`. The first test run
caught a float rounding bug in the percentile (`0.99f * 100` is slightly above 99, so the rank was off by one); fixed
with a small epsilon.

Verified end to end in the Editor through MCP (frames stepped manually): the run reached 150 enemies, lasted 70 s,
showed the result screen, wrote the JSON file and the log line, and restored the 60 FPS cap.

**Test device:** Xiaomi Redmi Note 14 Pro (4G, model 24116RACCG), MediaTek Helio G100-Ultra (MT6789), Mali-G57 MC2
GPU, 8 GB RAM, 1080 x 2400 at up to 120 Hz, Android 16.

## Optimized assets (`Assets/Optimized`, `Tools/Blender`)

The provided models in `Assets/Models` are never modified. Optimized versions are generated from them by Blender
scripts (Blender 5.2, run headless) and live in `Assets/Optimized`. Running a script again reproduces the same result,
and every step is explained in the script's comments.

```
blender --background --factory-startup --python Tools/Blender/optimize_enemy.py -- Assets/Models/enemy.fbx Assets/Optimized/Enemy
blender --background --factory-startup --python Tools/Blender/optimize_player.py -- Assets/Models/player.fbx Assets/Optimized/Player
```

| Script | Purpose |
|------|------|
| `inspect_model.py` | Triangles, vertices, bones, bone influences, materials and textures of an FBX |
| `inspect_uv.py` | UV range and face count per material, bone influence histogram |
| `optimize_enemy.py` | Builds the optimized enemy (below) |
| `optimize_player.py` | Builds the optimized player (below) |

### Enemy

| | Original `enemy.fbx` | `Enemy_Optimized.fbx` LOD0 | LOD1 |
|------|------|------|------|
| Triangles | 36,902 | 4,500 | 1,500 |
| Vertices (Unity, after UV/normal splits) | 18,453 | 2,777 | 1,087 |
| Bones | 65 | 22 | 22 |
| Max bone influences per vertex | 6 | 4 | 4 |
| Materials / draw calls | 2 | 1 | 1 |
| Textures | 8 x 4096 x 4096 PNG | 2 x 1024 x 512, ASTC 6x6 on Android | (shared) |
| File size | 100 MB FBX (textures embedded) | 0.35 MB FBX + 1.8 MB PNG | |

What the script does and why:
1. **Finger bones removed.** 40 finger bones and 3 unweighted end bones go (65 -> 22). Their weights are added to the
   hand bone first, so the hands keep their shape and follow the wrist. Fingers are a few pixels on screen; animating
   them is pure CPU cost. Humanoid avatars do not require finger bones, so the Mixamo clips still retarget.
2. **Max 4 bone weights per vertex**, renormalized. Only 668 of 18,453 vertices had more than 4.
3. **One material instead of two.** Both original materials used the full 0..1 UV square with separate texture sets.
   Their UVs are squeezed into the left and right halves of one atlas, so each enemy is one draw call.
4. **LODs by decimation** (Blender's Decimate modifier, collapse). Names ending in `_LOD0`/`_LOD1` make Unity create
   the LOD Group on import. Skin weights are interpolated by the decimation, so the LODs stay rigged.
5. **Texture atlas.** The diffuse and normal maps of both materials are scaled from 4096 to 512 and placed side by side
   (1024 x 512). An enemy covers roughly 100 x 150 pixels on a 1080p screen, so 4096 textures were far beyond what can
   be seen. The specular maps are flat (a 4096 PNG of 58 KB) and are dropped along with the glossiness maps; the
   material uses a constant smoothness of 0.25 instead.

**Quality comparison** (rendered through MCP, same Zombie Walk pose): up close the optimized LOD0 keeps the silhouette,
muscle detail, fingers and head shape; from the game camera's distance LOD0 and LOD1 are indistinguishable.

The optimized enemy actually looks **closer to the provided reference image** (`Assets/Models/enemy.jpg`, matte red
skin) than the original import: the original normal maps are imported as plain color textures, which makes URP read
wrong normals and gives the skin a bluish, glossy look. The optimized normal atlas is imported as a normal map. To keep
the comparison honest, the reference build keeps the original import as it was.

**LOD switching.** With this camera (18-28 m away, 40 degree field of view) an enemy covers 9-14% of the screen height.
LOD0 is used above 12% (the closer half of the screen), LOD1 below. The Unity side is
`Assets/Prefabs/Enemy_Optimized.prefab` (EnemyView + Animator with `AC_Enemy`, `M_Enemy` URP Lit material, blob shadow).

### Player

| | Original `player.fbx` | `Player_Optimized.fbx` |
|------|------|------|
| Triangles | 19,450 (head 8,256 + body 11,194) | 7,999 |
| Meshes / skinned renderers | 2 | 1 |
| Material slots | 3 (two body materials using the same texture) | 2 (body, head) |
| UV sets | 3 (only one used) | 1 |
| Bones | 69 | 69 |
| Diffuse / normal textures | 1024 / 512 (x2) + specular | 512 / 512 (x2), ASTC 6x6 |

Decisions that differ from the enemy, and why:
- **No atlas.** The body UVs extend beyond 0..1 (u from -0.78 to 1.32); the texture is used tiled. An atlas would break
  the tiling. One extra draw call for a single character does not matter.
- **Finger bones kept.** There is only one player; their cost is negligible, while the hand holding the rifle is
  visible. The enemy's fingers were dropped because there are 150 copies of it.
- **No LOD.** The player is always in the middle of the screen, at the same distance.
- Specular maps dropped; the materials use a constant smoothness of 0.3.

Two bugs caught while doing this through MCP: clearing the material list in Blender (`materials.clear()`) also reset
the faces' material indices, and the mesh fell back to a single submesh (noticed by reading the submesh count in Unity,
fixed by writing the indices back after the clear). A texture that was not rescaled was saved empty because Blender
loads pixels lazily (fixed by forcing the pixels to load before saving).

As with the enemy, the original player's normal maps are imported as color textures; the optimized player looks closer
to the provided `player.jpg` reference (light blue-turquoise uniform, dark vest). The rifle's position in the hand was
recomputed on the new skeleton with the same method; the barrel direction came out identical to the original.

### Rifle

The mesh (1,988 triangles) is already light and was not changed. The three textures (albedo, metallic/smoothness,
normal) are copied to `Assets/Optimized/Rifle` and imported at 512 instead of 2048, ASTC 6x6 (`M_Rifle_Optimized`).
The rifle is 30-40 pixels long on screen.

### Render settings (Mobile quality level only)

Changed on `Mobile_RPAsset` / `Mobile_Renderer`; the PC quality level is untouched.

| Setting | Before | After | Why |
|------|------|------|------|
| Post-processing (renderer) | On (Tonemapping Neutral, Bloom 0.25, Vignette 0.2) | Off | Several full-screen passes per frame. Bloom's threshold of 1 is barely reached in this scene, so it cost GPU time for almost no visible effect. |
| HDR | On | Off | An HDR color buffer is twice the bandwidth of LDR; with post-processing off nothing needs it. |
| Shadow distance | 50 m | 35 m | The camera sees at most about 35 m; shadows beyond that were never visible. |
| Enemy shadows | Every enemy rendered a second time into the shadow map | Off, replaced by a blob shadow | See below. |

**Blob shadows.** Real-time shadows for 150 skinned meshes mean drawing (and skinning) every enemy twice. Each enemy
now has a `BlobShadow` child: a quad with a 64 x 64 soft round texture (`Assets/Optimized/Shared`), unlit,
alpha-blended, GPU instanced. Turning shadows off only on the far LOD was tried first and rejected: the shadows then
disappeared at the LOD boundary across the upper half of the screen, which looked inconsistent. The player keeps its
real shadow.

Lesson learned while doing it through MCP: making a URP material transparent by assigning its properties (`_Surface`,
blend factors, keywords) was not enough and the quad stayed invisible; the material had to be set up with URP's own
`BaseShaderGUI.SetupMaterialBlendMode`, which the Inspector also calls. The problem was isolated by rendering the quad
with an opaque red material first (it drew) and then with the real one (it did not).

### Animation optimization (enemy)

Measured first: in the Editor, in the benchmark scene (150 enemies), the Animator update time, the Transform count and
the full frame time were measured through MCP; every change was compared with the same measurement. Editor numbers
are not the same as the phone's; they were only used to tell which change helped. The final result comes from the
benchmark on the phone.

| Change | Editor measurement (150 enemies) |
|------|------|
| Start (Humanoid, bones as GameObjects) | Animator 2.33 ms, 3,978 Transforms, frame 3.76 ms |
| + Optimize Game Objects | Animator 2.33 ms, **459 Transforms**, frame 3.62 ms |
| + baked Generic clips | **Animator 1.07 ms (-54%)**, frame 3.28 ms |

- **Generic clips (`GenericClipBaker`, `Assets/Scripts/Editor`).** A Humanoid Animator converts the clip from the
  shared "human" format to the skeleton every frame, for every enemy (retargeting). **Tools > Arena Survivor > Bake
  Enemy Generic Clips** does that conversion once, in the Editor: the Humanoid clip is sampled at 30 FPS onto a
  temporary copy of the optimized enemy whose bones are restored, and every bone's local position and rotation is
  recorded into a new clip with `GameObjectRecorder` (scale curves are dropped; 22 bones x 7 curves = 154 curves).
  `AC_Enemy_Generic` uses the same states with these clips. Side-by-side renders at two different moments confirmed
  the poses match the Humanoid playback. The player stays Humanoid (one character, the rifle is attached to the hand bone).
- **Optimize Game Objects.** An import setting of the enemy model. No GameObjects/Transforms are created for the bones;
  the animation is written straight into the skinning matrices. Nothing is attached to the enemy, so it could be
  turned on safely. A side effect: sampling poses in the Editor with `AnimationMode` does not work without the bone
  objects; for the comparison renders and in the baking tool the copy's bones are restored with
  `AnimatorUtility.DeoptimizeTransformHierarchy`.
- **Cull Completely.** Enemies off screen are not animated at all. In the benchmark most enemies are on screen, so the
  gain is small there; it applies to the enemies walking in from the spawn ring in normal play.

Result on the phone (`PERFORMANCE.md` > Step 6): CPU main thread 9.9 ms -> 7.8-8.4 ms, 4-5 MB less memory.

### Enemy material

Enemies use `M_Enemy_SimpleLit_NoNormal`: URP **Simple Lit** (Blinn-Phong), no specular highlights, no normal map,
only the diffuse atlas. Lit (physically based), Simple Lit with a normal map and Simple Lit without one were rendered
side by side through MCP at the Mobile quality level, with the same 40-enemy crowd, from a closer angle than the game
camera; the three were indistinguishable. An enemy covers about 100 x 150 pixels on screen, so the contribution of the
normal map and PBR lighting is not visible at that size. The cheapest option was chosen; a texture read and the
tangent-space normal computation per pixel are gone as well. For comparison `M_Enemy` (Lit) and `M_Enemy_SimpleLit`
(with a normal map) stay in `Assets/Optimized/Enemy`. Result on the phone: GPU time 13.2 -> 11.7 ms
(`PERFORMANCE.md` > Step 7).

## Hit feedback (`Unity/Views`, `Unity/Effects`)

The case says "the player's movement, attacks and damage feedback should be clear". Damage to the player was already
visible with the red screen flash; this section makes the attack side clear.

| Feedback | How | Triggered by |
|------|------|------|
| **Hit enemy flashes** (0.1 s) | `EnemyView` reads the enemy's health every frame; if it is lower than in the previous frame, `_BaseColor` is multiplied by a bright colour through a `MaterialPropertyBlock`, and the block is cleared when the time is up | Health drop; `PlayDeath` for a killing hit |
| **Muzzle flash** (0.05 s) | A single-particle system (camera-facing) at the rifle's muzzle, turned on for every shot | `Weapon.Fired` -> `PlayerView.OnFired` |
| **Impact spark** (~0.3 s) | A 16-particle orange burst, the `HitSpark` prefab | `ProjectileSystem.Hit` -> `ImpactEffects.Play` |

Details and reasons:
- **Reading health instead of an event.** The enemy flash checks whether health dropped instead of subscribing to
  `Health.Damaged`. Pooled views are rebound to different enemies, so the subscriptions would have to be released
  correctly every time; reading removes that risk of bugs entirely.
- **Killing hit.** The enemy leaves the simulation in the same frame, so that hit never reaches `Sync`; the flash is
  started in `PlayDeath`. Corpses are no longer synced, so `EnemyDeathViews.Tick` advances their flash.
- **SRP Batcher.** A renderer with a `MaterialPropertyBlock` drops out of the SRP Batcher's batched drawing. The block
  only stays for the flash (0.1 s) and is then cleared with `SetPropertyBlock(null)`.
- **Pooled particles.** `ImpactEffects` (plain C#) keeps the sparks in an `ObjectPool<ParticleSystem>`, creates 16 of
  them at startup and returns the expired ones; no `Instantiate`/`Destroy` during play. All of them are cleared when a
  new run starts.
- **Material.** The spark and the muzzle flash use `M_AdditiveParticle`: URP Particles/Unlit, additive blend (adds up
  like light), a 64 x 64 soft dot texture. Additive was set up correctly with URP's `BaseShaderGUI.SetupMaterialBlendMode`
  again (see the blob shadow note).

**Verified through MCP** in play mode: in a 900-frame run on Normal, 32 shots and 31 hits were counted; every shot
showed the muzzle flash, and every hit a spark and a flashing enemy. The moments of a hit and a shot were captured with
close-up renders. In the first render the sparks were barely visible from the game distance; their size (0.2-0.4 m)
and count (16) were increased.

## Camera shake (`Core/Presentation/CameraShake`, `Unity/Cameras/FollowCamera`)

The camera shakes when the player is hit and when the player dies; together with the red screen flash it makes damage
"felt".

**Model: trauma.**
- Events give the camera a **trauma** between 0 and 1; trauma decays linearly by `shakeDecayPerSecond` per second.
- The shake strength is proportional to **trauma²**: small hits stay subtle, big ones are clear, and the decay looks
  natural (in the test a quarter of the trauma gives a sixteenth of the shake).
- The direction comes from **Perlin noise**, not from random numbers (two separate noise channels, X and Z); the
  camera sways smoothly instead of jittering.
- On top of the position offset it produces a small **roll** around the view axis (a third noise channel, at most
  `shakeMaxRollDegrees`). The camera is ~23 m from the arena, so a shift of a few centimetres can go unnoticed; a slight
  rotation of the whole image is noticed right away. Added after the feedback "the camera does not shake at all" in the Editor.
- `AddTrauma` accumulates (capped at 1); `RaiseTo` raises trauma to at least the given level but does not accumulate.

**Why `RaiseTo`.** In a crowd several enemies hit the player in turn, so several damage events arrive per second. If
trauma were added on every hit, the camera would shake at full strength all the time while the player is surrounded.
Damage raises trauma to 0.6, death to 1.

**The shake does not mix into the smoothing.** `FollowCamera` keeps the smoothed follow position separately and adds
the shake offset and roll at the very end (the roll is applied on top of the fixed view angle). Otherwise the follow
smoothing (SmoothDamp) would smooth the shake away too.

**Settings** (`Camera Settings` on the bootstrap): max offset 1.6 m, max roll 3°, decay 1.2/s, frequency 8, hit 0.6,
death 1.

**Tuning (measured through MCP).** The player was damaged in play mode and the camera's deviation from its rest
position was measured frame by frame (with this camera 1 m of ground is about 54 pixels of the 1080-pixel screen):

| Attempt | Single hit | Death | Problem |
|------|------|------|------|
| Frequency 22 (first) | - | - | Caught by a test: jumps of up to 63% of the max offset per frame, jitter instead of smooth motion |
| Offset 0.45 m, hit 0.35 | 2 cm, 0.1 s | - | Invisible on the phone |
| Offset 1 m, hit 0.45 | 7 cm (~4 px), 0.15 s | 45 cm | Still weak; in practice Perlin stays at ~1/3 of its theoretical bound |
| **Offset 1.6 m, hit 0.6, decay 1.2** | **22 cm (~12 px), 0.37 s** | **92 cm (~50 px), 0.65 s** | At most 33 cm over 5 hits in a row; does not accumulate |

In the benchmark mode the player takes no damage, so there is no shake; it does not affect the measurements.

**Firing recoil (`Core/Presentation/CameraRecoil`).** **Off by default** (`recoilDistance` = 0); it was decided to turn
it off after a game feel test. To turn it on, set `recoilDistance` to 0.15. When on, every shot pushes the camera
instantly by `recoilDistance` (0.15 m, ~8 px on screen) away from the target, and it returns exponentially
(`recoilReturnSharpness` 20: 95% back in 0.15 s; the fire interval is 0.35 s, so the camera settles before the next shot).
- **Why not shake (trauma):** the weapon fires ~3 times per second. With random shake the camera would jitter all the
  time and the damage shake would get lost in it. Recoil is directional and predictable; it gives a sense of "the
  weapon's weight", and the damage shake stays separate and recognizable.
- A new shot does not add to the old recoil, it replaces it; with fast fire the camera never moves further than one recoil.
- The return is computed with `e^(-sharpness * dt)`; it gives the same result at 30 and 120 FPS (tested).
- `GameBootstrap` finds the direction on the `Weapon.Fired` event with `CurrentTarget.Position - Player.Position`, adds
  the recoil offset to the shake offset and passes it to `FollowCamera`.
- Verified in play mode (MCP): over three shots the recoil was 0.150 m, exactly opposite to the target (dot product
  -1.00), and 0.0075 m after 0.15 s.

Tests: `CameraRecoilTests` (push projected onto the ground and opposite to the shot, recoils not accumulating, return
to rest before the next shot, frame rate independence, ignoring a zero direction, reset, invalid values).

Tests: `CameraShakeTests` (no offset without trauma, accumulation and cap at 1, `RaiseTo` not accumulating, linear
decay, the offset staying on the ground and within the trauma² bound, dependence on trauma², smoothness between frames,
roll staying within the trauma² bound and decaying with trauma, zero roll when not configured, reset).
