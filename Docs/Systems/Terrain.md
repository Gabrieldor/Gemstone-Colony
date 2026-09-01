# Terrain (single-chunk World-scene prototype)

## What it is

The first real (non-debug) voxel terrain: one standalone chunk with noise-driven mountains and
lakes, viewed with a free-fly debug camera. Lives in `Assets/Scenes/World.unity` as the
`TerrainChunk` GameObject. Deliberately scoped without a sphere or region graph — proves the
terrain-generation fundamentals in isolation before the Planet system built multi-chunk/sphere
complexity on top. This is "phase 1"; the Planet system (`Docs/Systems/PlanetShared.md`,
`PlanetSphere.md`) is "phase 2" and reuses patterns from here.

## How it works

1. **Height from noise.** For each `(x, z)` column, sample 2D Perlin noise to get a ground
   height. Anything at or below that height is `Ground`; anything above the height but at or
   below a fixed water-level constant is `Water`; everything else is `Empty`.
2. **Face culling, same pattern as the voxel sphere.** A face is only emitted where the
   neighbor cell is `Empty` (or outside the chunk) — including a free optimization beyond what
   was originally asked: a lake-bed ground voxel's top face is never generated, since it's
   always covered by opaque water above it.
3. **Two separate meshes, not submeshes.** Ground and water are separate child GameObjects
   (`GroundMesh`/`WaterMesh`), each single-material — simpler to build and wire than
   submeshes.
4. **`[ExecuteAlways]` regeneration.** `TerrainChunkGenerator` regenerates both meshes on
   `OnEnable`/`OnValidate`, so tweaking any Inspector field (dimensions, noise scale,
   height/amplitude, water level, seed, voxel size) rebuilds live in the Editor.

## Key files

- `TerrainChunkVoxelData.cs` — pure data: "what's at cell `(x,y,z)`". No mesh/rendering
  knowledge (Information Expert — this class alone knows the answer).
- `TerrainChunkMeshBuilder.cs` — static; turns voxel data + a `TerrainVoxelType` into a merged
  mesh. Adapts (doesn't share code with) the voxel sphere's face-culling approach.
- `TerrainChunkGenerator.cs` — the only MonoBehaviour. Owns Inspector-tunable parameters and
  the regenerate trigger; contains zero generation logic itself.
- `TerrainVoxelType.cs` — `Empty | Ground | Water` enum.
- `FlyCamera.cs` (`Assets/Scripts/Player/`) — free-fly debug camera (WASD + mouse-look while
  right-click held, Q/E vertical, Shift to sprint). Polls the new Input System directly
  (`Keyboard.current`/`Mouse.current`) — this project has legacy `Input.*` disabled
  (`activeInputHandler: 1`), so `Input.GetKey` etc. would throw at runtime. Reused as-is by
  both Planet demos.

## Gotchas / invariants

- Materials (`TerrainGround.mat`/`TerrainWater.mat`) are plain flat-color URP/Lit — the
  "individual voxel" blocky shading comes entirely from unshared per-face vertices in the mesh
  builder, not from any shader trick. Both Planet demos reuse these two materials directly
  rather than creating new ones.
- This system has no sphere/curvature and no region graph by design — don't try to make it do
  planet-scale work. Extend the Planet system instead.

## Known limitations

- One fixed-size box chunk, not a streaming/multi-chunk world.
- Benchmarked triangle/vertex budgets (rolling-hill terrain, ~58% solid fill) that informed
  chunk-size choices downstream: 32×32×32 → ~2.4ms/~10k tris; 64×32×64 → ~7.2ms/~31k tris;
  64×64×64 → ~15.8ms/~46k tris (at the 60fps frame budget — needs background-thread generation
  past this size). A full-height single-mesh column always costs more than a cube-shaped chunk
  of equal volume (more exposed side-wall area) — split tall columns into stacked sections.
