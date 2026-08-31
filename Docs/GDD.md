# Game Design Document — Gemstone Colony

## Concept

- **Genre/format:** 3D voxel-based game.
- **Engine:** Unity 6000.5.4f1, URP (3D).
- **Art style:** Voxel models authored in MagicaVoxel, exported as OBJ (+ MTL + palette texture) and imported into Unity.

## Design decisions & rationale

- **2026-08-30 — Voxel-based direction confirmed.** Project scope set to 3D voxel. First character asset sourced from MagicaVoxel export rather than built in-engine.
- **2026-08-30 — Asset folder structure.** Standardized under `Assets/`: `Art/Models/{Characters,Environment,Props}`, `Art/Materials`, `Art/Animations`, `Audio/{Music,SFX}`, `Prefabs/{Characters,Environment,Props,UI}`, `Scripts/{Core,Player,Systems,UI,Editor}`, `UI/{Fonts,Sprites}`. No `Voxel`/world-chunk folders yet — deferred until a terrain/world-generation system is actually designed, to avoid guessing structure ahead of need.
- **2026-08-30 — MagicaVoxel OBJ import material handling.** Unity's OBJ importer embeds the generated material inside the model asset rather than as a standalone `.mat`. For workflow flexibility (editing shader properties via MCP, reuse across prefabs) we extract/recreate a standalone material under `Assets/Art/Materials/` per model instead of relying on the embedded one.
- **2026-08-30 — Double-sided rendering for voxel OBJ imports.** MagicaVoxel's OBJ export produced geometry that was invisible from the front (backface-culled) when using the default URP/Lit single-sided culling — likely inverted winding/normals from the exporter. Fix: set `_Cull = Off` on the character material. This is a per-asset render setting, not a normals fix in the mesh itself; if this recurs across many voxel imports, revisit at the export/import-pipeline level rather than patching each material individually.
- **2026-08-30 — Texture filtering for voxel textures.** Voxel palette textures (e.g. `character.png`) are set to **Point (no filter)** on import to keep flat voxel colors crisp instead of blurring/bleeding under default bilinear filtering.

## Assets

| Asset | Source | Path | Notes |
|---|---|---|---|
| Character (first character model) | MagicaVoxel export (`character.obj/.mtl/.png`) | `Assets/Art/Models/Characters/Character/` | Material: `Assets/Art/Materials/Character.mat` (URP/Lit, double-sided). No animation/rig yet. |

## Procedural Terrain — Voxel Planet Prototype

### Background & validated decisions (design session, 2026-08-31)

A long design/prototyping pass explored how a very large (~40,000-voxel-radius) spherical voxel planet should be chunked for streaming. All of it currently lives as standalone, non-gameplay debug scripts under `Assets/Scripts/Procedural/`, exercised in `SampleScene` — none of it is a real streaming/world system yet. Key conclusions, so future work doesn't re-derive them:

- **Voxel geometry = face-culled cubic voxels, analytic occupancy tested per-cell.** Reference implementation: `VoxelSphereMeshBuilder.cs` (already optimized — no per-cell hash set, precomputed face templates, pre-sized mesh buffers, no redundant tangent/bounds recalculation).
- **All chunks must sample one shared, world-aligned voxel lattice.** Chunks that each invent their own local/rotated coordinate frame produce visible seams (gaps + overlaps) at shared boundaries. Proven concretely: `HexVoxelSphereMeshBuilder.cs` is a deliberately-broken demo (independent per-region grids → visible gaps/overlaps); `HexLabeledVoxelSphereMeshBuilder.cs` is the fix (one shared lattice, region membership used only to color/label voxels, never to place them) — seamless.
- **Square, cube-sphere-aligned chunks are the production choice for voxel geometry.** They tile a shared lattice with zero boundary-classification cost, subdivide cleanly for LOD (quadtree), and have uniform per-chunk workload. Demonstrated in `CubeSphereChunkMeshBuilder.cs`.
- **Hex/pentagon geodesic regions (`GeodesicSphereChunkMeshBuilder.cs`, `IcosphereBuilder.cs`) are reserved for a coarse world-gen DATA layer only** — a `FindRegion(direction) → regionId` lookup for biome/elevation-bias decisions — never as the actual chunk-mesh boundary. Using them as literal chunk boundaries (the "puzzle piece implementation") has a real, provable seam problem when each chunk generates independently; the seam fix above resolves it, but hex chunks still don't subdivide cleanly for LOD the way square chunks do, and still cost an O(regionCount) (prunable to a small constant via a safe-radius early-out + precomputed neighbor adjacency) per-voxel classification that square chunks don't pay at all.
- **Radial convergence toward the planet's core is a real singularity** (every chunk wedge narrows toward the exact center point) but is a non-issue in practice as long as only a shallow crust is ever voxelized (Minecraft-bedrock-style depth cutoff). At realistic planet/chunk-size ratios the "sub-1-voxel-wide" radius is only tens of voxels out of a 40,000-voxel planet radius — confirmed via `r_bedrock = planetRadius / chunkSurfaceWidth`.
- **Per-chunk-mesh triangle/vertex budget was benchmarked** (rolling-hill Perlin heightmap terrain, ~58% solid fill): 32×32×32 generates in ~2.4ms (~10k triangles, comfortable frame-budget headroom); 64×32×64 in ~7.2ms (~31k triangles, still comfortable); 64×64×64 in ~15.8ms (~46k triangles, at the 60fps frame budget — needs background-thread generation past this size). **A full-height single-mesh column costs more than a cube-shaped chunk of equal volume** (more exposed side-wall area) — always split a tall column into stacked mesh sections rather than one mesh.
- **No mobile target for this project** — chunk/triangle budgets can lean toward the more generous PC end.

### Task: single-chunk terrain prototype (`World` scene)

Goal: prove out one playable 64×32×64 voxel terrain chunk with basic noise-driven topology (mountains + lakes), viewed with a fly camera, so ground/water materials and individual-voxel readability can be evaluated visually before the full streaming planet system is built.

See `Docs/Checklist.md` → "World scene prototype" for the actionable task list.

### World scene prototype — build notes (2026-08-31)

Built as specified, no scope deviations. Architecture:

- **`TerrainChunkVoxelData.cs`** (`Assets/Scripts/Procedural/`) — pure data class, no Unity mesh/rendering knowledge. Samples 2D Perlin noise per (x, z) column to decide ground height, fills anything below the water-level constant with water, everything else stays `Empty`. Exposes a bounds-safe `TypeAt`/`IsSolid` lookup. Kept separate from mesh building (SRP / Information Expert: this class alone knows "what's at this cell").
- **`TerrainChunkMeshBuilder.cs`** — static, adapts (does not share code with) `VoxelSphereMeshBuilder.cs`'s `FaceTemplate`/face-culling approach. Builds one mesh per voxel type; a face is emitted only where the neighbor cell is `Empty` or outside the chunk. A voxel of any non-`Empty` type counts as solid for face-culling purposes, so e.g. a lake-bed ground voxel's top face (covered by opaque water) is never generated — a free triangle-count saving beyond what the checklist asked for.
- **`TerrainChunkGenerator.cs`** — `[ExecuteAlways]` MonoBehaviour, same `OnEnable`/`OnValidate`/`[ContextMenu("Regenerate")]` pattern as `CubeSphereChunkDebugVisual.cs`. Owns the Inspector-tunable parameters (dimensions, noise scale, height base/amplitude, water level, seed, voxel size) and two `MeshFilter` references (ground/water); regenerates both meshes on any Inspector edit.
- **`FlyCamera.cs`** (`Assets/Scripts/Player/`) — polls the new Input System directly (`Keyboard.current`/`Mouse.current`), not `Input.*`. This project's `ProjectSettings.asset` has `activeInputHandler: 1` (Input System Package only, no legacy Input Manager), so legacy `Input.*` calls would throw `InvalidOperationException` at runtime.
- Ground and water are two separate child GameObjects (`GroundMesh`/`WaterMesh` under `TerrainChunk`), each with its own single-material mesh, rather than one mesh with two submeshes — simpler to build and to wire in the Inspector; the checklist allowed either.
- Materials (`TerrainGround.mat`, `TerrainWater.mat`) are plain URP/Lit with a flat `_BaseColor` and no textures. The flat "individual voxel" shading comes entirely from the mesh builder emitting unshared vertices per face (each face gets its own normal) — same as `VoxelGray.mat`'s approach — not from any special shader setup.
- Verified via Unity MCP: no compile/console errors in edit or play mode, screenshots confirm mountains/lakes/voxel-level shading readability, Play mode entered/exited cleanly.
