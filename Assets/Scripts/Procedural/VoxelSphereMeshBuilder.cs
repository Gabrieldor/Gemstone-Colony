using System.Collections.Generic;
using UnityEngine;

namespace GemstoneColony.Procedural
{
    /// <summary>
    /// Generates a single merged mesh representing a solid sphere built from cubic voxels.
    /// Only faces exposed to empty space are emitted, so triangle count scales with the
    /// sphere's surface area rather than its volume.
    /// </summary>
    public static class VoxelSphereMeshBuilder
    {
        private static readonly Vector3Int[] FaceDirections =
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(0, -1, 0),
            new Vector3Int(0, 0, 1),
            new Vector3Int(0, 0, -1),
        };

        public static Mesh Build(float voxelSize, int radiusInVoxels)
        {
            var solidCells = CollectSolidCells(radiusInVoxels);

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            foreach (var cell in solidCells)
            {
                foreach (var direction in FaceDirections)
                {
                    if (solidCells.Contains(cell + direction))
                        continue;

                    AppendFace(vertices, normals, uvs, triangles, cell, direction, voxelSize);
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
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        private static HashSet<Vector3Int> CollectSolidCells(int radiusInVoxels)
        {
            var cells = new HashSet<Vector3Int>();
            float radiusSqr = radiusInVoxels * radiusInVoxels;

            for (int x = -radiusInVoxels; x <= radiusInVoxels; x++)
            {
                for (int y = -radiusInVoxels; y <= radiusInVoxels; y++)
                {
                    for (int z = -radiusInVoxels; z <= radiusInVoxels; z++)
                    {
                        if (x * x + y * y + z * z <= radiusSqr)
                            cells.Add(new Vector3Int(x, y, z));
                    }
                }
            }

            return cells;
        }

        private static void AppendFace(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3Int cell,
            Vector3Int direction,
            float voxelSize)
        {
            Vector3 center = new Vector3(cell.x, cell.y, cell.z) * voxelSize;
            Vector3 normal = direction;
            float half = voxelSize * 0.5f;

            Vector3 tangent = direction.x != 0 ? Vector3.up : Vector3.right;
            Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;
            tangent = Vector3.Cross(bitangent, normal).normalized;

            Vector3 faceCenter = center + normal * half;
            Vector3 v0 = faceCenter - tangent * half - bitangent * half;
            Vector3 v1 = faceCenter + tangent * half - bitangent * half;
            Vector3 v2 = faceCenter + tangent * half + bitangent * half;
            Vector3 v3 = faceCenter - tangent * half + bitangent * half;

            int startIndex = vertices.Count;
            vertices.Add(v0);
            vertices.Add(v1);
            vertices.Add(v2);
            vertices.Add(v3);

            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);

            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(0f, 1f));

            triangles.Add(startIndex);
            triangles.Add(startIndex + 1);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 3);
        }
    }
}
