using System.Collections.Generic;
using UnityEngine;
using GemstoneColony.Procedural;

namespace GemstoneColony.Procedural.Planet
{
    /// <summary>
    /// Builds a merged mesh for one chunk's voxel type. Emits a face only for cells this
    /// chunk owns, but decides face exposure from global solidity (not ownership) -- an owned
    /// solid cell next to an unowned-but-solid neighbor still gets that face suppressed, since
    /// the neighbor chunk will render it. This is what makes independent generation tile
    /// seamlessly.
    /// </summary>
    public static class SmallPlanetChunkMeshBuilder
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

        public static Mesh Build(SmallPlanetChunkVoxelData data, TerrainVoxelType typeToBuild)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            Vector3Int min = data.Min;
            Vector3Int max = data.Max;

            for (int x = min.x; x <= max.x; x++)
            {
                for (int y = min.y; y <= max.y; y++)
                {
                    for (int z = min.z; z <= max.z; z++)
                    {
                        if (!data.IsOwned(x, y, z) || data.TypeAt(x, y, z) != typeToBuild)
                            continue;

                        for (int i = 0; i < FaceTemplates.Length; i++)
                        {
                            FaceTemplate template = FaceTemplates[i];
                            int nx = x + template.Direction.x;
                            int ny = y + template.Direction.y;
                            int nz = z + template.Direction.z;

                            if (data.IsSolid(nx, ny, nz))
                                continue;

                            AppendFace(vertices, normals, tangents, uvs, triangles, x, y, z, template);
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
            mesh.RecalculateBounds();
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
            FaceTemplate template)
        {
            Vector3 center = new Vector3(x, y, z) * SmallPlanetConfig.VoxelSize;

            int startIndex = vertices.Count;
            vertices.Add(center + template.Corner0 * SmallPlanetConfig.VoxelSize);
            vertices.Add(center + template.Corner1 * SmallPlanetConfig.VoxelSize);
            vertices.Add(center + template.Corner2 * SmallPlanetConfig.VoxelSize);
            vertices.Add(center + template.Corner3 * SmallPlanetConfig.VoxelSize);

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
