using UnityEngine;

namespace GemstoneColony.Procedural.Planet
{
    /// <summary>
    /// Turns a world direction + biome into an outer terrain radius. Pure function of
    /// direction (no per-chunk seed/offset), so shared-boundary cells always agree.
    /// </summary>
    public static class SmallPlanetTerrainSampler
    {
        public static float OuterRadius(Vector3 direction, Biome biome)
        {
            BiomeShapeParams p = SmallBiomeCatalog.For(biome);
            float signedNoise = Sample3DNoise(direction * SmallPlanetConfig.NoiseFrequency) * 2f - 1f;
            return SmallPlanetConfig.BaseRadiusVoxels + p.BaseRadiusOffset + p.HeightAmplitude * signedNoise;
        }

        private static float Sample3DNoise(Vector3 p)
        {
            float xy = Mathf.PerlinNoise(p.x, p.y);
            float yz = Mathf.PerlinNoise(p.y, p.z);
            float zx = Mathf.PerlinNoise(p.z, p.x);
            return (xy + yz + zx) / 3f;
        }
    }
}
