# Task Checklist — Gemstone Colony

## Project setup
- [x] Unity 6 URP project scaffolded
- [x] Git initialized with remote (`origin`)
- [x] `CLAUDE.md` authored (SOLID/GRASP, MCP-only, performance, UI verification, doc-maintenance rules)
- [x] Standard `Assets/` folder structure created (Art, Audio, Prefabs, Scripts, UI)

## Art / Characters
- [x] First character voxel model imported (MagicaVoxel `character.obj`) → `Assets/Art/Models/Characters/Character/`
- [x] Standalone material created (`Assets/Art/Materials/Character.mat`, URP/Lit, double-sided) and assigned
- [x] Texture import settings fixed (Point filtering) for crisp voxel colors
- [x] Visually verified via MCP screenshots (front/back/left/right/top/bird's-eye)
- [ ] Rig/animate character (idle, walk, etc.) — not started
- [ ] Turn into a reusable prefab under `Assets/Prefabs/Characters/` — not started
- [ ] Additional character/creature models — not started

## Systems — not started
- [ ] Player controller
- [ ] Voxel world/terrain system — architecture explored and validated (see `Docs/GDD.md` → "Procedural Terrain — Voxel Planet Prototype"); phase 1 single-chunk fundamentals done, phase 2 full 6-face small-sphere puzzle-piece demo done (below), full streaming/multi-chunk production-scale planet system not started
- [ ] Camera system
- [ ] UI/HUD

## World scene prototype, phase 1 — single-chunk terrain demo (done, 2026-08-31)

Goal: a playable, visually-verifiable proof-of-concept of one 64×32×64 voxel terrain chunk with noise-driven mountains/lakes, viewed with a fly camera — the fundamentals (terrain gen, materials, camera), deliberately scoped without a sphere or the region graph. See `Docs/GDD.md` → "World scene prototype, phase 1 — build notes" for what was actually built and any deviations from the original spec.

- [x] New scene `Assets/Scenes/World.unity` (added to Build Settings alongside `SampleScene`)
- [x] Fly camera (`Assets/Scripts/Player/FlyCamera.cs`, first-person, not a character controller):
  - [x] WASD moves forward/back/strafe relative to look direction
  - [x] Mouse look while right mouse button is held (match Unity's Scene View navigation feel)
  - [x] Q/E or Space/Ctrl for vertical up/down movement
  - [x] Shift to move faster
  - [x] No physics/collision — free-fly debug camera, not gameplay movement
- [x] Terrain generation (`Assets/Scripts/Procedural/TerrainChunkVoxelData.cs` + `TerrainChunkMeshBuilder.cs` + `TerrainChunkGenerator.cs`), adapting the analytic-occupancy + `FaceTemplate` pattern from `VoxelSphereMeshBuilder.cs`:
  - [x] 64×32×64 voxel volume (Inspector-tunable on `TerrainChunkGenerator`)
  - [x] Per-column height from 2D Perlin noise → mountains are the noise-driven high columns
  - [x] Fixed water-level constant (Inspector-tunable); columns below it filled with water up to that level (lakes in low-noise basins)
  - [x] Ground and water tracked as distinct `TerrainVoxelType` values, built into two separate meshes
- [x] Two materials, both URP/Lit, flat per-face normals (from unshared per-face vertices in the mesh builder, same as `VoxelGray.mat`'s approach — no special shader needed):
  - [x] `Assets/Art/Materials/TerrainGround.mat` — solid green
  - [x] `Assets/Art/Materials/TerrainWater.mat` — solid blue
  - [x] No generated textures, per `CLAUDE.md` "No AI-generated art"
  - [x] Ground and water as two separate child objects (`GroundMesh`/`WaterMesh` under `TerrainChunk`), each with its own mesh — chosen over submeshes as the simpler option
- [x] Directional light (scene template default) lights the flat-shaded faces
- [x] Verification via Unity MCP:
  - [x] No compile errors, no console errors (edit mode or play mode)
  - [x] Screenshots confirm mountains, lake basins, consistent water level, and individually-readable voxel faces
  - [x] Entered Play mode — no runtime errors
- [x] Checklist and GDD updated

## World scene prototype, phase 2 — full 6-face small-sphere puzzle-piece demo (done, 2026-08-31)

Goal: prove the "puzzle piece" architecture for real — multiple square voxel chunks, generated **independently** (as if streamed, never merged into one mesh), covering all 6 cube faces of a small (32-voxel-radius) sphere, all sampling **one shared, world-aligned lattice**, with the hex/pentagon region graph used only to pick each chunk's biome parameters (never to place voxels). Visually verify (a) zero seams between independently-generated chunks — including at cube-face edges — and (b) visible biome variation following the region boundaries, and (c) the result actually reads as a connected round planet from a distance. See `Docs/GDD.md` → "World scene prototype, phase 2 — build notes" and `Docs/Systems/PlanetSphere.md` for the full architecture actually built (`Assets/Scripts/Procedural/Planet/`, plus shared pieces in `Assets/Scripts/Procedural/Planet/`).

An earlier single-cube-face 16-chunk "patch" version of this task proved within-face tiling but could never look like a planet (just a small tile floating in empty space), so it was scrapped in favor of the full 6-face version below.

- [x] Shared planet configuration (`SmallPlanetConfig.cs`, single static source of truth): base radius 32 voxels, shell thickness 8, sea level 31, region subdivision 1 — every chunk derives voxel positions from these same values via the same formula
- [x] Region graph (`PlanetRegionGraph.cs`, reuses `IcosphereBuilder` at subdivision 1 → 42 regions): adjacency from shared triangle edges; 3 biomes flood-filled from 3 farthest-point seed regions, large contiguous zones
- [x] 3 biomes (`SmallBiomeCatalog.cs`): Mountain (high amplitude, no water), Plains (moderate amplitude, no water), Basin (low base height, water filled to a fixed sea-level radius) — enough variety that boundaries are visually obvious
- [x] Generalized cube-face handling (`CubeFace.cs`): all 6 face normals/tangents/bitangents, plus `Classify(direction)` for deterministic face ownership at cube edges/corners
- [x] Chunk generator: 96 square voxel chunks (4×4 grid × 6 faces, ~16 voxels/edge — full `[-1,1]` per-face coverage, not a sub-patch), each its own GameObject/Mesh, generated in a loop (not hand-placed):
  - [x] Chunk placement follows `CubeSphereChunkMeshBuilder`'s per-face tangent/bitangent scheme, generalized via `CubeFace` to all 6 faces
  - [x] Each chunk independently resolves region → biome per cell and generates its own occupancy from the shared config + region lookup; no chunk reads another chunk's generated data (`SmallPlanetChunkVoxelData.cs` only ever calls the shared, stateless classifier)
- [x] Two materials — reused `TerrainGround.mat`/`TerrainWater.mat` from phase 1 directly, no new materials created
- [x] Reused the existing `World` scene's directional light; no new lights/cameras added
- [x] Verification via Unity MCP:
  - [x] No compile errors, no console errors/warnings in edit or play mode
  - [x] **Within-face boundary seams:** two chunk-pair scans, 0 gaps / 0 double-owned
  - [x] **Cross-face-edge seams:** exhaustive solid-cell scan along the full +X/+Y cube edge — 0 gaps / 0 double-owned after a fix (below); found 15 real gaps before it
  - [x] Wide screenshot confirms biome variation (mountain relief vs. flatter plains vs. basin lake) visibly follows region boundaries, not per-voxel noise
  - [x] Screenshots from 4 angles around the sphere confirm a complete, round, connected silhouette — reads as an actual small planet
  - [x] Play mode entered/exited cleanly, no runtime errors
  - [x] **Real bug found and fixed:** a region-lookup adjacency hill-climb shortcut could converge to the wrong region at this demo's coarse 42-region graph, causing two chunks to disagree on a shared boundary cell's biome/type. Fixed by removing the hill-climb entirely and calling the brute-force `PlanetRegionGraph.FindNearestRegion` directly everywhere — cheap at 42 regions, sidesteps the bug class entirely. Flagging this as a latent risk for any future work at much larger (production-scale) region counts, where a fast, *correct* lookup will actually be needed.
  - [x] Noted, not chased further: faint lines visible in screenshots align with the internal chunk grid on each face even after the gap fix — most likely a flat-shading/self-shadowing artifact between independently-lit chunk meshes, not a geometric crack (the numeric boundary checks are clean); worth a look if a fully seamless *visual* result (not just topological) is wanted later
- [x] Checklist, GDD, and `Docs/Systems/` updated with what was actually built, the region-lookup bug, and the known cosmetic artifact

## Follow-up — boundary-aware region-lookup optimization (done, 2026-08-31)

Goal: the all-brute-force region-lookup fix above is correct but pays the O(regionCount) cost for every cell, including deep-interior cells where it can't possibly cause a seam. Only brute-force cells actually near a chunk/face boundary; hill-climb everywhere else. See `Docs/GDD.md` → "Follow-up: boundary-aware region-lookup optimization" and `Docs/Systems/PlanetSphere.md` for the full story, including a second real bug found while tuning it.

- [x] Reintroduced the hill-climb as `PlanetRegionGraph.ResolveRegionNear` (shared file, no size dependency) — walks the region adjacency graph from a per-chunk hint, updated after every cell
- [x] Boundary-proximity test (`SmallPlanetChunkLayout.IsNearChunkBoundary` + `SmallPlanetConfig.RegionBoundaryMargin`): only cells within the margin of a chunk boundary use the guaranteed-correct `FindNearestRegion`
- [x] **Real bug found and fixed:** the first version measured boundary-proximity using the *asking* chunk's own face-relative (s, t), which is numerically unstable for cells that don't actually project well onto that face — exactly the cells near a cross-face boundary. Increasing the margin didn't fix it (proving it wasn't a margin-size issue). Fixed by always classifying proximity on the cell's own **native** face/chunk (`CubeFace.Classify` + `SmallPlanetChunkLayout.NativeChunkIndex`)
- [x] Verification via Unity MCP:
  - [x] No compile errors, no console errors/warnings; play mode entered/exited cleanly
  - [x] **Exhaustive scan, not a sample:** all 96 chunks, all 732 chunk pairs with any AABB overlap, checked against the real compiled `SmallPlanetChunkVoxelData` class — 0 type disagreements at the final margin (0.02)
  - [x] Measured split: 82% of cells use the cheap hill-climb, 18% pay for brute-force (down from 100% before this optimization); full 96-chunk regen now well under a second in-editor
  - [x] Visually re-confirmed: sphere still reads as a complete, connected planet from multiple angles
- [x] Checklist, GDD, and `Docs/Systems/` (`PlanetShared.md`, `PlanetSphere.md`) updated with what was actually built and the native-face bug

## Follow-up — real chunk-ownership gap found and fixed (done, 2026-08-31)

Goal: a real, visible gap was spotted directly in the Editor (not by any numeric check) — selecting a corner chunk showed solid terrain ending abruptly with nothing beyond it. This bug predates the region-lookup work above and is unrelated to it — it's in `SmallPlanetChunkLayout.OwnsCell`'s ownership logic, from this demo's original build. See `Docs/GDD.md` → "Follow-up: real chunk-ownership gap found and fixed" and `Docs/Systems/PlanetSphere.md` for the full story.

- [x] **Root cause identified:** `OwnsCell` used a raw half-open interval (`s >= s0 && s < s1`) — correct at internal grid lines, but a face's outer edge (`s` or `t` exactly `+1`) has no "next chunk" to catch it, so cells landing exactly there belonged to no chunk. Not rare: happens for any integer voxel coordinate where two axis magnitudes tie exactly (common near diagonals, guaranteed near corners)
- [x] **Fixed:** `OwnsCell` now uses `NativeChunkIndex` (the clamped-floor helper already written for the region-lookup optimization) instead of the raw range comparison
- [x] Verification via Unity MCP:
  - [x] No compile errors, no console errors/warnings
  - [x] The exact corner that showed 13/169 zero-owner directions before the fix now shows 0/169
  - [x] Broadened check: all 8 cube corners + all 12 cube edges (3,380 samples) — 100% exactly-one-owner, 0 zero, 0 double
  - [x] 5,000 uniformly-random directions across the whole sphere — 100% exactly-one-owner
  - [x] Regenerated the real scene; visually re-confirmed at the exact chunk/angle that showed the gap — solid, continuous terrain
- [x] Checklist, GDD, and `Docs/Systems/PlanetSphere.md` updated with what was found and fixed

## Follow-up — r_bedrock actually implemented (done, 2026-08-31)

Goal: the GDD's background section named `r_bedrock = planetRadius / chunkSurfaceWidth` as the fix for radial convergence toward the planet's core, but no code ever implemented it — the demo used a fixed *thickness* relative to local terrain height instead of a fixed *floor radius*. Planets should always have their crust reach down to r_bedrock (a real, absolute radius, the same for every direction); everything below it is deliberately left blank. See `Docs/GDD.md` → "Follow-up: r_bedrock actually implemented" and `Docs/Systems/PlanetSphere.md` → "The r_bedrock floor".

- [x] `SmallPlanetConfig.cs`: added `ChunkSurfaceWidthVoxels` (physical chunk width at the surface) and `BedrockRadiusVoxels = BaseRadiusVoxels / ChunkSurfaceWidthVoxels`; removed `ShellThicknessVoxels`
- [x] `SmallPlanetVoxelClassifier.cs`: crust now spans `[BedrockRadiusVoxels, outer]` for every direction — a fixed floor, not a fixed thickness relative to local terrain height; below the floor is unconditionally `Empty`
- [x] `SmallBiomeCatalog.cs`: removed `MinPossibleInnerRadius` (no longer needed — the floor is one shared constant, not per-biome)
- [x] `SmallPlanetChunkLayout.cs`: `GetChunkAabb`'s minimum radius now uses `BedrockRadiusVoxels` directly
- [x] Verification via Unity MCP:
  - [x] No compile errors, no console errors/warnings; play mode entered/exited cleanly
  - [x] Regenerated all 96 chunks against the real scene — ground triangle count dropped ~55k → ~32.7k (fewer inter-biome "cliff" faces now that the inner boundary is uniform, not stepped) — a real improvement, not a regression
  - [x] Re-ran type ground-truth (real class vs. 100% brute-force) across all 96 chunks — 0 divergence
  - [x] Re-ran mesh face-count vs. expected-exposed-faces per chunk — 0 mismatches
  - [x] Re-ran the ownership-completeness audit (8 corners + 12 edges + 5,000 random directions) — 100% exactly-one-owner, unaffected by this change as expected
  - [x] Visually re-confirmed: sphere still reads as a complete, connected planet
- [x] Checklist, GDD, and `Docs/Systems/PlanetSphere.md` updated with what was built and the triangle-count finding

## Documentation
- [x] `Docs/GDD.md` created
- [x] `Docs/Checklist.md` created (this file)
