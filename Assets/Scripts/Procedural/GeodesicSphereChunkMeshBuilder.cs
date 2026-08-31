using System.Collections.Generic;
using UnityEngine;

namespace GemstoneColony.Procedural
{
    /// <summary>
    /// Debug visualization: builds a geodesic sphere via IcosphereBuilder, then takes
    /// its dual -- one cell per icosphere vertex, bounded by the centroids of that
    /// vertex's surrounding triangles. Icosahedron vertices (valence 5) become
    /// pentagons; every vertex introduced by subdivision (valence 6) becomes a
    /// hexagon. This always produces exactly 12 pentagons, the rest hexagons -- the
    /// same "soccer ball" pattern regardless of subdivision level.
    /// Each cell gets its own unshared vertices and a single flat color.
    /// </summary>
    public static class GeodesicSphereChunkMeshBuilder
    {
        public static Mesh Build(float radius, int subdivisions)
        {
            IcosphereBuilder.Result ico = IcosphereBuilder.Build(subdivisions);
            List<Vector3> icoVertices = ico.Vertices;
            List<int[]> icoTriangles = ico.Triangles;

            List<int>[] incidentTriangles = BuildIncidentTriangleLookup(icoVertices.Count, icoTriangles);
            Vector3[] triangleCentroids = BuildTriangleCentroids(icoVertices, icoTriangles);

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            for (int v = 0; v < icoVertices.Count; v++)
            {
                AppendCell(icoVertices[v], incidentTriangles[v], triangleCentroids, v, radius,
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

        private static List<int>[] BuildIncidentTriangleLookup(int vertexCount, List<int[]> triangles)
        {
            var incident = new List<int>[vertexCount];
            for (int i = 0; i < vertexCount; i++)
                incident[i] = new List<int>();

            for (int triIndex = 0; triIndex < triangles.Count; triIndex++)
                foreach (int vIndex in triangles[triIndex])
                    incident[vIndex].Add(triIndex);

            return incident;
        }

        private static Vector3[] BuildTriangleCentroids(List<Vector3> vertices, List<int[]> triangles)
        {
            var centroids = new Vector3[triangles.Count];
            for (int i = 0; i < triangles.Count; i++)
            {
                int[] tri = triangles[i];
                Vector3 centroid = (vertices[tri[0]] + vertices[tri[1]] + vertices[tri[2]]) / 3f;
                centroids[i] = centroid.normalized; // project back onto the sphere
            }

            return centroids;
        }

        // Builds one dual cell (pentagon or hexagon) around icosphere vertex `centerDirection`:
        // a fan of triangles from the center out to its surrounding triangle centroids,
        // ordered by angle so the fan doesn't self-intersect.
        private static void AppendCell(
            Vector3 centerDirection,
            List<int> incidentTriangleIndices,
            Vector3[] triangleCentroids,
            int cellIndex,
            float radius,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Color> colors,
            List<int> triangles)
        {
            Vector3 tangent = Mathf.Abs(centerDirection.y) < 0.99f
                ? Vector3.Cross(Vector3.up, centerDirection).normalized
                : Vector3.Cross(Vector3.right, centerDirection).normalized;
            Vector3 bitangent = Vector3.Cross(centerDirection, tangent);

            var ordered = new List<int>(incidentTriangleIndices);
            ordered.Sort((a, b) =>
            {
                float angleA = AngleAround(triangleCentroids[a], centerDirection, tangent, bitangent);
                float angleB = AngleAround(triangleCentroids[b], centerDirection, tangent, bitangent);
                return angleA.CompareTo(angleB);
            });

            Color color = ChunkDebugColorPalette.ForIndex(cellIndex);

            int centerVertexIndex = vertices.Count;
            vertices.Add(centerDirection * radius);
            normals.Add(centerDirection);
            colors.Add(color);

            int firstRimIndex = -1;
            int previousRimIndex = -1;

            foreach (int triIndex in ordered)
            {
                Vector3 rimDirection = triangleCentroids[triIndex];
                int rimVertexIndex = vertices.Count;
                vertices.Add(rimDirection * radius);
                normals.Add(rimDirection);
                colors.Add(color);

                if (previousRimIndex >= 0)
                {
                    triangles.Add(centerVertexIndex);
                    triangles.Add(previousRimIndex);
                    triangles.Add(rimVertexIndex);
                }
                else
                {
                    firstRimIndex = rimVertexIndex;
                }

                previousRimIndex = rimVertexIndex;
            }

            triangles.Add(centerVertexIndex);
            triangles.Add(previousRimIndex);
            triangles.Add(firstRimIndex);
        }

        private static float AngleAround(Vector3 point, Vector3 center, Vector3 tangent, Vector3 bitangent)
        {
            Vector3 offset = point - center;
            return Mathf.Atan2(Vector3.Dot(offset, bitangent), Vector3.Dot(offset, tangent));
        }
    }
}
