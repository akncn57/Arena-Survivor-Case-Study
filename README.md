# Arena Survivor

**English** | [Türkçe](README.tr.md)

A Survivor.io-style mobile game built for a Unity Developer case study. The player moves around a single arena with a
virtual joystick, the rifle automatically fires at the nearest enemy in range, and the goal is to survive 3 minutes
against zombies coming in waves.

- **Unity** 6000.3.16f1, URP 17.3, Input System 1.19, Android (IL2CPP, ARM64)
- **APK:** _(link to be added)_
- **Video:** _(link to be added)_

## Case requirements

| Requirement | How it is met |
|------|------|
| Single arena, joystick movement | 40 x 40 arena; our own floating joystick that appears where the screen is touched (`VirtualJoystick` + `JoystickModel`) |
| Auto-attack enemies in range | The rifle targets the nearest enemy; bullets are simulated and hits are tested along their path (fast bullets do not pass through enemies) |
| Waves, chasing, damage | `WaveSpawner` spawns waves on an off-screen ring around the player; `EnemySystem` updates all enemies in one loop |
| Survive 3 minutes = win, die = lose | `GameSession` timer; winning and losing happen exactly once |
| Result screen: kills + replay | "YOU SURVIVED" / "YOU DIED", kills, time, lifetime kills, Play Again and Menu |
| Lifetime kills persist across restarts | `persistentDataPath/save.json`, `JsonUtility`, atomic write (temp file + replace), defaults on a corrupt file |
| 3 difficulties in the same scene | `DifficultySettings` ScriptableObjects: enemy count and spawn rate (below) |
| Use the provided models | Player, enemy and rifle come from the provided models; the originals in `Assets/Models` were never changed, optimized versions are separate files |
| One end-to-end task through Unity MCP | The scene, UI, Animator controllers and an optimization step were done and verified through MCP; see the [AI work log](Docs/AI_WORKLOG.md) |
| Reference build, profile, optimize, re-measure | In-game benchmark mode, same device and same conditions; `v1.0-reference` and `v1.1-optimized` tags |

## Gameplay

| Mode | Description |
|------|------|
| **Easy / Normal / Hard** | The 3-minute run the case asks for. The difficulties only differ in spawn rate, wave size and the cap on enemies alive at once. |
| **Endless** | An extra mode added after the case scope. No timer, it lasts until death. Enemies drop XP and health; on a level up the game pauses and the player picks one of 3 upgrade cards (damage, attack speed, multishot, range, health, speed, magnet). Enemies come in larger numbers and grow tougher and faster over time. The best time and level are saved. |
| **Benchmark** | A repeatable performance test with a fixed seed and 150 enemies (below). |

| Difficulty | Spawn interval (s) | Wave size | Max enemies alive |
|------|------|------|------|
| Easy | 3.0 -> 1.5 | 2 -> 6 | 40 |
| Normal | 2.5 -> 1.0 | 3 -> 10 | 80 |
| Hard | 2.0 -> 0.6 | 4 -> 16 | 150 |

The values ramp linearly from start to end over the run.

**Controls:** on the phone, touch anywhere on the screen and drag to move; in the Editor WASD / arrow keys or a gamepad.
The game runs in landscape.

## Opening the project and building

1. Open the project with Unity **6000.3.16f1** (Git LFS is not needed).
2. Open the `Assets/Scenes/Arena.unity` scene and press Play.
3. Android build: **File > Build Profiles > Android**, the `Arena` scene is in the scene list; **Build**. The player
   settings (IL2CPP, ARM64, LandscapeLeft, Frame Timing Stats) are saved in the project.

Tests: **Window > General > Test Runner > EditMode > Run All** (278 EditMode tests covering all Core systems).

## Reference and optimized versions

| Tag | Contents |
|------|------|
| `v1.0-reference` | First working build: original models and textures, no optimization |
| `v1.1-optimized` | Optimized assets, mobile render settings, animation optimization |

```
git checkout v1.0-reference   # reference build
git checkout v1.1-optimized   # optimized build
```

The endless mode was added after `v1.1-optimized`; it does not change the benchmark scenario.

### Performance (Xiaomi Redmi Note 14 Pro, Mali-G57 MC2, 150 enemies)

| | Reference | Optimized | Change |
|------|------|------|------|
| Average FPS | 14.6 | **83.8** | **x5.7** |
| GPU time | 68.6 ms | 11.7 ms | -83% |
| CPU main thread | 17.4 ms | 8.2 ms | -53% |
| Allocated memory | 119 MB | 107 MB | -10% |
| APK size | 50.2 MB | ~42 MB | -16% |

The game is capped at 60 FPS; the optimized build uses about 70% of that budget. Every step was measured separately:
[Docs/PERFORMANCE.md](Docs/PERFORMANCE.md) (raw JSON results in `Docs/Benchmarks`).

**How it was measured:** the BENCHMARK button in the menu starts a 150-enemy run with a fixed seed; the player takes no
damage and does not move, and after a 10 s warm-up 60 s are measured. Average FPS, 1% low, frame times and CPU/GPU times
from `FrameTimingManager` are shown on screen, saved as JSON and written to logcat. Release build, no Profiler
connection needed.

### Asset optimization: quality / performance trade-offs

The original models stay as they are in `Assets/Models`. The optimized versions were generated into `Assets/Optimized`
by Blender scripts (`Tools/Blender`, headless, reproducible).

| Asset | Original | Optimized | Gain | Cost |
|------|------|------|------|------|
| Enemy | 36,902 triangles, 65 bones, 2 materials, 8 x 4096² textures | LOD0 4,500 / LOD1 1,500 triangles, 22 bones, 1 atlas material (1024 x 512, ASTC) | The GPU's main load; 14.6 -> 49.4 FPS on its own | Fingers are not animated, less detail up close. From the game camera (an enemy is ~100 x 150 px) LOD0 and LOD1 are indistinguishable, checked with a render comparison. |
| Enemy material | URP Lit + normal map | URP Simple Lit, no normal map | GPU 13.2 -> 11.7 ms | No specular highlight; the three variants were rendered in the same scene and compared, no visible difference at this size. |
| Enemy shadow | Real-time | Blob shadow (soft round quad) | No second draw of every enemy | The shadow does not follow the body's shape. |
| Enemy animation | Humanoid, bones as GameObjects | Baked Generic clips, Optimize Game Objects, Cull Completely | Animator time -54%, CPU 9.9 -> 8.4 ms | The baking tool has to be run again when a clip changes. |
| Player | 19,450 triangles, 2 meshes, 3 materials | 7,999 triangles, 1 mesh, 2 materials, 512² textures | Memory and GPU | Finger bones deliberately kept (single character, the hand holding the rifle is visible). |
| Rifle | 3 x 2048² textures | 3 x 512² (ASTC) | Memory | The rifle is 30-40 px on screen; no visible difference. |

Details and the reason behind each decision: [Docs/TECH.md](Docs/TECH.md) > Optimized assets.

## Architecture (in short)

- **Game logic in plain C#** (`Assets/Scripts/Core`, the `ArenaSurvivor.Core` assembly): no MonoBehaviours, not tied
  to Unity's clock. The whole game can be simulated in an EditMode test, without play mode.
- **Thin Unity layer** (`Assets/Scripts/Unity`): input, views, camera, UI. Only `GameBootstrap` has an `Update()`;
  the order of everything that happens in a frame is visible in one method.
- **One system, one loop:** not 150 `Update()`s for 150 enemies, but a single loop in `EnemySystem.Tick`. A spatial
  grid for enemy separation (O(n) instead of O(n²)).
- **Pooling:** enemies, bullets, pickups, view GameObjects and particles; no `Instantiate`/`Destroy` during play, no
  per-frame garbage.
- **Data-driven tuning:** player, enemy, weapon, difficulties and endless in ScriptableObjects (`Assets/Data`).
- No DI framework; dependencies go through constructors and are wired by hand in a single composition root.

```
Assets/
  Models/        provided original models (untouched)
  Optimized/     optimized models, textures, materials, baked clips
  Scripts/Core   game logic (plain C#)
  Scripts/Unity  Unity layer
  Scripts/Editor editor tools (clip baking, endless setup)
  Tests/EditMode Core tests
  Data/          ScriptableObject settings
Tools/Blender/   asset analysis and optimization scripts
Docs/            technical documentation, performance, AI work log
```

## Documentation

| File | Contents |
|------|------|
| [Docs/TECH.md](Docs/TECH.md) | How every system works and why it was built that way |
| [Docs/PERFORMANCE.md](Docs/PERFORMANCE.md) | Reference and optimized measurements, step by step |
| [Docs/AI_WORKLOG.md](Docs/AI_WORKLOG.md) | Working with AI: 3 key decisions, the MCP task, where the AI was wrong |

Every document also has a Turkish version (`*.tr.md`).

## Known limitations

- The arena is a placeholder: a flat ground and low walls.
- In the Rifle Run clip the left hand leaves the weapon (a limitation of the clip itself).
- No audio.
- No separate device measurement was made for the endless mode; the enemy count is kept below the benchmark's (at most 100).
