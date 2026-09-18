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
