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
- [ ] Voxel world/terrain system — architecture explored and validated (see `Docs/GDD.md` → "Procedural Terrain — Voxel Planet Prototype"); single-chunk prototype done (see "World scene prototype" below), full streaming/multi-chunk planet system not started
- [ ] Camera system
- [ ] UI/HUD

## World scene prototype — single-chunk terrain demo (done, 2026-08-31)

Goal: a playable, visually-verifiable proof-of-concept of one 64×32×64 voxel terrain chunk with noise-driven mountains/lakes, viewed with a fly camera — informs material/readability decisions before the full streaming planet system is built. See `Docs/GDD.md` → "World scene prototype — build notes" for what was actually built and any deviations from the original spec.

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

## Documentation
- [x] `Docs/GDD.md` created
- [x] `Docs/Checklist.md` created (this file)
