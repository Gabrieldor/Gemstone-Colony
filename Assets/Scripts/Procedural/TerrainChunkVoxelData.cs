using UnityEngine;

namespace GemstoneColony.Procedural
{
    /// <summary>
    /// Generates a per-cell voxel-type grid for one terrain chunk: a 2D Perlin-noise
    /// heightmap decides how tall each column's ground is, and any column shorter than
    /// waterLevel gets filled with water up to that level. Pure data -- no mesh/rendering
    /// knowledge, so it can be reused by any consumer that just needs "what's at (x,y,z)".
    /// </summary>
    public sealed class TerrainChunkVoxelData
    {
        public int Width { get; }
        public int Height { get; }
        public int Depth { get; }

        private readonly TerrainVoxelType[] cells;

        public TerrainChunkVoxelData(
            int width,
            int height,
            int depth,
            float noiseScale,
            float heightBase,
            float heightAmplitude,
            int waterLevel,
            int seed)
        {
            Width = width;
            Height = height;
            Depth = depth;
            cells = new TerrainVoxelType[width * height * depth];

            // Perlin noise repeats/mirrors near the origin and at integer coordinates, so
            // seed just picks a different, unrelated sampling region instead of reseeding
            // a PRNG.
            var random = new System.Random(seed);
            float offsetX = (float)random.NextDouble() * 10000f;
            float offsetZ = (float)random.NextDouble() * 10000f;

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    float noise = Mathf.PerlinNoise(
                        offsetX + x * noiseScale,
                        offsetZ + z * noiseScale);
                    int columnHeight = Mathf.Clamp(
                        Mathf.RoundToInt(heightBase + noise * heightAmplitude),
                        0,
                        height - 1);

                    for (int y = 0; y < height; y++)
                    {
                        TerrainVoxelType type;
                        if (y <= columnHeight)
                            type = TerrainVoxelType.Ground;
                        else if (y <= waterLevel)
                            type = TerrainVoxelType.Water;
                        else
                            type = TerrainVoxelType.Empty;

                        cells[Index(x, y, z)] = type;
                    }
                }
            }
        }

        public TerrainVoxelType TypeAt(int x, int y, int z)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height || z < 0 || z >= Depth)
                return TerrainVoxelType.Empty;

            return cells[Index(x, y, z)];
        }

        public bool IsSolid(int x, int y, int z) => TypeAt(x, y, z) != TerrainVoxelType.Empty;

        private int Index(int x, int y, int z) => (x * Height + y) * Depth + z;
    }
}
