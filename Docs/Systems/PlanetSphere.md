# Planet — full small sphere demo

## What it is

A complete, small, fully-connected planet: all 6 cube faces generated in full, small enough
(radius 32 voxels) to generate in a few seconds and to visibly read as a round connected
sphere from a distance. This is the project's only planet-generation demo — an earlier
single-face "patch" version was scrapped in favor of this. Lives as the `PlanetSmall`
GameObject in `Assets/Scenes/World.unity`.

## How it works

Reuses the shared pieces from `PlanetShared.md` (`Biome`, `BiomeShapeParams`,
`PlanetRegionGraph`, `CubeFace`, `IcosphereBuilder`) directly.

1. **Config** (`SmallPlanetConfig.cs`): single source of truth — base radius 32 voxels, sea
   level 31, noise frequency 5, region subdivision 1 (42 regions), 4×4 chunks per face.
   Everything else reads from here; nothing is duplicated per-chunk.
2. **Biomes** (`SmallBiomeCatalog.cs`): Mountain (+1 radius, ±3 amplitude, no water), Plains
   (+0, ±1, no water), Basin (−2, ±1, water) — small numbers because they're proportional to
   this demo's 32-voxel radius.
3. **Terrain shape is a pure function of world direction** (`SmallPlanetTerrainSampler.cs`):
   noise is the average of three axis-aligned 2D Perlin projections sampled directly from the
   direction vector — no per-chunk seed or face-local coordinate, so two chunks always agree
   on a shared boundary cell regardless of which face(s) they're on.
4. **Voxel classification** (`SmallPlanetVoxelClassifier.cs`): the one shared "what's at cell
   (x,y,z)" rule every chunk calls identically — direction + biome → outer radius →
   Ground/Water/Empty. The crust always spans `[SmallPlanetConfig.BedrockRadiusVoxels, outer]`
   — see "The r_bedrock floor" below.
5. **Full per-face tiling, not a patch** (`SmallPlanetChunkLayout.cs`): `[-1, 1]` on all 6
   faces, not a centered sub-region. Ownership checks `CubeFace.Classify(direction)` *before*
   the (s,t)-range test — necessary once there's more than one face in play, since a cell can
   be geometrically close to a chunk's (s,t) rectangle while actually belonging to a different
   face entirely.
6. **Per-chunk generation never reads another chunk's data** (`SmallPlanetChunkVoxelData.cs`):
   each chunk computes a tight world-space AABB from its corners, then for every cell in that
   AABB, classifies ownership and decides solid/water/empty from the shared formula — the same
   answer any other chunk would compute for that cell, but only the owning chunk emits it.
7. **Face culling looks at true global solidity, not just this chunk's cells**
   (`SmallPlanetChunkMeshBuilder.cs`) — an owned solid cell next to an unowned-but-solid
   neighbor still gets that face suppressed, since the neighbor chunk will render it. This is
   the actual mechanism that makes independent generation tile seamlessly.
8. **`SmallPlanetChunkGenerator.cs`** — thin `[ExecuteAlways]` MonoBehaviour: owns
   `(faceIndex, chunkI, chunkJ)` + two `MeshFilter` refs, shares one statically-cached region
   graph across all 96 chunk instances.

## Config numbers

Radius 32 voxels, bedrock radius 2 (see below), region subdivision 1 (42 regions), 4×4 chunks
per face × 6 faces = 96 chunks total (~16 voxels/edge each). ~32.7k ground + ~7.7k water
triangles; generates in ~1s in-Editor.

## The r_bedrock floor

The crust's inner bound is a **fixed radius**, not a fixed thickness relative to the local
terrain surface. Per the GDD's background section: a chunk covers a fixed *angular* slice of
the sphere, so its physical width shrinks linearly toward the center — below
`SmallPlanetConfig.BedrockRadiusVoxels = BaseRadiusVoxels / ChunkSurfaceWidthVoxels`, a chunk's
wedge would be under 1 voxel wide and the cube-face-chunk scheme becomes geometrically
degenerate, not just wasteful to render. `SmallPlanetVoxelClassifier.TypeAt` treats
`dist < BedrockRadiusVoxels` as unconditionally `Empty`, regardless of biome — everything below
is deliberately left unvoxelized, the way Minecraft's bedrock layer works.

This replaced an earlier `ShellThicknessVoxels` (a fixed *thickness* measured back from each
direction's own local terrain height) — that meant a mountain's crust bottomed out deeper than
a basin's, at no radius in particular, disconnected from any actual "core" concept. The
bedrock floor is now the same absolute radius everywhere, matching how a real planet (and
Minecraft) actually works. It also **simplified** the AABB sizing: no more per-biome minimum
radius calculation (`SmallBiomeCatalog.MinPossibleInnerRadius` was removed entirely) — the
floor is just the one constant, `SmallPlanetChunkLayout.GetChunkAabb` uses
`BedrockRadiusVoxels` directly.

**Side effect, not a regression:** ground triangle count actually *dropped* (~55k → ~32.7k)
with this change, re-verified clean. The old per-biome-relative shell created extra vertical
"cliff" faces wherever two adjacent biomes' inner boundaries differed in depth; a single
uniform bedrock floor has one continuous inner surface (a tiny sphere at radius 2, negligible
face count) instead of a stepped one.

## A real, pre-existing ownership gap (found visually, not by a numeric check)

**This bug predates all the region-lookup work below and has nothing to do with it** — it was
in `SmallPlanetChunkLayout` from this demo's original build, undetected by every earlier
verification pass (including the region-lookup optimization's own exhaustive checks), because
those all tested *agreement between chunks*, never *"does every direction have an owner at
all."**

Found by directly looking at the Editor: selecting a corner chunk (`Chunk_f2_3_3`) and looking
at its selection outline showed a real gap — solid green terrain right up to a jagged edge,
then nothing (background) beyond it, at a spot where another chunk should have continued the
surface. Automated checks (region/type ground-truth comparison across all 96 chunks, mesh
face-count vs. expected-exposed-face-count per chunk, live scene component health) all came
back clean, which was the first sign this wasn't a region-lookup or mesh-building issue at all.

**Root cause:** `OwnsCell` used a raw half-open interval, `s >= s0 && s < s1` (and the same for
`t`), to decide which chunk owns a given `(s, t)`. That's correct at every *internal* grid
line — the next chunk's inclusive `s >= s0` catches the boundary value. But the outer edge of a
face (`s` or `t` exactly `+1`) has no "next chunk" to catch it: the last chunk's own range is
exclusive at its upper end (`s < 1.0` when `s1 == 1.0`), so a cell landing exactly on `s == 1`
belonged to **no chunk at all**. This is not a rare float coincidence to shrug off — for
*integer* voxel coordinates, `s` or `t` lands exactly on a whole-face boundary whenever two of
the direction's axis magnitudes are exactly equal (e.g. cell `(18, 18, -18)`), which happens
often near diagonals and is guaranteed near cube corners, where multiple such ties coincide. A
direct scan confirmed it: 13 of 169 sampled directions near one corner had **zero** owners
across all 96 chunks.

**Fixed** by using `NativeChunkIndex` (the clamped-floor helper already written for the
region-lookup optimization below) instead of the raw range comparison: `NativeChunkIndex(s) ==
chunkI && NativeChunkIndex(t) == chunkJ`. Clamping the floored index to `[0, ChunksPerFaceEdge
- 1]` means `s == 1.0` correctly floors/clamps into the last chunk instead of falling through.

**Verification:** the same corner that showed zero owners now shows exactly one, for all 169
sampled directions. Broadened to all 8 cube corners and all 12 cube edges (169 samples each,
3,380 total) — 100% exactly-one-owner, 0 zero, 0 double. A further 5,000 uniformly-random
directions across the whole sphere: also 100% exactly-one-owner. Regenerated the real scene and
visually re-confirmed at the exact chunk/angle that showed the gap — solid, continuous terrain,
no gray patch.

**Takeaway for future verification:** "do independently-generated chunks agree with each
other" and "does every cell have an owner at all" are different properties, and only the first
was actually being tested before. A seam/gap check needs both.

## The real bug this demo found (worth knowing before touching region-lookup code)

An exhaustive scan across **cube-face edges** found 15 real gaps early on. Root cause: an
adjacency hill-climb shortcut for region lookup converged to the wrong region at this demo's
coarse 42-region graph, so two chunks on either side of a face edge occasionally disagreed
about a boundary cell's biome, producing a visible gap that looked geometric but was actually a
biome disagreement. **Fixed at the time** by calling `PlanetRegionGraph.FindNearestRegion`
(brute-force, O(regionCount)) unconditionally for every cell, everywhere, and removing the
hill-climb entirely.

## Optimization: brute-force only near real boundaries, hill-climb everywhere else

The all-brute-force fix above is correct but wasteful — it pays the O(regionCount) cost for
every cell, including the ~80%+ that are deep in a chunk's interior where a wrong region can
never disagree with anything (nothing else ever computes that same interior cell). Reintroduced
the hill-climb (`PlanetRegionGraph.ResolveRegionNear`, in the shared file since it has no
size dependency) for interior cells, and kept `FindNearestRegion` only for cells within
`SmallPlanetConfig.RegionBoundaryMargin` of a chunk boundary (`SmallPlanetChunkLayout.
IsNearChunkBoundary`). Each chunk seeds its hill-climb hint once per chunk (one brute-force
call at the chunk's center direction), then carries the hint forward across cells, updating it
to whichever method was actually used each time.

**A second real bug was found tuning this, worth internalizing:** the first version checked
boundary-proximity using the *asking* chunk's own face-relative `(s, t)`, which is fine near
that face's center but becomes numerically extreme/unstable for cells that don't actually
project well onto that particular face (near or past its own edge) — exactly the cells near a
cross-face boundary, i.e. exactly where this test mattered most. This reintroduced type
disagreements at several cross-face-edge pairs (up to 34 mismatched cells in testing), and
critically, simply *increasing* the margin didn't fix it (still ~20-34 mismatches even at
margin = full chunk width) — proving the bug wasn't "margin too small" but "measuring the
wrong thing." **Fixed** by always classifying boundary-proximity on the cell's own **native**
face/chunk (`CubeFace.Classify(direction)` + that face's own `TryGetST`, via
`SmallPlanetChunkLayout.NativeChunkIndex`), which is always well-behaved regardless of which
chunk is asking. Verified via an exhaustive scan of all 96 chunks (732 chunk pairs with any
AABB overlap) at the final margin — 0 type disagreements, checked against the real compiled
class, not just a standalone test replica.

**Final numbers:** margin = 0.02 (an empirically-verified value, not derived from chunk width —
see `SmallPlanetConfig.cs`'s comment) gives an 82% interior (cheap hill-climb) / 18%
near-boundary (brute-force) split. One full 96-chunk regeneration pass takes ~0.5s in-editor,
down from earlier all-brute-force timings, though this wasn't a rigorously isolated A/B
benchmark.

## Key files (`Assets/Scripts/Procedural/Planet/Sphere/`)

- `SmallPlanetConfig.cs` — radius/bedrock/chunk-count constants + the region-boundary margin.
- `SmallBiomeCatalog.cs` — biome parameters for this demo's scale (outer-radius shaping only —
  no per-biome inner radius; the bedrock floor is a single shared constant).
- `SmallPlanetChunkLayout.cs` — per-face chunk ownership (`OwnsCell`, via `NativeChunkIndex` —
  see the ownership-gap bug above) + AABB, plus `IsNearChunkBoundary` for the region-lookup
  optimization below.
- `SmallPlanetTerrainSampler.cs` / `SmallPlanetVoxelClassifier.cs` — direction+biome → terrain.
- `SmallPlanetChunkVoxelData.cs` — per-chunk data; owns the interior/near-boundary branch.
- `SmallPlanetChunkMeshBuilder.cs` — per-chunk mesh.
- `SmallPlanetChunkGenerator.cs` — MonoBehaviour orchestrator, loops all 6 faces × 4×4 chunks.

(`PlanetRegionGraph.ResolveRegionNear`, the hill-climb itself, lives in the shared
`Assets/Scripts/Procedural/Planet/PlanetRegionGraph.cs` — see `PlanetShared.md`.)

## Verification performed

Exhaustive scan across all 96 chunks / all 732 chunk pairs with any AABB overlap (within-face
and cross-face-edge both included, using the real compiled `SmallPlanetChunkVoxelData` class):
0 type disagreements. Screenshots from multiple angles around the sphere confirming a complete
round silhouette with no visible gap from any side. No compile/console errors, play mode clean.

Re-run in full after the r_bedrock change (type ground-truth, mesh face-count, and the
ownership-completeness corner/edge/random checks from the ownership-gap fix above, all against
the regenerated real scene): still 0 divergence in every check.

## Known limitations / open items

- **Cosmetic artifact, not a seam:** faint lines aligned with the internal chunk grid remain
  visible even with clean numeric boundary checks — most likely independent per-chunk-mesh flat
  shading/self-shadowing rather than a geometric gap (the numeric scan rules out an actual
  gap). Not chased further; flag if it turns out to matter visually later.
- One fixed small demo scale (32-voxel radius, 96 chunks). Scaling up toward the
  ~40,000-voxel production planet size discussed in `Docs/GDD.md` is real future work — bigger
  region counts, streaming/LOD, and re-verifying (or re-deriving) the boundary margin at that
  scale all need solving first; the 0.02 value here was tuned empirically for this specific
  demo's geometry and region count, not derived from first principles.
