# Planet — shared infrastructure ("puzzle piece" region graph)

## What it is

The shared pieces used by the Planet sphere system (`PlanetSphere.md`): the hex/pentagon
biome region graph, and the general cube-face math. This is the "puzzle piece" concept from
the design/prototyping pass (see `Prototypes.md`) turned into real code — a per-direction
biome lookup that never decides voxel placement, only what kind of terrain a chunk should
generate once it's already decided which cells it owns.

## How it works

1. **Region graph** (`PlanetRegionGraph.cs`): build a geodesic sphere via `IcosphereBuilder`
   (see `Prototypes.md`) at a chosen subdivision level — one region per icosphere vertex.
   Region *adjacency* comes for free from the icosphere's triangles: two regions are adjacent
   if some triangle contains both vertex indices.
2. **Biome assignment via flood-fill, not per-region randomness.** Pick a handful of
   well-separated "seed" regions (farthest-point selection), assign each a biome
   (`Biome.cs`: `Mountain | Plains | Basin`), then BFS outward over the adjacency graph so
   every region inherits its nearest seed's biome. This is deliberate: naive per-region random
   assignment produces speckled noise with no visible structure; flood-fill produces a few
   large contiguous zones with real boundaries you can actually see and screenshot.
3. **Two region-lookup methods, used selectively, not interchangeably:**
   - `FindNearestRegion(direction)` — brute-force dot-product argmax over region centers.
     Guaranteed correct. O(regionCount); fine at "a few hundred regions" scale.
   - `ResolveRegionNear(direction, hint)` — adjacency hill-climb from a hint region, walking
     to whichever neighbor improves the dot product until no neighbor helps. O(hops), not
     O(regionCount) — but a local max isn't always the true global-nearest region. See the
     gotcha below for exactly when each is safe to use.
4. **Biome → terrain parameters** (`BiomeShapeParams.cs`): each biome maps to a
   `(baseRadiusOffset, heightAmplitude, hasWater)` tuple that a terrain sampler turns into
   actual per-direction ground height and water fill. Plain data — every planet-scale demo
   defines its own catalog of these values sized for its own radius (see `SmallBiomeCatalog`
   in `PlanetSphere.md`), since the numbers are inherently tied to a specific base radius.
5. **`CubeFace.cs`**: general cube-to-sphere face math — all 6 face normals, each with its own
   tangent/bitangent basis, and `Classify(direction)` → dominant face, with deterministic
   tie-breaking so every caller agrees exactly at cube edges/corners.

## Key files (`Assets/Scripts/Procedural/Planet/`)

- `Biome.cs` — the 3-value enum.
- `BiomeShapeParams.cs` — plain per-biome shape data (radius offset, height amplitude, water).
- `PlanetRegionGraph.cs` — region graph construction + flood-fill biome assignment + nearest-
  region lookup.
- `CubeFace.cs` — general 6-face cube-sphere math.

## Gotchas / invariants

- **`ResolveRegionNear`'s hill-climb is only safe away from chunk/face boundaries — using it
  unconditionally caused a real bug once, twice.** At coarse region counts, a greedy hill-climb
  can converge to a *local* optimum that isn't the true nearest region. That's harmless deep in
  a chunk's interior (nothing else ever computes that exact same interior cell to disagree
  with), but right at a boundary two neighboring chunks can disagree about which biome a shared
  cell belongs to — which looks exactly like a geometric seam even though the placement/lattice
  math is fine. This happened for real (see `PlanetSphere.md`): first when the hill-climb was
  used unconditionally everywhere (fixed at the time by removing it and brute-forcing every
  cell), then again when it was reintroduced with a boundary-proximity guard that measured
  proximity using the *wrong* chunk's face-relative coordinates (fixed by always classifying
  proximity on the cell's own **native** face — see `SmallPlanetChunkLayout.
  IsNearChunkBoundary`'s doc comment). If a cross-face-edge or chunk-boundary gap ever
  reappears, check region agreement between the two chunks before assuming it's a
  placement/winding bug — and check whether the boundary test is using native-face coordinates
  before assuming the margin is just too small.
- Every consumer must derive positions from the **same shared planet config** (radius, shell
  thickness, voxel size) via the same formula — this is what actually makes independently-
  generated chunks tile seamlessly. Don't let per-chunk state drift from the shared source.

## Known limitations

- `FindNearestRegion`'s brute-force scan (and the boundary-detection overhead around
  `ResolveRegionNear`) are only cheap because region counts here are in the tens/hundreds. A
  real production planet (the ~40,000-voxel-radius scale discussed in `Docs/GDD.md`) would
  need this whole approach re-validated at scale — more regions, a different boundary-margin
  value (the current one was tuned empirically for one specific demo's geometry, not derived
  from first principles), and likely a spatial index rather than brute-force at the boundary.
