# VoxelSphereMeshBuilder

## What it is

A decorative solid sphere made of cubic voxels, standing next to/under the character in
`SampleScene`. Purely cosmetic — proves "build a solid shape out of cubes" works before any
terrain/planet system existed. Not connected to gameplay.

## How it works

1. **Pick which grid cells are solid.** Loop over every integer `(x, y, z)` cell in a cube
   bounding box of the requested radius; a cell is solid if `x²+y²+z² <= radius²` (a digital
   ball). Stored in a `HashSet<Vector3Int>` for O(1) neighbor lookups.
2. **Face culling.** For every solid cell, check its 6 axis-neighbors. Only emit a quad for a
   face whose neighbor cell is *not* solid — internal faces between two solid cells are never
   seen, so skipping them keeps triangle count proportional to surface area, not volume (a
   radius-22 sphere is ~44,600 solid cells but only ~18,200 triangles).
3. **One merged mesh, one draw call.** All faces go into a single `Mesh` with unshared
   vertices per face (flat per-face normals → the blocky "voxel" look, not smooth shading).

## Key files

- `VoxelSphereMeshBuilder.cs` — the whole thing; a static `Build(voxelSize, radiusInVoxels)`
  method, no MonoBehaviour, no scene knowledge.

## Gotchas / invariants

- **Triangle winding matters and is easy to get backwards.** A face's winding order must put
  its outward normal on the correct side per Unity's front-face convention, or it renders
  from the inside-out (looks fine in isolation, but fails to occlude anything placed inside
  it). Verify winding with an actual visibility test (render a single extracted face and check
  a pixel), not by reasoning about cross-product handedness abstractly — that reasoning is a
  known trap here (see the Planet system's own hard-won lesson on regenerating mesh *assets*,
  below).
- If you ever need to regenerate this mesh's saved `.asset` in the Editor, **delete and
  recreate the asset** rather than `EditorUtility.CopySerialized` onto the existing one — the
  latter was found (via extensive debugging) to silently corrupt something that breaks
  depth-testing against other objects while still rendering its own color and passing physics
  raycasts correctly. Very hard to detect except by testing actual occlusion.

## Known limitations

- Radius/voxel size are fixed at generation time (Editor-time, not runtime-tunable via
  Inspector like the Terrain/Planet systems).
- No biome/coloring — flat gray material only.
