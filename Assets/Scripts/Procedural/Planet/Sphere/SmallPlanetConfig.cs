namespace GemstoneColony.Procedural.Planet
{
    /// <summary>
    /// Single shared source of truth for the small demo sphere's planet-wide constants. Every
    /// chunk must derive its voxel positions and terrain shape from these same values via the
    /// same formula -- that agreement is what makes independently-generated chunks tile
    /// seamlessly.
    /// </summary>
    public static class SmallPlanetConfig
    {
        public const float VoxelSize = 1f;
        public const float BaseRadiusVoxels = 32f;
        public const float SeaLevelRadiusVoxels = 31f;
        public const float NoiseFrequency = 5f;

        public const int RegionSubdivisions = 1; // 42 regions -- plenty of visible variety on a 32-voxel sphere

        public const int ChunksPerFaceEdge = 4;
        public const float ChunkStWidth = 2f / ChunksPerFaceEdge;

        // Physical voxel width of one chunk at the surface (approximated at the face center,
        // where tangent-plane distortion is minimal: width ~= radius * angular chunk width).
        public const float ChunkSurfaceWidthVoxels = ChunkStWidth * BaseRadiusVoxels;

        // r_bedrock (see Docs/GDD.md "Background & validated decisions"): every chunk covers a
        // fixed ANGULAR slice, so its physical width shrinks linearly toward the planet's
        // center. Below this radius, a chunk's wedge would be under 1 voxel wide -- the
        // cube-face-chunk scheme itself becomes geometrically degenerate, not just impractical
        // to render. The crust always spans [BedrockRadiusVoxels, outer] for every direction
        // (a fixed floor, not a fixed thickness relative to local terrain height -- a deep
        // mountain and a shallow basin both bottom out at the same absolute radius, the way
        // real planets and Minecraft's bedrock layer both work). Everything below is
        // deliberately left unvoxelized (Empty) -- there is nothing to generate there, and
        // nothing should ever try.
        public const float BedrockRadiusVoxels = BaseRadiusVoxels / ChunkSurfaceWidthVoxels;

        // How close (in s,t), on a cell's own NATIVE face/chunk, it needs to be to that
        // chunk's boundary before its region lookup gets the guaranteed-correct brute-force
        // scan instead of the cheap hill-climb -- see SmallPlanetChunkLayout.
        // IsNearChunkBoundary and SmallPlanetChunkVoxelData for why "native face" matters.
        // This value is empirically verified, not derived: an exhaustive scan across all 96
        // chunks (732 chunk pairs with any AABB overlap) found 0 type disagreements at this
        // margin, giving an 82%/18% interior/near-boundary split (down from 100% brute-force
        // before this optimization existed). A naive "fraction of chunk width" estimate was
        // tried first and was wrong by a wide margin -- the tangent-plane projection distorts
        // non-uniformly near a face's own edge, so don't rederive this from chunk width
        // without re-running that exhaustive check (see Docs/Systems/PlanetSphere.md).
        public const float RegionBoundaryMargin = 0.02f;
    }
}
