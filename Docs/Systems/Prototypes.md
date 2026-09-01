# Prototypes (sphere-chunking design exploration)

## What it is

A set of **standalone, non-gameplay debug scripts** (`Assets/Scripts/Procedural/Prototypes/`)
from a design/prototyping pass that answered one question: *when you chop a large spherical
voxel planet into chunks, what chunk shape and coordinate scheme avoids seams at chunk
boundaries?* None of these are used by any real system — they're kept as working reference
implementations because they each demonstrate one conclusion concretely, and the Planet
system's design leans on those conclusions. Exercised standalone in `SampleScene`.

## The core lesson (read this before touching the Planet system)

**Every chunk must sample one shared, world-aligned voxel lattice.** If each chunk instead
invents its own local/rotated coordinate frame, you get visible gaps and overlaps at shared
boundaries — even though each chunk looks fine in isolation. This is proven, not asserted, by
two scripts that are otherwise nearly identical:

- `HexVoxelSphereMeshBuilder.cs` — **deliberately broken.** Independent per-region grids →
  visible seams. Kept as a live "this is what the bug looks like" reference.
- `HexLabeledVoxelSphereMeshBuilder.cs` — **the fix.** One shared lattice; region membership
  is only used to *color/label* voxels after the fact, never to decide where they go. Seamless.

If a future chunk boundary shows gaps, compare against `HexVoxelSphereMeshBuilder.cs` first —
it's almost certainly the same class of bug (a chunk deriving its own local frame instead of
reading from the shared one).

## Other conclusions demonstrated here

- **Square, cube-sphere-aligned chunks are the production choice for voxel geometry**
  (`CubeSphereChunkMeshBuilder.cs`, `CubeSphereChunkDebugVisual.cs`). Treat the sphere as a
  cube with 6 faces, each subdivided into a grid, corners projected onto the sphere by
  normalizing. Chunks tile a shared lattice with **zero boundary-classification cost**:
  "which chunk owns this cell" is a direct formula (dominant axis → face, divide the other two
  components by it → tangent-space `s,t` → floor by chunk size → chunk index). No search
  needed.
- **Hex/pentagon geodesic regions are reserved for a coarse DATA layer only**
  (`GeodesicSphereChunkMeshBuilder.cs`, shared `IcosphereBuilder.cs`) — a
  `FindRegion(direction) → regionId` lookup for biome/elevation decisions, never as the actual
  chunk-mesh boundary. Using them as literal chunk boundaries (the "puzzle piece"
  implementation, see the Planet system) has the same seam problem as above unless fixed the
  same way, and hex chunks don't subdivide cleanly for LOD the way square chunks do. Regions
  also cost an O(regionCount) per-voxel classification that square chunks don't pay.

## Key files

- `IcosphereBuilder.cs` (now under `Shared/`, not `Prototypes/`) — recursively subdivides an
  icosahedron, projecting new vertices onto the unit sphere. Used by both the geodesic-region
  prototype here and by the real Planet region graph.
- `ChunkDebugColorPalette.cs` (`Shared/`) — golden-angle hue stepping so neighboring
  chunks/regions are visually distinguishable in debug views. Used across several prototypes.
- `CubeSphereChunkMeshBuilder.cs` / `*DebugVisual.cs` — square chunk tiling demo.
- `GeodesicSphereChunkMeshBuilder.cs` / `*DebugVisual.cs` — hex/pentagon dual-mesh demo.
- `HexVoxelSphereMeshBuilder.cs` / `*DebugVisual.cs` — the broken seam demo.
- `HexLabeledVoxelSphereMeshBuilder.cs` / `*DebugVisual.cs` — the seam fix demo.

## Known limitations

- Debug-only: flat vertex-color materials, no biome logic, no real chunk streaming/LOD.
- Radial convergence toward the sphere's core is a real singularity (every chunk wedge narrows
  to a point at the center) but is a non-issue as long as only a shallow crust near the outer
  radius is ever voxelized — confirmed via `r_bedrock = planetRadius / chunkSurfaceWidth`
  staying comfortably above zero at realistic planet/chunk ratios.
