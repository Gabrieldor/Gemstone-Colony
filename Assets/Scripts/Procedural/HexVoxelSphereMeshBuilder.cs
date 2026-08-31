using System.Collections.Generic;
using UnityEngine;

namespace GemstoneColony.Procedural
{
    /// <summary>
    /// Debug visualization only -- NOT a recommended production approach. Demonstrates
    /// what happens if hex/pentagon geodesic regions are used directly as voxel chunk
    /// boundaries, instead of only as a coarse data layer above a separate square
    /// voxel-chunk grid.
    ///
    /// Each geodesic region independently builds its own small cubic voxel grid,
    /// oriented to its own local tangent plane, then keeps only the voxels whose
    /// world position tests nearest to that region's center (a per-voxel point-in-
    /// cell check). Because neighboring regions pick their local grid orientation
    /// independently -- there is no shared alignment convention between them -- the
    /// grids are not rotationally aligned, and the nearest-region carving produces a
    /// jagged per-region boundary that does not match its neighbor's jagged boundary.
    /// Expect visible gaps, overlaps, and misaligned voxel "grain" between regions.
    /// </summary>
    public static class HexVoxelSphereMeshBuilder
    {
        private static readonly Vector3Int[] LocalFaceOffsets =
        {
            new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0),
            new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        public static Mesh Build(float radius, int subdivisions, int voxelsPerRegionEdge, int shellThicknessVoxels)
        {
            List<Vector3> regionCenters = IcosphereBuilder.Build(subdivisions).Vertices;
            float[] nearestNeighborDistance = ComputeNearestNeighborDistances(regionCenters);

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            for (int regionIndex = 0; regionIndex < regionCenters.Count; regionIndex++)
            {
                BuildRegionVoxels(
                    regionCenters, nearestNeighborDistance, regionIndex, radius,
                    voxelsPerRegionEdge, shellThicknessVoxels,
                    vertices, normals, colors, triangles);
            }

            var mesh = new Mesh
            {
                indexFormat = vertices.Count > 65000
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16,
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            return mesh;
        }

        private static float[] ComputeNearestNeighborDistances(List<Vector3> centers)
        {
            var distances = new float[centers.Count];
            for (int i = 0; i < centers.Count; i++)
            {
                float best = float.MaxValue;
                for (int j = 0; j < centers.Count; j++)
                {
                    if (i == j) continue;
                    float d = Vector3.Distance(centers[i], centers[j]);
                    if (d < best) best = d;
                }
                distances[i] = best;
            }
            return distances;
        }

        private static void BuildRegionVoxels(
            List<Vector3> regionCenters,
            float[] nearestNeighborDistance,
            int regionIndex,
            float radius,
            int voxelsPerRegionEdge,
            int shellThicknessVoxels,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Color> colors,
            List<int> triangles)
        {
            Vector3 center = regionCenters[regionIndex];

            // Each region derives its own tangent frame independently -- there is no
            // shared convention pulling neighboring regions' grids into alignment.
            // This is the root cause of the seams this class exists to demonstrate.
            Vector3 tangent = Mathf.Abs(center.y) < 0.99f
                ? Vector3.Cross(Vector3.up, center).normalized
                : Vector3.Cross(Vector3.right, center).normalized;
            Vector3 bitangent = Vector3.Cross(center, tangent);
            Vector3 depth = Vector3.Cross(tangent, bitangent);

            // Grid sized to comfortably overshoot the region's true (hex/pentagon)
            // footprint, then carved down to shape below by the nearest-region test.
            float halfExtent = nearestNeighborDistance[regionIndex] * radius * 0.5f * 1.3f;
            float voxelSize = (2f * halfExtent) / voxelsPerRegionEdge;

            var occupied = new bool[voxelsPerRegionEdge, voxelsPerRegionEdge, shellThicknessVoxels];

            for (int gx = 0; gx < voxelsPerRegionEdge; gx++)
            {
                float localX = (gx - (voxelsPerRegionEdge - 1) * 0.5f) * voxelSize;

                for (int gy = 0; gy < voxelsPerRegionEdge; gy++)
                {
                    float localY = (gy - (voxelsPerRegionEdge - 1) * 0.5f) * voxelSize;

                    for (int gz = 0; gz < shellThicknessVoxels; gz++)
                    {
                        float localZ = (gz - (shellThicknessVoxels - 1) * 0.5f) * voxelSize;

                        Vector3 worldPos = center * radius + tangent * localX + bitangent * localY + depth * localZ;
                        int nearestRegion = FindNearestRegion(worldPos.normalized, regionCenters);

                        occupied[gx, gy, gz] = nearestRegion == regionIndex;
                    }
                }
            }

            Color color = ChunkDebugColorPalette.ForIndex(regionIndex);

            for (int gx = 0; gx < voxelsPerRegionEdge; gx++)
            for (int gy = 0; gy < voxelsPerRegionEdge; gy++)
            for (int gz = 0; gz < shellThicknessVoxels; gz++)
            {
                if (!occupied[gx, gy, gz])
                    continue;

                Vector3 cellCenter = center * radius
                    + tangent * ((gx - (voxelsPerRegionEdge - 1) * 0.5f) * voxelSize)
                    + bitangent * ((gy - (voxelsPerRegionEdge - 1) * 0.5f) * voxelSize)
                    + depth * ((gz - (shellThicknessVoxels - 1) * 0.5f) * voxelSize);

                foreach (Vector3Int offset in LocalFaceOffsets)
                {
                    int nx = gx + offset.x;
                    int ny = gy + offset.y;
                    int nz = gz + offset.z;

                    bool neighborSolid =
                        nx >= 0 && nx < voxelsPerRegionEdge &&
                        ny >= 0 && ny < voxelsPerRegionEdge &&
                        nz >= 0 && nz < shellThicknessVoxels &&
                        occupied[nx, ny, nz];

                    if (neighborSolid)
                        continue;

                    Vector3 faceNormal = tangent * offset.x + bitangent * offset.y + depth * offset.z;
                    Vector3 seed = offset.x != 0 ? depth : tangent;
                    Vector3 faceBitangent = Vector3.Cross(faceNormal, seed).normalized;
                    Vector3 faceTangent = Vector3.Cross(faceBitangent, faceNormal).normalized;

                    AppendFace(vertices, normals, colors, triangles, cellCenter, faceNormal, faceTangent, faceBitangent, voxelSize, color);
                }
            }
        }

        private static int FindNearestRegion(Vector3 direction, List<Vector3> regionCenters)
        {
            int best = 0;
            float bestDot = float.NegativeInfinity;
            for (int i = 0; i < regionCenters.Count; i++)
            {
                float dot = Vector3.Dot(direction, regionCenters[i]);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = i;
                }
            }
            return best;
        }

        private static void AppendFace(
            List<Vector3> vertices, List<Vector3> normals, List<Color> colors, List<int> triangles,
            Vector3 cellCenter, Vector3 normal, Vector3 faceTangent, Vector3 faceBitangent, float voxelSize, Color color)
        {
            float half = voxelSize * 0.5f;
            Vector3 faceCenter = cellCenter + normal * half;

            Vector3 v0 = faceCenter - faceTangent * half - faceBitangent * half;
            Vector3 v1 = faceCenter + faceTangent * half - faceBitangent * half;
            Vector3 v2 = faceCenter + faceTangent * half + faceBitangent * half;
            Vector3 v3 = faceCenter - faceTangent * half + faceBitangent * half;

            int startIndex = vertices.Count;
            vertices.Add(v0);
            vertices.Add(v1);
            vertices.Add(v2);
            vertices.Add(v3);

            for (int i = 0; i < 4; i++)
            {
                normals.Add(normal);
                colors.Add(color);
            }

            triangles.Add(startIndex);
            triangles.Add(startIndex + 1);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 3);
        }
    }
}
