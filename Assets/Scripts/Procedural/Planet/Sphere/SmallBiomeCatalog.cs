using System.Collections.Generic;
using UnityEngine;

namespace GemstoneColony.Procedural.Planet
{
    /// <summary>
    /// Per-biome terrain shaping for the small demo sphere (32-voxel base radius) -- a single
    /// lookup table, not duplicated per chunk, so every chunk agrees on a given biome's shape.
    /// </summary>
    public static class SmallBiomeCatalog
    {
        private static readonly Dictionary<Biome, BiomeShapeParams> Table = new Dictionary<Biome, BiomeShapeParams>
        {
            { Biome.Mountain, new BiomeShapeParams(baseRadiusOffset: 1f, heightAmplitude: 3f, hasWater: false) },
            { Biome.Plains, new BiomeShapeParams(baseRadiusOffset: 0f, heightAmplitude: 1f, hasWater: false) },
            { Biome.Basin, new BiomeShapeParams(baseRadiusOffset: -2f, heightAmplitude: 1f, hasWater: true) },
        };

        public static BiomeShapeParams For(Biome biome) => Table[biome];

        // Used to size chunk AABBs loosely enough to contain every cell any chunk could
        // possibly own or need as a face-culling neighbor, across every biome. There's no
        // per-biome minimum radius counterpart -- the crust's inner bound is the fixed
        // SmallPlanetConfig.BedrockRadiusVoxels floor, the same for every biome.
        public static float MaxPossibleOuterRadius(float baseRadius)
        {
            float max = float.NegativeInfinity;
            foreach (var entry in Table)
                max = Mathf.Max(max, baseRadius + entry.Value.BaseRadiusOffset + entry.Value.HeightAmplitude);
            return max;
        }
    }
}
