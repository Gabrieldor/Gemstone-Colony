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

### Task, phase 1: single-chunk terrain fundamentals (`World` scene) — done, 2026-08-31

Goal: prove out the basics in isolation — one standalone voxel terrain chunk with noise-driven mountains/lakes, a fly camera, and readable ground/water materials — before adding any planet-scale complexity on top. Deliberately scoped without a sphere or the region graph: with only one chunk in the scene, there's nothing for a region lookup to check against. See build notes below for what was actually built; it's the foundation phase 2 (below) builds on.

### World scene prototype, phase 1 — build notes (2026-08-31)

Built as specified, no scope deviations. Architecture:

- **`TerrainChunkVoxelData.cs`** (`Assets/Scripts/Procedural/`) — pure data class, no Unity mesh/rendering knowledge. Samples 2D Perlin noise per (x, z) column to decide ground height, fills anything below the water-level constant with water, everything else stays `Empty`. Exposes a bounds-safe `TypeAt`/`IsSolid` lookup. Kept separate from mesh building (SRP / Information Expert: this class alone knows "what's at this cell").
- **`TerrainChunkMeshBuilder.cs`** — static, adapts (does not share code with) `VoxelSphereMeshBuilder.cs`'s `FaceTemplate`/face-culling approach. Builds one mesh per voxel type; a face is emitted only where the neighbor cell is `Empty` or outside the chunk. A voxel of any non-`Empty` type counts as solid for face-culling purposes, so e.g. a lake-bed ground voxel's top face (covered by opaque water) is never generated — a free triangle-count saving beyond what the checklist asked for.
- **`TerrainChunkGenerator.cs`** — `[ExecuteAlways]` MonoBehaviour, same `OnEnable`/`OnValidate`/`[ContextMenu("Regenerate")]` pattern as `CubeSphereChunkDebugVisual.cs`. Owns the Inspector-tunable parameters (dimensions, noise scale, height base/amplitude, water level, seed, voxel size) and two `MeshFilter` references (ground/water); regenerates both meshes on any Inspector edit.
- **`FlyCamera.cs`** (`Assets/Scripts/Player/`) — polls the new Input System directly (`Keyboard.current`/`Mouse.current`), not `Input.*`. This project's `ProjectSettings.asset` has `activeInputHandler: 1` (Input System Package only, no legacy Input Manager), so legacy `Input.*` calls would throw `InvalidOperationException` at runtime.
- Ground and water are two separate child GameObjects (`GroundMesh`/`WaterMesh` under `TerrainChunk`), each with its own single-material mesh, rather than one mesh with two submeshes — simpler to build and to wire in the Inspector; the checklist allowed either.
- Materials (`TerrainGround.mat`, `TerrainWater.mat`) are plain URP/Lit with a flat `_BaseColor` and no textures. The flat "individual voxel" shading comes entirely from the mesh builder emitting unshared vertices per face (each face gets its own normal) — same as `VoxelGray.mat`'s approach — not from any special shader setup.
- Verified via Unity MCP: no compile/console errors in edit or play mode, screenshots confirm mountains/lakes/voxel-level shading readability, Play mode entered/exited cleanly.

### Task, phase 2: full small-sphere puzzle-piece demo (`World` scene) — done, 2026-08-31

Goal: build a small, fully-connected planet out of multiple **independently-generated** square
voxel chunks (not one merged mesh) covering all 6 cube faces, all sampling one shared,
world-aligned lattice, with the hex/pentagon region graph used purely as a per-chunk biome
lookup — proving out, for real, that (a) independently-generated chunks tile with zero seams
(including at cube-face edges), and (b) region membership visibly drives terrain variation.
Viewed with the same fly camera from phase 1.

An earlier version of this task built a single-face 16-chunk "patch" instead of a full sphere;
it proved the within-face tiling claim but could never visually read as a planet (pulled back,
it was just a small tile floating in empty space), so it was scrapped in favor of the full
6-face version below once that limitation became clear. See `Docs/Systems/PlanetSphere.md` for
the living reference on how this system works; this section is the historical build record.

### World scene prototype, phase 2 — build notes (2026-08-31)

New code lives under `Assets/Scripts/Procedural/Planet/` (separate subfolder from phase 1's
flat `Procedural/`, since this is a distinct, larger subsystem):

- **`SmallPlanetConfig.cs`** — the single shared source of truth: base radius 32 voxels, shell
  thickness 8, sea level 31, noise frequency 5, region subdivision 1 (42 regions), 4×4 chunks
  per face. Every other script reads from this; nothing is duplicated per-chunk.
- **`Biome.cs`** / **`BiomeShapeParams.cs`** / **`SmallBiomeCatalog.cs`** — Mountain (+1
  radius, ±3 amplitude, no water), Plains (+0, ±1, no water), Basin (−2, ±1, water filled to
  the sea-level radius).
- **`SmallPlanetTerrainSampler.cs`** — turns (direction, biome) into an outer radius. Noise is
  the average of three axis-aligned 2D Perlin projections sampled directly from the world
  direction vector — a pure function of direction with no per-chunk seed, so two chunks always
  agree on a shared boundary cell regardless of which face(s) they're on.
- **`PlanetRegionGraph.cs`** — builds the region graph via `IcosphereBuilder` (subdivision 1,
  42 regions), adjacency from shared triangle edges, then flood-fills 3 biomes outward from 3
  greedily-spread seed regions (farthest-point sampling) — produces a few large contiguous
  biome zones, not per-region speckle.
- **`SmallPlanetVoxelClassifier.cs`** — the one shared "what's at cell (x,y,z)" rule every
  chunk calls identically: direction + biome → outer/inner radius → Ground/Water/Empty.
- **`CubeFace.cs`** — general 6-face cube-sphere math (all 6 face normals/tangents/bitangents,
  plus `Classify(direction)` for deterministic face ownership at cube edges/corners).
- **`SmallPlanetChunkLayout.cs`** — full `[-1, 1]` per-face chunk tiling; ownership checks
  `CubeFace.Classify` *before* the (s, t)-range test, since a cell can be geometrically close
  to a chunk's rectangle while actually belonging to a different face.
- **`SmallPlanetChunkVoxelData.cs`** — per-chunk Information Expert (same split as phase 1's
  `TerrainChunkVoxelData`): precomputes global type + ownership for every cell in its AABB,
  purely from the shared config/classifier/layout and its own `(faceIndex, i, j)` — never
  reads another chunk's data or GameObject.
- **`SmallPlanetChunkMeshBuilder.cs`** — adapts phase 1's `FaceTemplate` pattern. Emits a face
  only for cells this chunk **owns**, but decides face exposure from **global** solidity (not
  ownership) — an owned solid cell next to an unowned-but-solid neighbor still gets that face
  suppressed, since the neighbor chunk will render it. This is the actual mechanism that makes
  independent generation tile seamlessly.
- **`SmallPlanetChunkGenerator.cs`** — thin `[ExecuteAlways]` adapter mirroring
  `TerrainChunkGenerator`; owns `(faceIndex, chunkI, chunkJ)` + two `MeshFilter` refs, shares
  one statically-cached region graph across all 96 instances.

**A real bug was found and fixed during verification: a region-lookup hill-climb shortcut
could converge to the wrong region.** An early version resolved a cell's region via a
hill-climb over the region adjacency graph from a hint (previous cell's region) instead of
scanning all regions, to avoid an O(regionCount) cost per voxel. At this demo's coarse 42
regions, the exhaustive cross-face-edge boundary scan (below) caught 15 cells where the greedy
walk landed on the wrong region, producing a biome/type disagreement between two chunks for
the same physical cell — a real, if narrow, seam cause. **Fixed by removing the hill-climb
entirely** and having every consumer call `PlanetRegionGraph.FindNearestRegion` (brute-force)
directly — cheap enough at 42 regions, and it sidesteps the whole bug class. The
non-convergence risk of a hill-climb-style shortcut is a real latent finding worth flagging for
any future work at much larger (production-scale) region counts, where brute-force stops being
cheap enough and a correct fast lookup will be needed.

**Scene:** `PlanetSmall` (96 `Chunk_f{face}_{i}_{j}` children, 6 faces × 4×4, each with
`GroundMesh`/`WaterMesh` grandchildren) added to `World.unity`, centered at `(-150, 60, -50)` —
clear of phase 1's `TerrainChunk`. Reuses `TerrainGround.mat`/`TerrainWater.mat`, the scene's
existing light, and `FlyCamera` — no new materials/lights/cameras. One full 96-chunk
regeneration pass produces ~55.1k ground + ~7.5k water triangles and runs in a few seconds
in-editor.

**Verification via Unity MCP:**
- No compile errors, no console errors/warnings; play mode entered/exited cleanly.
- **Within-face boundaries:** two independent chunk-pair scans (an s-axis pair and a t-axis pair on the same face), 0 gaps / 0 double-owned both times.
- **Cross-face-edge boundary:** scanned every solid cell along the full +X/+Y cube edge (all 4 chunks on each face's edge row, 8 chunks total) — 0 gaps / 0 double-owned after the fix above, 5,247 solid boundary cells scanned; found 15 real gaps before it.
- Visually: screenshots from 4 different angles around the sphere all show a complete, round, connected silhouette with no gaps — reads as an actual small planet.
- **Known minor cosmetic artifact, not a seam:** faint thin lines are visible in screenshots aligned exactly with the internal chunk grid on each face, even with the numeric checks clean. Most likely flat-shading/self-shadowing precision at independently-generated mesh-to-mesh boundaries (each chunk is its own `MeshRenderer`, lit independently) rather than a geometric crack — not chased further given the numeric verification is unambiguous, but worth another look if a future pass wants a fully seamless *visual* result, not just a topologically seamless one.

See `Docs/Checklist.md` → "World scene prototype, phase 2" for the checked-off task list.

### Follow-up: boundary-aware region-lookup optimization (2026-08-31)

The all-brute-force fix above (call `FindNearestRegion`, O(regionCount), for every cell) is
correct but wasteful — most cells are deep in a chunk's interior, where a wrong region can
never disagree with anything (nothing else ever computes that same interior cell). Reintroduced
the hill-climb as `PlanetRegionGraph.ResolveRegionNear` (moved to the shared file — no size
dependency), used only for cells comfortably inside a chunk; `FindNearestRegion` is still used,
unconditionally, for cells within a small margin of a chunk boundary
(`SmallPlanetChunkLayout.IsNearChunkBoundary` / `SmallPlanetConfig.RegionBoundaryMargin`).

**A second real bug turned up while tuning this — the boundary test itself was measuring the
wrong thing.** The first version checked boundary-proximity using the *asking* chunk's own
face-relative `(s, t)`, which becomes numerically extreme/unstable for cells that don't
actually project well onto that face — exactly the cells near a cross-face boundary. This
reintroduced type disagreements (up to 34 mismatched cells at some cross-face pairs), and
*increasing* the margin didn't fix it (still ~20-34 mismatches even at margin = full chunk
width), which is what proved it wasn't a margin-size problem. **Fixed** by always classifying
boundary-proximity on the cell's own **native** face/chunk (`CubeFace.Classify` + that face's
own `TryGetST`), not whichever chunk happens to be asking.

**Verification:** exhaustive scan of all 96 chunks (732 chunk pairs with any AABB overlap,
within-face and cross-face-edge both included) against the real compiled
`SmallPlanetChunkVoxelData` class — 0 type disagreements at the final margin (0.02, tuned
empirically against this exhaustive check, not derived analytically). Result: 82% of cells use
the cheap hill-climb, 18% pay for brute-force — down from 100% before this optimization. A full
96-chunk regeneration pass now runs in well under a second in-editor. Re-confirmed visually
(sphere still reads as a complete connected planet from multiple angles) and via play mode
(clean, no errors).

See `Docs/Systems/PlanetShared.md` and `Docs/Systems/PlanetSphere.md` for the living reference
on this optimization; `Docs/Checklist.md` → "World scene prototype, phase 2" has the checklist
entry.

### Follow-up: real chunk-ownership gap found and fixed (2026-08-31)

**Found visually, not by a numeric check, and predates all the region-lookup work above —
it's a separate bug, in the ownership/placement logic, not region lookup.** Selecting a corner
chunk (`Chunk_f2_3_3`) in the Editor and looking at its selection outline showed a real gap:
solid terrain up to a jagged edge, then nothing beyond it, where another chunk should have
continued the surface. Every earlier automated check (region/type agreement between chunks,
mesh face-count vs. voxel data, live scene component health) came back clean — because all of
them tested *agreement between chunks*, never *"does every direction have an owner at all."*

**Root cause:** `SmallPlanetChunkLayout.OwnsCell` used a raw half-open interval (`s >= s0 && s
< s1`) to decide ownership. That's correct at internal grid lines, but the outer edge of a face
(`s` or `t` exactly `+1`) has no "next chunk" to catch it via a lower-bound test — the last
chunk's own range excludes its upper end, so a cell landing exactly on `s == 1` belonged to no
chunk. Not a rare float coincidence: for integer voxel coordinates, this happens whenever two
axis magnitudes are exactly equal (common near diagonals, guaranteed near cube corners). A
direct scan found 13 of 169 sampled directions near one corner with zero owners across all 96
chunks.

**Fixed** by using the already-existing `NativeChunkIndex` (clamped-floor helper, originally
written for the region-lookup optimization) for ownership too, instead of the raw range
comparison — `NativeChunkIndex(s) == chunkI && NativeChunkIndex(t) == chunkJ`. Clamping means
`s == 1.0` correctly lands in the last chunk instead of falling through.

**Verification:** the same corner now shows exactly one owner for all 169 sampled directions.
Broadened to all 8 cube corners + all 12 cube edges (3,380 samples) and 5,000 uniformly-random
directions across the whole sphere — 100% exactly-one-owner in every case. Regenerated the real
scene and visually re-confirmed at the exact chunk/angle that showed the gap.

**Takeaway:** "chunks agree with each other" and "every cell has an owner" are different
properties — a seam/gap check needs both, and only the first was actually being tested before.
See `Docs/Systems/PlanetSphere.md` for full detail.

### Follow-up: r_bedrock actually implemented (2026-08-31)

The background section above named `r_bedrock = planetRadius / chunkSurfaceWidth` as the radius
below which a chunk's angular wedge becomes sub-1-voxel-wide, and said the fix was a
"Minecraft-bedrock-style depth cutoff" — but no code ever actually implemented that cutoff.
The demo's crust instead used `ShellThicknessVoxels`, a fixed *thickness measured back from
each direction's own local terrain height* — meaning a mountain's crust bottomed out deeper
than a basin's, at no radius connected to any actual "core" concept, and nothing stopped the
crust from (in principle) being made deep enough to approach the real singularity.

**Changed:** the crust now always spans a fixed absolute range,
`[SmallPlanetConfig.BedrockRadiusVoxels, outer]`, the same floor for every direction regardless
of biome. `BedrockRadiusVoxels = BaseRadiusVoxels / ChunkSurfaceWidthVoxels` (currently 32/16 =
2 at this demo's scale) is now a real computed constant, and everything below it is
unconditionally `Empty` — deliberately left blank, exactly as originally intended.
`SmallBiomeCatalog.MinPossibleInnerRadius` (a per-biome minimum-radius calculation the old
relative-thickness approach needed) was removed entirely; the AABB sizing just uses the one
shared constant now.

**Not a regression, a real improvement:** ground triangle count dropped from ~55k to ~32.7k.
The old relative-thickness shell created extra vertical "cliff" faces wherever two adjacent
biomes' inner boundaries landed at different depths; one uniform bedrock floor has a single
continuous (and tiny) inner surface instead of a stepped one.

**Verification:** full re-run of every existing check against the regenerated real scene — type
ground-truth (real class vs. 100% brute-force) across all 96 chunks, mesh face-count vs.
expected-exposed-faces per chunk, and the ownership-completeness corner/edge/random-sample
audit from the bug above. All clean, 0 divergence. See `Docs/Systems/PlanetSphere.md` → "The
r_bedrock floor" for full detail.
