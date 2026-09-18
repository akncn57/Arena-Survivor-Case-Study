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

No dependency injection framework is used. A single composition root in the scene will create the
systems and wire them together by hand (added with the gameplay systems).

### Assemblies

| Assembly | Folder | Contents | References |
|----------|--------|----------|------------|
| `ArenaSurvivor.Core` | `Assets/Scripts/Core` | Game logic, plain C#, no MonoBehaviours | UnityEngine only (math types, JsonUtility) |
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
