using System.Collections.Generic;
using UnityEngine;

namespace GemstoneColony.Procedural
{
    /// <summary>
    /// Builds a single merged mesh for one voxel type within a TerrainChunkVoxelData grid.
    /// Only faces exposed to a non-solid neighbor (empty, or outside the chunk) are emitted,
    /// and vertices are never shared between faces so each face gets flat per-face shading.
    /// </summary>
    public static class TerrainChunkMeshBuilder
    {
        private readonly struct FaceTemplate
        {
            public readonly Vector3Int Direction;
            public readonly Vector3 Normal;
            public readonly Vector4 Tangent;
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
                Tangent = new Vector4(tangent.x, tangent.y, tangent.z, 1f);

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

        private static readonly Vector2[] FaceUVs =
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
        };

        public static Mesh Build(TerrainChunkVoxelData data, TerrainVoxelType typeToBuild, float voxelSize)
        {
            // Rough capacity guess (top+bottom coverage of the footprint) just to cut down
            // on List reallocations -- being off doesn't cost correctness, just a bit of
            // regrowth, same tradeoff VoxelSphereMeshBuilder makes.
            int estimatedFaces = Mathf.Max(64, data.Width * data.Depth * 2);

            var vertices = new List<Vector3>(estimatedFaces * 4);
            var normals = new List<Vector3>(estimatedFaces * 4);
            var tangents = new List<Vector4>(estimatedFaces * 4);
            var uvs = new List<Vector2>(estimatedFaces * 4);
            var triangles = new List<int>(estimatedFaces * 6);

            for (int x = 0; x < data.Width; x++)
            {
                for (int y = 0; y < data.Height; y++)
                {
                    for (int z = 0; z < data.Depth; z++)
                    {
                        if (data.TypeAt(x, y, z) != typeToBuild)
                            continue;

                        for (int i = 0; i < FaceTemplates.Length; i++)
                        {
                            FaceTemplate template = FaceTemplates[i];
                            int nx = x + template.Direction.x;
                            int ny = y + template.Direction.y;
                            int nz = z + template.Direction.z;

                            if (data.IsSolid(nx, ny, nz))
                                continue; // neighbor covers this face, it will never be seen

                            AppendFace(vertices, normals, tangents, uvs, triangles, x, y, z, template, voxelSize);
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
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            return mesh;
        }

        private static void AppendFace(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector4> tangents,
            List<Vector2> uvs,
            List<int> triangles,
            int x,
            int y,
            int z,
            FaceTemplate template,
            float voxelSize)
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
                tangents.Add(template.Tangent);
                uvs.Add(FaceUVs[i]);
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
