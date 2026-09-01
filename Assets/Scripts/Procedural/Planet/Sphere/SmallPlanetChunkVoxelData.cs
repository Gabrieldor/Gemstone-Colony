using UnityEngine;
using GemstoneColony.Procedural;

namespace GemstoneColony.Procedural.Planet
{
    /// <summary>
    /// Per-chunk Information Expert: precomputes voxel type + ownership for every cell in its
    /// AABB. Never reads another chunk's data -- purely a function of the shared config,
    /// region graph, and this chunk's own (faceIndex, chunkI, chunkJ).
    /// </summary>
    public sealed class SmallPlanetChunkVoxelData
    {
        private readonly Vector3Int aabbMin;
        private readonly Vector3Int aabbMax;
        private readonly int sizeY;
        private readonly int sizeZ;
        private readonly TerrainVoxelType[] types;
        private readonly bool[] owned;
        private readonly PlanetRegionGraph.Result regions;

        public Vector3Int Min => aabbMin;
        public Vector3Int Max => aabbMax;

        public SmallPlanetChunkVoxelData(int faceIndex, int chunkI, int chunkJ, PlanetRegionGraph.Result regions)
        {
            this.regions = regions;
            SmallPlanetChunkLayout.GetChunkAabb(faceIndex, chunkI, chunkJ, out aabbMin, out aabbMax);

            int sizeX = aabbMax.x - aabbMin.x + 1;
            sizeY = aabbMax.y - aabbMin.y + 1;
            sizeZ = aabbMax.z - aabbMin.z + 1;
            types = new TerrainVoxelType[sizeX * sizeY * sizeZ];
            owned = new bool[types.Length];

            CubeFace ownFace = CubeFace.All[faceIndex];

            // Seed the hill-climb hint once per chunk (cheap: one brute-force lookup) rather
            // than per cell, so even the first interior cell gets a trustworthy starting point.
            SmallPlanetChunkLayout.GetChunkStRange(chunkI, chunkJ, out float chunkS0, out float chunkS1, out float chunkT0, out float chunkT1);
            Vector3 chunkCenterDirection = ownFace.DirectionAt((chunkS0 + chunkS1) * 0.5f, (chunkT0 + chunkT1) * 0.5f);
            int regionHint = PlanetRegionGraph.FindNearestRegion(chunkCenterDirection, regions.Centers);

            for (int x = aabbMin.x; x <= aabbMax.x; x++)
            {
                for (int y = aabbMin.y; y <= aabbMax.y; y++)
                {
                    for (int z = aabbMin.z; z <= aabbMax.z; z++)
                    {
                        int idx = Index(x, y, z);
                        Vector3 pos = new Vector3(x, y, z) * SmallPlanetConfig.VoxelSize;
                        float dist = pos.magnitude;
                        if (dist < 1f)
                        {
                            types[idx] = TerrainVoxelType.Empty;
                            continue;
                        }

                        // Guaranteed-correct brute-force near a chunk/face boundary (where a
                        // neighboring chunk could independently compute this same physical
                        // cell and must agree); the cheap hill-climb everywhere else, since an
                        // occasional local-optimum miss deep in a chunk's interior only
                        // slightly perturbs a biome boundary's shape locally -- nothing else
                        // ever computes that same interior cell to disagree with. See
                        // SmallPlanetChunkLayout.IsNearChunkBoundary and Docs/Systems/
                        // PlanetSphere.md (the "always brute-force" fix this refines).
                        //
                        // Boundary-proximity is evaluated on the cell's own NATIVE face/chunk
                        // (CubeFace.Classify + that face's own (s,t)), not this chunk's face --
                        // a cell can be genuinely close to a real cross-face boundary while
                        // projecting to an extreme/unstable (s,t) under a non-native face's
                        // tangent-plane. Using the wrong face here caused real type
                        // disagreements at cross-face-edge boundaries during verification.
                        Vector3 direction = pos / dist;
                        int nativeFaceIndex = CubeFace.Classify(direction);
                        CubeFace nativeFace = CubeFace.All[nativeFaceIndex];
                        bool nearBoundary = !nativeFace.TryGetST(direction, out float ns, out float nt);
                        if (!nearBoundary)
                        {
                            int nativeChunkI = SmallPlanetChunkLayout.NativeChunkIndex(ns);
                            int nativeChunkJ = SmallPlanetChunkLayout.NativeChunkIndex(nt);
                            nearBoundary = SmallPlanetChunkLayout.IsNearChunkBoundary(ns, nt, nativeChunkI, nativeChunkJ, SmallPlanetConfig.RegionBoundaryMargin);
                        }

                        regionHint = nearBoundary
                            ? PlanetRegionGraph.FindNearestRegion(direction, regions.Centers)
                            : PlanetRegionGraph.ResolveRegionNear(direction, regions.Centers, regions.Adjacency, regionHint);
                        Biome biome = regions.Biomes[regionHint];

                        TerrainVoxelType type = SmallPlanetVoxelClassifier.TypeAt(x, y, z, biome);
                        types[idx] = type;
                        if (type != TerrainVoxelType.Empty)
                            owned[idx] = SmallPlanetChunkLayout.OwnsCell(direction, faceIndex, chunkI, chunkJ);
                    }
                }
            }
        }

        public TerrainVoxelType TypeAt(int x, int y, int z)
        {
            if (InBounds(x, y, z))
                return types[Index(x, y, z)];

            // Rare fallback for neighbor lookups just outside this chunk's AABB padding.
            Vector3 pos = new Vector3(x, y, z) * SmallPlanetConfig.VoxelSize;
            float dist = pos.magnitude;
            if (dist < 1f)
                return TerrainVoxelType.Empty;

            Vector3 direction = pos / dist;
            int region = PlanetRegionGraph.FindNearestRegion(direction, regions.Centers);
            return SmallPlanetVoxelClassifier.TypeAt(x, y, z, regions.Biomes[region]);
        }

        public bool IsSolid(int x, int y, int z) => TypeAt(x, y, z) != TerrainVoxelType.Empty;

        public bool IsOwned(int x, int y, int z) => InBounds(x, y, z) && owned[Index(x, y, z)];

        private bool InBounds(int x, int y, int z) =>
            x >= aabbMin.x && x <= aabbMax.x &&
            y >= aabbMin.y && y <= aabbMax.y &&
            z >= aabbMin.z && z <= aabbMax.z;

        private int Index(int x, int y, int z) =>
            ((x - aabbMin.x) * sizeY + (y - aabbMin.y)) * sizeZ + (z - aabbMin.z);
    }
}
