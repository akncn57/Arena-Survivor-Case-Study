# Performance

Measurements of the reference build (before optimization) and, later, the optimized build,
taken under the same conditions with the in-game benchmark (see `TECH.md` > Benchmark mode).

## Test conditions

| | |
|------|------|
| Device | Xiaomi Redmi Note 14 Pro (4G, model 24116RACCG) |
| SoC / GPU | MediaTek Helio G100-Ultra (MT6789) / Mali-G57 MC2 |
| RAM / OS | 8 GB / Android 16 (HyperOS 3) |
| Screen | 2400 x 1080, landscape |
| Graphics API | Vulkan |
| Build | Release (not development), IL2CPP, ARM64, URP "Mobile" quality level |
| Scenario | Benchmark button: seed 12345, 150 enemy cap, invulnerable idle player, 10 s warm-up + 60 s measured, 120 FPS cap |
| Procedure | Phone on charger, screen on, app freshly started, benchmark run twice in a row |

Raw results: `Benchmarks/reference_run1.json`, `Benchmarks/reference_run2.json`, screenshot `Benchmarks/reference_run2.jpg`.

## Reference build (`v1.0-reference`)

Original models, textures and Humanoid Animators, no optimization.

| Metric | Run 1 | Run 2 |
|------|------|------|
| Average FPS | 14.5 | 14.6 |
| 1% low FPS | 10.7 | 13.1 |
| Frame time avg / p99 / max (ms) | 69.0 / 93.4 / 161.3 | 68.6 / 76.4 / 169.8 |
| **GPU time per frame (ms)** | **69.0** | **68.6** |
| CPU main thread per frame (ms) | 17.4 | 17.4 |
| Enemies alive (avg of 150 cap) | 149.8 | 149.7 |
| Kills | 51 | 53 |
| Allocated memory | 119 MB | 119 MB |

The two runs agree within about 1% on average FPS, GPU and CPU time. Spawn positions are identical (fixed
seed); kill counts differ slightly because the simulation advances with the real frame time, so bullet hits
land a few milliseconds apart between runs. The load itself (enemies alive) is the same.

### Analysis

**The game is GPU-bound.** The frame time equals the GPU time (about 69 ms), while the CPU main thread needs
only about 17 ms. Speeding up the CPU would not raise the frame rate until the GPU cost comes down.

Likely GPU costs, in order of expected impact:

1. **Geometry.** 150 enemies x 36,902 triangles is about 5.5 million triangles per frame, drawn a second time
   for the shadow map. Far beyond what a Mali-G57 MC2 handles at 60 FPS.
2. **Skinning.** Every enemy deforms an 18,453-vertex mesh with 65 bones each frame. The work scales with the
   vertex count, so a lower-poly mesh cuts it proportionally; fewer bones help further.
3. **Textures.** Eight 4096 x 4096 textures (diffuse, normal, specular, glossiness for two materials) cost memory
   bandwidth, which is scarce on mobile GPUs. A single 1024 (or smaller) atlas with ASTC compression is enough
   at the size enemies appear on screen.
4. **Shadows and materials.** Shadow casting for 150 skinned meshes and two materials (two draw calls) per enemy.

On the CPU side (the next bottleneck once the GPU cost drops): 150 Humanoid Animators (retargeting is more
expensive than Generic), animation of 65 bones each, and enemy logic.

### Optimization plan (prioritized by expected impact)

| # | Change | Targets |
|------|------|------|
| 1 | Enemy mesh decimated to about 4-5k triangles (LOD0) plus a lower LOD, created in Blender as new files | GPU geometry, skinning |
| 2 | Enemy textures merged and reduced to one 1024 (or 512) atlas, ASTC compressed, one material | GPU bandwidth, draw calls, memory |
| 3 | Shadow settings for enemies (cheaper or no shadow casting at distance) | GPU shadow pass |
| 4 | Animator: fewer bones (strip fingers), cheaper culling mode, possibly Generic clips | CPU animation |
| 5 | Player model: moderate decimation, merged materials; rifle textures to 512 | GPU, memory |

Each change is measured again with the same benchmark on the same device before moving to the next.

## Step 1: optimized enemy (`v1.1.0`, commit `749ca37`)

Change: the enemy prefab uses `Enemy_Optimized` (4,500 / 1,500 tri LODs, 22 bones, max 4 weights, one
material, 1024 x 512 ASTC atlas). Details in `TECH.md` > Optimized assets. Everything else unchanged.

| Metric | Reference (run 2) | Step 1 run 1 | Step 1 run 2 | Change |
|------|------|------|------|------|
| Average FPS | 14.6 | 49.3 | 49.4 | **x3.4** |
| 1% low FPS | 13.1 | 29.5 | 39.2 | x3.0 |
| Frame time avg / p99 (ms) | 68.6 / 76.4 | 20.3 / 33.9 | 20.2 / 25.5 | -71% |
| **GPU time (ms)** | **68.6** | 19.9 | **19.9** | **-71%** |
| CPU main thread (ms) | 17.4 | 12.4 | 12.2 | -30% |
| Allocated memory | 119 MB | 112 MB | 112 MB | -6% |
| APK size | 50.2 MB | 41.8 MB | | -17% |

Raw results: `Benchmarks/step1_enemy_run1.json`, `Benchmarks/step1_enemy_run2.json`, `Benchmarks/step1_enemy_run2.jpg`.

**Reading:** the enemy asset was the dominant GPU cost, as the reference analysis predicted. The CPU also got
faster: 43 fewer bones per enemy to animate and fewer weights to skin. The frame time (20.2 ms) still equals the
GPU time, so the game is still GPU-bound and about 3-4 ms short of 60 FPS (16.7 ms). Next: render settings
(shadows, resolution, post-processing).

## Step 2: mobile render settings (`v1.1.1`, commit `7cafdc1`)

Change (Mobile quality level only): post-processing off, HDR off, shadow distance 35 m, enemies use a blob shadow
instead of casting real-time shadows. Details in `TECH.md` > Render settings.

| Metric | Reference | Step 1 | Step 2 run 1 | Step 2 run 2 |
|------|------|------|------|------|
| Average FPS | 14.6 | 49.4 | 78.3 | **78.3** |
| 1% low FPS | 13.1 | 39.2 | 58.7 | **58.7** |
| Frame time avg / p99 / max (ms) | 68.6 / 76.4 / 169.8 | 20.2 / 25.5 / 42.5 | 12.8 / 17.0 / 34.0 | **12.8 / 17.0 / 25.5** |
| GPU time (ms) | 68.6 | 19.9 | 12.5 | **12.6** |
| CPU main thread (ms) | 17.4 | 12.2 | 10.3 | **10.4** |
| Allocated memory | 119 MB | 112 MB | 112 MB | 112 MB |

Raw results: `Benchmarks/step2_render_run1.json`, `Benchmarks/step2_render_run2.json`, `Benchmarks/step2_render_run2.jpg`.

**Reading:** removing post-processing, HDR and 150 shadow-casting skinned meshes cut another 7 ms of GPU time.
The 60 FPS target is met: the average frame takes 12.8 ms of the 16.7 ms budget, and the 1% low is at 58.7 FPS
with the benchmark's 120 FPS cap (normal play is capped at 60). CPU (10.4 ms) and GPU (12.6 ms) are now close,
so there is no single dominant bottleneck left. Overall: **5.4x the reference frame rate**.
