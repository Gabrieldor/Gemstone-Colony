using System.Collections.Generic;
using UnityEngine;

namespace GemstoneColony.Procedural
{
    /// <summary>
    /// Generates a single merged mesh representing a solid sphere built from cubic voxels.
    /// Only faces exposed to empty space are emitted, so triangle count scales with the
    /// sphere's surface area rather than its volume. Voxel occupancy is analytic
    /// (x^2 + y^2 + z^2 &lt;= r^2), so no per-cell set is built or queried.
    /// </summary>
    public static class VoxelSphereMeshBuilder
    {
        // All 6 cube faces are axis-aligned, so their normal/tangent/corner geometry only
        // has 6 possible values, no matter how many voxels are in the sphere. We compute
        // each one exactly once here (in the static array below) instead of recomputing
        // it, per face, every time AppendFace runs.
        private readonly struct FaceTemplate
        {
            public readonly Vector3Int Direction;   // which of the 6 neighbor cells this face faces
            public readonly Vector3 Normal;         // same as Direction, as a float vector
            public readonly Vector4 Tangent;         // tangent-space basis vector, for shaders that sample normal maps
            public readonly Vector3 Corner0;         // the 4 corners of this face, as offsets from
            public readonly Vector3 Corner1;         // the voxel's center, assuming a voxel size of 1.
            public readonly Vector3 Corner2;         // Build() scales these by the real voxel size later.
            public readonly Vector3 Corner3;

            public FaceTemplate(Vector3Int direction)
            {
                Direction = direction;
                Normal = direction;

                // Build an orthonormal basis (tangent, bitangent, normal) for this face.
                // "up" is an arbitrary starting vector that's never parallel to the normal
                // for a side face; Cross/Cross re-derives a proper right-angle tangent from it.
                Vector3 tangent = direction.x != 0 ? Vector3.up : Vector3.right;
                Vector3 bitangent = Vector3.Cross(Normal, tangent).normalized;
                tangent = Vector3.Cross(bitangent, Normal).normalized;
                Tangent = new Vector4(tangent.x, tangent.y, tangent.z, 1f);

                // The face is a unit square sitting on the +Normal side of the voxel,
                // spanned by tangent/bitangent. Corners go around it in UV winding order
                // (0,0) -> (1,0) -> (1,1) -> (0,1) to match FaceUVs below.
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

        // Matches the Corner0..Corner3 winding order in FaceTemplate.
        private static readonly Vector2[] FaceUVs =
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
        };

        public static Mesh Build(float voxelSize, int radiusInVoxels)
        {
            int radiusSqr = radiusInVoxels * radiusInVoxels;

            // Empirical average of ~3.2 exposed faces per surface voxel on a voxelized
            // sphere; surface voxel count is ~4*pi*r^2, giving ~6*pi*r^2 total faces.
            // This is just a starting List capacity guess to avoid reallocations as we
            // fill the lists below — being a bit over is cheap, being under just means
            // a normal List growth happens like before.
            int estimatedFaces = Mathf.Max(24, Mathf.CeilToInt(6f * Mathf.PI * radiusSqr));

            var vertices = new List<Vector3>(estimatedFaces * 4);
            var normals = new List<Vector3>(estimatedFaces * 4);
            var tangents = new List<Vector4>(estimatedFaces * 4);
            var uvs = new List<Vector2>(estimatedFaces * 4);
            var triangles = new List<int>(estimatedFaces * 6);

            // Walk every voxel cell in the bounding cube and keep only the ones inside the
            // sphere (x^2 + y^2 + z^2 <= r^2) — this replaces the old HashSet<Vector3Int>
            // of solid cells: "is this cell solid?" is now a formula, not a lookup.
            for (int x = -radiusInVoxels; x <= radiusInVoxels; x++)
            {
                int xSqr = x * x;

                for (int y = -radiusInVoxels; y <= radiusInVoxels; y++)
                {
                    // Running partial sum so the inner z loop only needs one more term,
                    // instead of recomputing x*x + y*y every iteration.
                    int xySqr = xSqr + y * y;
                    if (xySqr > radiusSqr)
                        continue; // this whole (x, y) column is outside the sphere

                    for (int z = -radiusInVoxels; z <= radiusInVoxels; z++)
                    {
                        if (xySqr + z * z > radiusSqr)
                            continue; // this cell is outside the sphere: nothing to draw here

                        // Cell (x, y, z) is solid. For each of its 6 neighbors, if the
                        // neighbor is OUTSIDE the sphere, that side of this voxel is exposed
                        // to empty space and needs a face. If the neighbor is also solid,
                        // it hides this face (nothing will ever see it), so skip it.
                        for (int i = 0; i < FaceTemplates.Length; i++)
                        {
                            FaceTemplate template = FaceTemplates[i];
                            int nx = x + template.Direction.x;
                            int ny = y + template.Direction.y;
                            int nz = z + template.Direction.z;

                            if (nx * nx + ny * ny + nz * nz <= radiusSqr)
                                continue; // neighbor is solid too -> face is hidden, skip it

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
            mesh.SetTriangles(triangles, 0); // also recalculates bounds by default
            return mesh;
        }

        // Emits one quad (as 4 vertices + 2 triangles) for the face of voxel (x, y, z)
        // described by template, scaled to the real voxel size.
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

            // Two triangles covering corners 0-1-2 and 0-2-3, in the winding order Unity
            // expects for a front-facing (visible) quad given this face's normal.
            triangles.Add(startIndex);
            triangles.Add(startIndex + 1);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 3);
        }
    }
}
