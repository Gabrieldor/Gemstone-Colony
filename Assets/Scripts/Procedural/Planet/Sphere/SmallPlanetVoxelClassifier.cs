using UnityEngine;
using GemstoneColony.Procedural;

namespace GemstoneColony.Procedural.Planet
{
    /// <summary>
    /// The one shared "what's at cell (x,y,z)" rule every chunk calls identically -- a pure
    /// function of the cell + biome, reading only SmallPlanetConfig/SmallBiomeCatalog/
    /// SmallPlanetTerrainSampler. Two chunks always agree on a shared boundary cell because
    /// neither depends on chunk-local state.
    /// </summary>
    public static class SmallPlanetVoxelClassifier
    {
        public static TerrainVoxelType TypeAt(int x, int y, int z, Biome biome)
        {
            Vector3 pos = new Vector3(x, y, z) * SmallPlanetConfig.VoxelSize;
            float dist = pos.magnitude;
            if (dist < 1f)
                return TerrainVoxelType.Empty; // guards direction normalize; below bedrock anyway at this demo's scale

            // Below r_bedrock: deliberately left blank, regardless of biome. See
            // SmallPlanetConfig.BedrockRadiusVoxels.
            if (dist < SmallPlanetConfig.BedrockRadiusVoxels)
                return TerrainVoxelType.Empty;

            Vector3 direction = pos / dist;
            float outer = SmallPlanetTerrainSampler.OuterRadius(direction, biome);

            // Crust always spans [BedrockRadiusVoxels, outer] -- a fixed floor, not a fixed
            // thickness relative to the local terrain surface.
            if (dist <= outer)
                return TerrainVoxelType.Ground;

            if (SmallBiomeCatalog.For(biome).HasWater && dist <= SmallPlanetConfig.SeaLevelRadiusVoxels)
                return TerrainVoxelType.Water;

            return TerrainVoxelType.Empty;
        }
    }
}
