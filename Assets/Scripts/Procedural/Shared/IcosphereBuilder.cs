using System.Collections.Generic;
using UnityEngine;

namespace GemstoneColony.Procedural
{
    /// <summary>
    /// Builds a geodesic sphere by recursively subdividing an icosahedron's triangular
    /// faces, projecting every new vertex onto the unit sphere. Shared by anything that
    /// needs the underlying vertex/triangle data (dual-mesh construction, region graphs),
    /// not just direct rendering.
    /// </summary>
    public static class IcosphereBuilder
    {
        public readonly struct Result
        {
            public readonly List<Vector3> Vertices;
            public readonly List<int[]> Triangles;

            public Result(List<Vector3> vertices, List<int[]> triangles)
            {
                Vertices = vertices;
                Triangles = triangles;
            }
        }

        public static Result Build(int subdivisions)
        {
            List<Vector3> vertices = BuildIcosahedronVertices();
            List<int[]> triangles = BuildIcosahedronTriangles();

            for (int level = 0; level < subdivisions; level++)
                Subdivide(vertices, ref triangles);

            return new Result(vertices, triangles);
        }

        private static List<Vector3> BuildIcosahedronVertices()
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;

            var vertices = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };

            for (int i = 0; i < vertices.Count; i++)
                vertices[i] = vertices[i].normalized;

            return vertices;
        }

        private static List<int[]> BuildIcosahedronTriangles()
        {
            return new List<int[]>
            {
                new[] {0, 11, 5}, new[] {0, 5, 1}, new[] {0, 1, 7}, new[] {0, 7, 10}, new[] {0, 10, 11},
                new[] {1, 5, 9}, new[] {5, 11, 4}, new[] {11, 10, 2}, new[] {10, 7, 6}, new[] {7, 1, 8},
                new[] {3, 9, 4}, new[] {3, 4, 2}, new[] {3, 2, 6}, new[] {3, 6, 8}, new[] {3, 8, 9},
                new[] {4, 9, 5}, new[] {2, 4, 11}, new[] {6, 2, 10}, new[] {8, 6, 7}, new[] {9, 8, 1},
            };
        }

        // Splits every triangle into 4 by adding an edge-midpoint vertex per edge, shared
        // between the two triangles on either side of that edge (via the cache) so
        // subdivision doesn't duplicate vertices along shared edges.
        private static void Subdivide(List<Vector3> vertices, ref List<int[]> triangles)
        {
            var midpointCache = new Dictionary<long, int>();
            var newTriangles = new List<int[]>(triangles.Count * 4);

            foreach (int[] tri in triangles)
            {
                int a = GetOrCreateMidpoint(midpointCache, vertices, tri[0], tri[1]);
                int b = GetOrCreateMidpoint(midpointCache, vertices, tri[1], tri[2]);
                int c = GetOrCreateMidpoint(midpointCache, vertices, tri[2], tri[0]);

                newTriangles.Add(new[] {tri[0], a, c});
                newTriangles.Add(new[] {tri[1], b, a});
                newTriangles.Add(new[] {tri[2], c, b});
                newTriangles.Add(new[] {a, b, c});
            }

            triangles = newTriangles;
        }

        private static int GetOrCreateMidpoint(Dictionary<long, int> cache, List<Vector3> vertices, int i0, int i1)
        {
            long key = i0 < i1 ? ((long) i0 << 32) + i1 : ((long) i1 << 32) + i0;
            if (cache.TryGetValue(key, out int existing))
                return existing;

            Vector3 midpoint = ((vertices[i0] + vertices[i1]) * 0.5f).normalized;
            vertices.Add(midpoint);
            int index = vertices.Count - 1;
            cache[key] = index;
            return index;
        }
    }
}
