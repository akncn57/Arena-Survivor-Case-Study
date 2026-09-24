# AI Work Log

**English** | [Türkçe](AI_WORKLOG.tr.md)

The project was developed from start to finish with Claude Code, connected to the Unity Editor through **MCP for Unity**
(CoplayDev). For asset analysis and optimization the AI drove Blender through headless scripts.

**Way of working** (rules written in `CLAUDE.md`):
- One system at a time: the AI writes the system and explains it, and does not move on to the next one without approval.
- Every Core system comes with EditMode tests; every system is described in `Docs/TECH.md`.
- Decisions (camera angle, landscape orientation, Mixamo animations, our own joystick, JSON save, no Git LFS,
  performance target) were written into `CLAUDE.md` at the start of the project; the AI worked by them in every session.
- Everything done in the Editor was **verified through MCP by measuring or rendering**; "it should work" was not
  accepted. Results on the phone were always measured on the real device.

## Key decision 1: game logic independent from Unity, in plain C# and tested

**Options.** (a) The classic Unity approach: a MonoBehaviour on every enemy, Update, colliders and physics.
(b) Game logic in plain C# classes, MonoBehaviours only as a thin layer for drawing and input.
(c) (b) + a DI container such as VContainer.

**Decision: (b).** A container was considered and rejected: at this size manual wiring is shorter, adds no package
dependency and keeps every connection visible in one file.

**Why.**
- One loop instead of an `Update()` for each of 150 enemies; the right structure for a mobile CPU from the start.
- No physics engine: a bullet hit is tested against the path the bullet travelled in that frame; the results are
  deterministic and can be verified in tests.
- The whole game can be simulated without play mode: a 3-minute run plays in milliseconds in a test.

**Outcome.** 278 EditMode tests. The tests caught real bugs: a float rounding error in the benchmark's percentile
(`0.99f * 100` came out slightly above 99), and a camera shake that jittered instead of moving smoothly (jumps of 63% of
the offset per frame). The decision paid off again with the endless mode added last: in a cloud session that could not
reach the Unity MCP server, the Core systems and tests were compiled and run without Unity, and the difficulty was tuned
by simulating Core with a bot.

## Key decision 2: measure first, then rebuild the enemy asset

**Situation.** The reference build ran at 14.6 FPS on the phone. The first reflex could have been code optimization;
the benchmark instead showed that the frame time equalled the GPU time (~69 ms) while the CPU used only ~17 ms. The game
was **GPU-bound**; making the CPU faster would have gained nothing.

**Decision.** First an in-game benchmark mode that guarantees identical conditions was written (fixed seed,
invulnerable and stationary player, 150 enemies, JSON + logcat output). Then the biggest GPU cost was targeted: an enemy
with 36,902 triangles, 65 bones and eight 4096² textures, times 150. The AI wrote Blender scripts that first analyze the
model and then produce the optimized version: two LODs (4,500 / 1,500 triangles), finger bones removed (65 -> 22, with
their weights moved to the hand), 4 bone influences per vertex, two materials merged into one atlas. The original files
were never changed; the reference build remained an honest comparison.

**Outcome.** 14.6 -> 49.4 FPS in one step. 83.8 FPS (x5.7) after seven measured steps in total. Every step was measured
on the same device with the same benchmark before moving on (`Docs/PERFORMANCE.md`). A rejected alternative: turning
shadows off only on the far LOD. It was tried; the shadows disappeared abruptly across the upper half of the screen, so
all enemies got blob shadows instead.

**Bugs caught.** Clearing the material list in Blender also reset the faces' material indices and the mesh fell back to
a single submesh (noticed by reading the submesh count in Unity). A texture that was not rescaled was saved empty
because Blender loads pixels lazily. Both were caught by checking the output in Unity.

## Key decision 3: a position correction, not a force, for the enemy crowd

**Problem.** Enemies turned into one pile on top of the player; they could not be told apart and could all hit at once.

**First attempt (rejected).** Pushing enemies that get too close apart with a force. Measured in play mode through MCP,
the rear ranks squeezed the front ranks: the crowd collapsed into a disc of ~4 m, the closest pair was 0.32 m apart,
and 36 enemies were within 1.5 m of the player.

**Decision.** Every overlapping pair is moved apart by half of the overlap (a position correction); neighbours come
from a spatial grid (~1,600 distance checks instead of 22,350 for 150 enemies). The radius and stiffness were chosen by
measuring the average neighbour distance, crowd radius, number of enemies attacking at once and tick time in a
150-enemy simulation through MCP (1.2 m chosen, ~6 enemies attack at once).

**A problem found later.** The crowd was seen jittering on the phone. Measured, every enemy moved back and forth by
~18 cm per frame: on a long 30 FPS frame the walking step was larger than what separation corrected in one step. The
simulation was split into 1/60 s substeps and the stiffness lowered to 0.5; the jitter was gone.

## End-to-end task through Unity MCP: enemy animation optimization

The clearest example of the "read editor state -> act -> verify" flow the case asks for:

1. **Read.** In the benchmark scene (150 enemies) the Animator update time, the Transform count and the frame time were
   measured through MCP: Animator 2.33 ms, 3,978 Transforms, frame 3.76 ms.
2. **Act.** **Optimize Game Objects** was turned on for the enemy model (the bones stop being GameObjects). A tool was
   written that does the Humanoid retargeting once in the Editor instead of every frame (`GenericClipBaker`): the
   Humanoid clip is sampled at 30 FPS and every bone's local transform is recorded into a Generic clip. The Animator
   controller was rebuilt with these clips, and **Cull Completely** was turned on for off-screen enemies.
3. **Verify.** The same measurement was repeated: 459 Transforms, Animator 1.07 ms (-54%), frame 3.28 ms. Side-by-side
   renders at two different moments confirmed that the poses match the Humanoid playback. Benchmark on the phone: CPU
   main thread 9.9 -> 8.4 ms, 4-5 MB less memory.

Other work done through MCP: building the scene, the UI and the Animator controllers, choosing the camera framing by
rendering, computing the rifle's position on the hand bone from the animation pose, comparing material variants with
side-by-side renders, and tuning game feel (camera shake, impact sparks) by measuring frame by frame.

## Where the AI was wrong and got corrected

- **The health bar was always full.** To fix the fill's squared-off look the AI had removed its sprite; uGUI silently
  ignores `fillAmount` on an Image without a sprite. The check through MCP only read the `fillAmount` value and did not
  see the bug; **it was noticed while playing on the phone**. The fix was verified by measuring the fill's actual width
  and rendering the HUD. Lesson: reading a value is not seeing the result.
- **A test that measured nothing.** The first "only neighbours are checked" test placed enemies exactly 2 m apart;
  none of them fell into a neighbouring cell, so the check count was 0 and the test passed for nothing. Fixed with a
  random crowd and a requirement of at least one check.
- **An invisible transparent material.** Making a URP material transparent by assigning its properties one by one was
  not enough; the quad was not drawn. The problem was first narrowed down to the material by rendering with an opaque
  red material, then the material was set up with URP's own `BaseShaderGUI.SetupMaterialBlendMode`.
- **A weak camera shake.** With the first values a hit was invisible on the phone (2 cm). The camera deviation was
  measured frame by frame and tuned in three rounds; a roll was also added.
- **A mislabelled measurement.** A benchmark JSON file had been mixed up with the previous step's result; it was
  noticed from the version and date fields inside the file and deleted.

## Late addition: endless mode

When the case was ready to deliver, a separate endless mode was added (XP and health drops, a level bar, picking one of
3 cards, enemies growing stronger over time). This was done in a Claude Code session running in the cloud, which could
not reach the Unity MCP server on the developer's machine. Therefore:
- The Core systems and tests were compiled and run in .NET with a small stand-in for the UnityEngine math types (all
  278 tests passed); the Unity and Editor code was type-checked against real Unity reference DLLs.
- The difficulty was tuned by simulating Core with a bot that runs around the arena and picks random cards.
- Instead of building the scene by hand or by editing YAML, an editor command that does it in one click was written
  (**Tools > Arena Survivor > Setup Endless Mode**); the new UI is built by duplicating existing, styled objects.
- One mistake got through: while cleaning up a line ending, the closing brace of the editor assembly definition was
  deleted as well. The .NET checks did not read `.asmdef` files, so Unity reported it after the merge; it was fixed
  with a one-line commit.
