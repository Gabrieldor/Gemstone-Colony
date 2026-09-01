using System.Collections.Generic;
using UnityEngine;

namespace GemstoneColony.Procedural
{
    /// <summary>
    /// Debug visualization: proves the fix for HexVoxelSphereMeshBuilder's seams.
    /// Voxel geometry comes from a single shared, axis-aligned lattice (a shell
    /// between an inner and outer radius, same analytic-occupancy technique as
    /// VoxelSphereMeshBuilder) -- so every voxel is generated exactly once, and
    /// gaps/overlaps between "chunks" are geometrically impossible. The hex/pentagon
    /// region graph is used only to LABEL each voxel (for coloring here; biome data
    /// in a real system) by nearest region center -- it never decides which voxels
    /// exist, unlike HexVoxelSphereMeshBuilder.
    /// </summary>
    public static class HexLabeledVoxelSphereMeshBuilder
    {
        private readonly struct FaceTemplate
        {
            public readonly Vector3Int Direction;
            public readonly Vector3 Normal;
            public readonly Vector3 Corner0;
            public readonly Vector3 Corner1;
            public readonly Vector3 Corner2;
            public readonly Vector3 Corner3;

            public FaceTemplate(Vector3Int direction)
            {
                Direction = direction;
                Normal = direction;

                Vector3 tangent = direction.x != 0 ? Vector3.up : Vector3.right;
                Vector3 bitangent = Vector3.Cross(Normal, tangent).normalized;
                tangent = Vector3.Cross(bitangent, Normal).normalized;

                Vector3 faceCenter = Normal * 0.5f;
                Corner0 = faceCenter - tangent * 0.5f - bitangent * 0.5f;
                Corner1 = faceCenter + tangent * 0.5f - bitangent * 0.5f;
                Corner2 = faceCenter + tangent * 0.5f + bitangent * 0.5f;
                Corner3 = faceCenter - tangent * 0.5f + bitangent * 0.5f;
            }
        }

        private static readonly FaceTemplate[] FaceTemplates =
        {
            new FaceTemplate(new Vector3Int(1, 0, 0)),
            new FaceTemplate(new Vector3Int(-1, 0, 0)),
            new FaceTemplate(new Vector3Int(0, 1, 0)),
            new FaceTemplate(new Vector3Int(0, -1, 0)),
            new FaceTemplate(new Vector3Int(0, 0, 1)),
            new FaceTemplate(new Vector3Int(0, 0, -1)),
        };

        public static Mesh Build(float voxelSize, int outerRadiusInVoxels, int shellThicknessVoxels, int regionSubdivisions)
        {
            List<Vector3> regionCenters = IcosphereBuilder.Build(regionSubdivisions).Vertices;

            int outerRadiusSqr = outerRadiusInVoxels * outerRadiusInVoxels;
            int innerRadius = Mathf.Max(0, outerRadiusInVoxels - shellThicknessVoxels);
            int innerRadiusSqr = innerRadius * innerRadius;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            for (int x = -outerRadiusInVoxels; x <= outerRadiusInVoxels; x++)
            {
                int xSqr = x * x;

                for (int y = -outerRadiusInVoxels; y <= outerRadiusInVoxels; y++)
                {
                    int xySqr = xSqr + y * y;
                    if (xySqr > outerRadiusSqr)
                        continue;

                    for (int z = -outerRadiusInVoxels; z <= outerRadiusInVoxels; z++)
                    {
                        int distSqr = xySqr + z * z;
                        if (!IsInShell(distSqr, innerRadiusSqr, outerRadiusSqr))
                            continue;

                        Color color = default;
                        bool colorResolved = false;

                        for (int i = 0; i < FaceTemplates.Length; i++)
                        {
                            FaceTemplate template = FaceTemplates[i];
                            int nx = x + template.Direction.x;
                            int ny = y + template.Direction.y;
                            int nz = z + template.Direction.z;
                            int neighborDistSqr = nx * nx + ny * ny + nz * nz;

                            if (IsInShell(neighborDistSqr, innerRadiusSqr, outerRadiusSqr))
                                continue; // neighbor solid too -> face hidden

                            if (!colorResolved)
                            {
                                // Resolved once per cell (not per face) -- the region
                                // graph only labels this already-decided voxel.
                                Vector3 direction = new Vector3(x, y, z).normalized;
                                int region = FindNearestRegion(direction, regionCenters);
                                color = ChunkDebugColorPalette.ForIndex(region);
                                colorResolved = true;
                            }

                            AppendFace(vertices, normals, colors, triangles, x, y, z, template, voxelSize, color);
                        }
                    }
                }
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

        private static bool IsInShell(int distSqr, int innerRadiusSqr, int outerRadiusSqr)
        {
            return distSqr <= outerRadiusSqr && distSqr >= innerRadiusSqr;
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
            int x, int y, int z, FaceTemplate template, float voxelSize, Color color)
        {
            Vector3 center = new Vector3(x, y, z) * voxelSize;

            int startIndex = vertices.Count;
            vertices.Add(center + template.Corner0 * voxelSize);
            vertices.Add(center + template.Corner1 * voxelSize);
            vertices.Add(center + template.Corner2 * voxelSize);
            vertices.Add(center + template.Corner3 * voxelSize);

            for (int i = 0; i < 4; i++)
            {
                normals.Add(template.Normal);
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
