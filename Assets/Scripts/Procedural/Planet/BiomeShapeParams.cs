namespace GemstoneColony.Procedural.Planet
{
    /// <summary>
    /// Per-biome terrain shaping parameters (radius offset, height noise amplitude, water).
    /// Plain data -- values are inherently tied to a specific planet radius, so each
    /// planet-scale demo defines its own catalog of these (see SmallBiomeCatalog) rather than
    /// sharing one table; this type just avoids redeclaring the same 3-field struct per demo.
    /// </summary>
    public readonly struct BiomeShapeParams
    {
        public readonly float BaseRadiusOffset;
        public readonly float HeightAmplitude;
        public readonly bool HasWater;

        public BiomeShapeParams(float baseRadiusOffset, float heightAmplitude, bool hasWater)
        {
            BaseRadiusOffset = baseRadiusOffset;
            HeightAmplitude = heightAmplitude;
            HasWater = hasWater;
        }
    }
}
