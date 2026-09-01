using UnityEngine;

namespace GemstoneColony.Procedural.Planet
{
    /// <summary>
    /// Chunk grid geometry for the small demo's full 6-face coverage: which (face, s, t)
    /// rectangle each chunk owns. Every chunk tiles its face's whole [-1, 1] range, and
    /// ownership must also disambiguate BETWEEN faces at cube edges/corners -- that's what
    /// CubeFace.Classify is for, checked before the (s, t)-range test.
    /// </summary>
    public static class SmallPlanetChunkLayout
    {
        public static void GetChunkStRange(int chunkI, int chunkJ, out float s0, out float s1, out float t0, out float t1)
        {
            s0 = -1f + chunkI * SmallPlanetConfig.ChunkStWidth;
            s1 = s0 + SmallPlanetConfig.ChunkStWidth;
            t0 = -1f + chunkJ * SmallPlanetConfig.ChunkStWidth;
            t1 = t0 + SmallPlanetConfig.ChunkStWidth;
        }

        // Is (s, t) within a thin `margin`-wide band straddling this chunk's own ownership
        // rectangle edge (just inside OR just outside, not arbitrarily far outside)? Chunks
        // tile their face edge-to-edge, and faces tile edge-to-edge with each other, so every
        // chunk-to-chunk AND face-to-face adjacency in the whole tiling shows up as "near this
        // chunk's own boundary" -- one test covers both cases. Used to decide whether a cell's
        // region lookup needs the guaranteed-correct brute-force scan (near a boundary another
        // chunk could also compute) or can use the cheap hill-climb (either safely interior,
        // or far enough outside the rectangle that it's just AABB slack no chunk actually
        // owns or depends on -- nothing meaningfully computes that same cell to disagree with).
        //
        // IMPORTANT: (s, t) must come from the cell's OWN native face (CubeFace.Classify(dir)
        // + that face's TryGetST), not from whichever chunk happens to be asking. A cell can
        // fall well outside an asking chunk's own face-relative domain (s or t far past +-1,
        // where the tangent-plane projection is numerically extreme/unstable), even while
        // being genuinely close to a real cross-face boundary -- using a non-native face's
        // (s, t) here previously caused real cross-face-edge type disagreements. See
        // NativeChunkIndex below for computing the native (chunkI, chunkJ) for given (s, t).
        public static bool IsNearChunkBoundary(float s, float t, int chunkI, int chunkJ, float margin)
        {
            GetChunkStRange(chunkI, chunkJ, out float s0, out float s1, out float t0, out float t1);
            bool withinPaddedRect = s >= s0 - margin && s < s1 + margin && t >= t0 - margin && t < t1 + margin;
            if (!withinPaddedRect)
                return false;

            return (s - s0) < margin || (s1 - s) < margin || (t - t0) < margin || (t1 - t) < margin;
        }

        // Which chunk (s, t) natively falls into on its own face, clamped for the s=1/t=1 edge
        // case (floating point can land exactly on the upper bound).
        public static int NativeChunkIndex(float st)
        {
            int i = Mathf.FloorToInt((st + 1f) / SmallPlanetConfig.ChunkStWidth);
            return Mathf.Clamp(i, 0, SmallPlanetConfig.ChunksPerFaceEdge - 1);
        }

        // Uses NativeChunkIndex (clamped floor), not a raw half-open [s0,s1) range comparison.
        // The raw range test has a real gap: every INTERNAL grid line is fine (the next
        // chunk's inclusive s>=s0 catches it), but the face's own outer edge (s or t exactly
        // +-1) has no "next chunk" to catch it via a lower-bound test -- the last chunk's
        // range is exclusive at its upper end, so that exact value belongs to NO chunk. This
        // isn't a rare float coincidence: for integer voxel coordinates, s or t lands exactly
        // on a whole-face boundary whenever two of the direction's axis magnitudes are exactly
        // equal (common near diagonals/corners, e.g. cell (18,18,-18)), producing a real,
        // visible strip of unowned-by-anyone cells. Found via a direct scan near a cube
        // corner: 13/169 sampled directions had zero owners across all 96 chunks. See
        // Docs/Systems/PlanetSphere.md.
        public static bool OwnsCell(Vector3 direction, int faceIndex, int chunkI, int chunkJ)
        {
            if (CubeFace.Classify(direction) != faceIndex)
                return false;

            CubeFace face = CubeFace.All[faceIndex];
            if (!face.TryGetST(direction, out float s, out float t))
                return false;

            return NativeChunkIndex(s) == chunkI && NativeChunkIndex(t) == chunkJ;
        }

        public static void GetChunkAabb(int faceIndex, int chunkI, int chunkJ, out Vector3Int min, out Vector3Int max)
        {
            GetChunkStRange(chunkI, chunkJ, out float s0, out float s1, out float t0, out float t1);
            CubeFace face = CubeFace.All[faceIndex];

            float maxOuter = SmallBiomeCatalog.MaxPossibleOuterRadius(SmallPlanetConfig.BaseRadiusVoxels);
            float maxRadius = Mathf.Max(maxOuter, SmallPlanetConfig.SeaLevelRadiusVoxels) + 1f;
            float minRadius = SmallPlanetConfig.BedrockRadiusVoxels - 1f;

            Vector3 lo = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 hi = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            float[] sCorners = { s0, s1, s1, s0 };
            float[] tCorners = { t0, t0, t1, t1 };
            float[] radii = { minRadius, maxRadius };

            for (int c = 0; c < 4; c++)
            {
                Vector3 dir = face.DirectionAt(sCorners[c], tCorners[c]);
                for (int r = 0; r < radii.Length; r++)
                {
                    Vector3 p = dir * radii[r];
                    lo = Vector3.Min(lo, p);
                    hi = Vector3.Max(hi, p);
                }
            }

            min = new Vector3Int(Mathf.FloorToInt(lo.x) - 1, Mathf.FloorToInt(lo.y) - 1, Mathf.FloorToInt(lo.z) - 1);
            max = new Vector3Int(Mathf.CeilToInt(hi.x) + 1, Mathf.CeilToInt(hi.y) + 1, Mathf.CeilToInt(hi.z) + 1);
        }
    }
}
