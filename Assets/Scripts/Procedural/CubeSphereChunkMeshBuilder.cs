using System.Collections.Generic;
using UnityEngine;

namespace GemstoneColony.Procedural
{
    /// <summary>
    /// Debug visualization: treats the sphere as a cube with 6 faces, each face
    /// subdivided into a grid of quad "chunks", with every corner then projected
    /// onto the sphere by normalizing. This is the naive (uncorrected) cube-to-sphere
    /// mapping, so chunks visibly shrink near face corners and grow near face
    /// centers -- that distortion is the point of this visualization.
    /// Each chunk gets its own unshared vertices and a single flat color.
    /// </summary>
    public static class CubeSphereChunkMeshBuilder
    {
        private static readonly Vector3[] FaceNormals =
        {
            new Vector3(1, 0, 0),
            new Vector3(-1, 0, 0),
            new Vector3(0, 1, 0),
            new Vector3(0, -1, 0),
            new Vector3(0, 0, 1),
            new Vector3(0, 0, -1),
        };

        public static Mesh Build(float radius, int chunksPerFaceEdge)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            int chunkIndex = 0;
            float step = 2f / chunksPerFaceEdge; // each face spans [-1, 1] before projection

            foreach (Vector3 faceNormal in FaceNormals)
            {
                // Same tangent-basis trick as VoxelSphereMeshBuilder's FaceTemplate.
                Vector3 tangent = faceNormal.x != 0 ? Vector3.up : Vector3.right;
                Vector3 bitangent = Vector3.Cross(faceNormal, tangent).normalized;
                tangent = Vector3.Cross(bitangent, faceNormal).normalized;

                for (int i = 0; i < chunksPerFaceEdge; i++)
                {
                    float s0 = -1f + i * step;
                    float s1 = s0 + step;

                    for (int j = 0; j < chunksPerFaceEdge; j++)
                    {
                        float t0 = -1f + j * step;
                        float t1 = t0 + step;

                        Vector3 c0 = ProjectToSphere(faceNormal, tangent, bitangent, s0, t0, radius);
                        Vector3 c1 = ProjectToSphere(faceNormal, tangent, bitangent, s1, t0, radius);
                        Vector3 c2 = ProjectToSphere(faceNormal, tangent, bitangent, s1, t1, radius);
                        Vector3 c3 = ProjectToSphere(faceNormal, tangent, bitangent, s0, t1, radius);

                        Color color = ChunkDebugColorPalette.ForIndex(chunkIndex);
                        chunkIndex++;

                        int startIndex = vertices.Count;
                        vertices.Add(c0);
                        vertices.Add(c1);
                        vertices.Add(c2);
                        vertices.Add(c3);

                        Vector3 flatNormal = Vector3.Cross(c1 - c0, c2 - c0).normalized;
                        for (int k = 0; k < 4; k++)
                        {
                            normals.Add(flatNormal);
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

        private static Vector3 ProjectToSphere(Vector3 normal, Vector3 tangent, Vector3 bitangent, float s, float t, float radius)
        {
            Vector3 cubePoint = normal + tangent * s + bitangent * t;
            return cubePoint.normalized * radius;
        }
    }
}
