# Arena Survivor: Case Study

Survivor.io-style mobile game built for the Skyloft Studios Unity Developer case study
(also a portfolio piece). Deadline: 2026-09-25. All code, docs and commits in English.

## Case requirements (summary)
- Single arena, virtual joystick movement, auto-attack enemies in range (rifle fires projectiles).
- Enemies spawn in waves, chase and damage the player. Survive 3 minutes to win; die to lose.
- Result screen: kill count + replay. Total kills persist across app restarts.
- 3 difficulty levels tuned by enemy count and spawn rate (same scene).
- Must use the provided player/enemy/rifle models (no replacement with primitives).
  Keep originals untouched; create optimized versions separately; explain quality/perf trade-offs.
- Complete at least one meaningful task end-to-end through Unity MCP (read editor state -> act -> verify).
- Save first working build as the reference (git tag), then profile, optimize and re-measure under the same conditions.
- Deliverables: Git repo (reference vs optimized distinguishable by tag), Android APK, README,
  AI work log (3 key decisions), 3-5 min video.

## Environment
- Unity 6000.3.16f1, URP 17.3, new Input System 1.19, Android build support.
- MCP for Unity (CoplayDev, v10.2.0) over HTTP at http://127.0.0.1:8080/mcp (see `.mcp.json`).
  Start the server from Window > MCP for Unity > Start Server before using MCP tools.
- Blender 5.2 at `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe` (headless analysis/decimation).
- Plain Git, no LFS (originals never change; keeps clones working without LFS).

## Decisions
- Camera: slightly tilted isometric-style follow camera (not straight top-down).
- Animation: classic Animator with Mixamo clips (both characters already use Mixamo rigs; download "Without Skin", Humanoid).
- Custom on-screen joystick (no third-party asset).
- Save system: JSON file in `Application.persistentDataPath` via `JsonUtility`, atomic write (temp file + replace),
  `version` field, defaults on missing/corrupt file. Extensible `SaveData` class.
- Data-driven tuning: `EnemyDefinition` and `DifficultySettings` ScriptableObjects. One enemy type for now.
- Object pooling for enemies, projectiles and VFX.
- Performance target: stable 60 FPS on mid-range Android; Low/Medium/High URP quality tiers for weaker devices.
- Scope: required mechanics first; level-up/upgrades only if time remains.

## Code conventions
- Game logic in plain C# classes (`ArenaSurvivor.Core` assembly). MonoBehaviours only as a thin Unity-facing layer
  (input, transforms, animation, UI, composition root). No DI framework; one composition root wires systems by hand.
- Central systems update many entities in one loop (e.g. one enemy system, not one Update per enemy).
- Every Core system gets EditMode NUnit tests (`Assets/Tests/EditMode`, mirroring source folders). Use fakes via interfaces.
- Keep `Docs/TECH.md` updated whenever a system is added or changed; the user must be able to understand every system.
- Build one system at a time, explain it, get the user's OK before the next.

## Source asset analysis (before optimization)
| Asset  | Tris   | Verts  | Materials | Bones | Textures |
|--------|--------|--------|-----------|-------|----------|
| enemy  | 36,902 | 18,453 | 2         | 65    | 8 x 4096^2 (Diffuse/Normal/Glossiness/Specular x2), embedded in the 100 MB FBX |
| player | 19,450 | 10,219 | 3 slots (2 meshes) | 69 | 1024^2 diffuse, 512^2 normal/specular (head + body) |
| rifle  | 1,988  | 1,055  | 1         | -     | 3 x 2048^2 (Albedo/MetallicSmoothness/Normal) |
No animation clips in any model (T-pose only).

Optimization plan: enemy LOD0 ~4-5k tris + LOD1 ~1.5k, strip finger bones (~25 bones), 2 skin weights,
merge to one atlas material, textures 512-1024 ASTC, convert spec/gloss to URP metallic/smoothness.
Player: merge materials, moderate decimation. Rifle: textures to 512.

## Layout
- `Assets/Models/` : original provided assets (do not modify).
- `Assets/Scripts/Core/` : plain C# game logic (`ArenaSurvivor.Core.asmdef`).
- `Assets/Tests/EditMode/` : EditMode tests (`ArenaSurvivor.Tests.EditMode.asmdef`).
- `Docs/TECH.md` : technical overview of all systems.
