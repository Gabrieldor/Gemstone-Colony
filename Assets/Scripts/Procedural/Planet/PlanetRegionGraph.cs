using System.Collections.Generic;
using UnityEngine;
using GemstoneColony.Procedural;

namespace GemstoneColony.Procedural.Planet
{
    /// <summary>
    /// Builds the hex/pentagon region graph (via IcosphereBuilder) and assigns each region
    /// a biome by flood-filling out from a few widely-spread seed regions -- producing a
    /// handful of large contiguous biome zones rather than per-region speckle. Regions are
    /// used only to label cells for biome lookup; they never decide which voxels exist.
    /// </summary>
    public static class PlanetRegionGraph
    {
        public sealed class Result
        {
            public readonly List<Vector3> Centers;
            public readonly Biome[] Biomes;
            public readonly List<int>[] Adjacency;

            public Result(List<Vector3> centers, Biome[] biomes, List<int>[] adjacency)
            {
                Centers = centers;
                Biomes = biomes;
                Adjacency = adjacency;
            }
        }

        private static readonly Biome[] SeedBiomes = { Biome.Mountain, Biome.Plains, Biome.Basin };

        public static Result Build(int subdivisions)
        {
            IcosphereBuilder.Result ico = IcosphereBuilder.Build(subdivisions);
            List<int>[] adjacency = BuildAdjacency(ico.Vertices.Count, ico.Triangles);
            int[] seeds = PickFarSeeds(ico.Vertices, SeedBiomes.Length);
            Biome[] biomes = FloodFillBiomes(ico.Vertices.Count, adjacency, seeds, SeedBiomes);
            return new Result(ico.Vertices, biomes, adjacency);
        }

        // Brute-force nearest region by angular distance (max dot product). Deliberately not
        // a hint-based adjacency hill-climb -- an earlier version used one to avoid the
        // O(regionCount) cost per voxel, but at coarse region counts it could converge to the
        // wrong region right at a chunk boundary and cause two chunks to disagree on a shared
        // cell's biome (see Docs/Systems/PlanetSphere.md). Fine at the region counts used
        // here; a genuinely fast *and* correct lookup is real future work at production scale.
        public static int FindNearestRegion(Vector3 direction, List<Vector3> centers)
        {
            int best = 0;
            float bestDot = float.NegativeInfinity;
            for (int i = 0; i < centers.Count; i++)
            {
                float dot = Vector3.Dot(direction, centers[i]);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = i;
                }
            }
            return best;
        }

        // Adjacency hill-climb from a hint region: walks to whichever neighbor improves the
        // dot product, stopping at a local max. O(hops), not O(regionCount) -- but a local
        // max isn't always the true global-nearest region, so this is only safe to use where
        // being occasionally slightly wrong can't cause two independent computations of the
        // same physical cell to disagree (i.e. away from chunk/face boundaries -- see
        // SmallPlanetChunkLayout.IsNearChunkBoundary and Docs/Systems/PlanetSphere.md for why
        // "always hill-climb" caused a real seam bug once).
        public static int ResolveRegionNear(Vector3 direction, List<Vector3> centers, List<int>[] adjacency, int hint)
        {
            int current = hint;
            float currentDot = Vector3.Dot(direction, centers[current]);

            bool improved = true;
            while (improved)
            {
                improved = false;
                foreach (int neighbor in adjacency[current])
                {
                    float dot = Vector3.Dot(direction, centers[neighbor]);
                    if (dot > currentDot)
                    {
                        currentDot = dot;
                        current = neighbor;
                        improved = true;
                    }
                }
            }

            return current;
        }

        private static List<int>[] BuildAdjacency(int vertexCount, List<int[]> triangles)
        {
            var sets = new HashSet<int>[vertexCount];
            for (int i = 0; i < vertexCount; i++)
                sets[i] = new HashSet<int>();

            foreach (int[] tri in triangles)
            {
                AddEdge(sets, tri[0], tri[1]);
                AddEdge(sets, tri[1], tri[2]);
                AddEdge(sets, tri[2], tri[0]);
            }

            var result = new List<int>[vertexCount];
            for (int i = 0; i < vertexCount; i++)
                result[i] = new List<int>(sets[i]);
            return result;
        }

        private static void AddEdge(HashSet<int>[] sets, int a, int b)
        {
            sets[a].Add(b);
            sets[b].Add(a);
        }

        // Greedy farthest-point sampling by angular distance -- good enough spread for a
        // 3-biome demo without needing true k-center optimality.
        private static int[] PickFarSeeds(List<Vector3> centers, int count)
        {
            var seeds = new int[count];
            seeds[0] = 0;
            var maxDotToChosen = new float[centers.Count];
            for (int i = 0; i < centers.Count; i++)
                maxDotToChosen[i] = Vector3.Dot(centers[i], centers[0]);

            for (int s = 1; s < count; s++)
            {
                int best = 0;
                float bestVal = float.PositiveInfinity;
                for (int i = 0; i < centers.Count; i++)
                {
                    if (maxDotToChosen[i] < bestVal)
                    {
                        bestVal = maxDotToChosen[i];
                        best = i;
                    }
                }

                seeds[s] = best;
                for (int i = 0; i < centers.Count; i++)
                {
                    float d = Vector3.Dot(centers[i], centers[best]);
                    if (d > maxDotToChosen[i])
                        maxDotToChosen[i] = d;
                }
            }

            return seeds;
        }

        private static Biome[] FloodFillBiomes(int vertexCount, List<int>[] adjacency, int[] seeds, Biome[] seedBiomes)
        {
            var biomes = new Biome[vertexCount];
            var visited = new bool[vertexCount];
            var queue = new Queue<int>();

            for (int i = 0; i < seeds.Length; i++)
            {
                biomes[seeds[i]] = seedBiomes[i];
                visited[seeds[i]] = true;
                queue.Enqueue(seeds[i]);
            }

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (int neighbor in adjacency[current])
                {
                    if (visited[neighbor])
                        continue;
                    visited[neighbor] = true;
                    biomes[neighbor] = biomes[current];
                    queue.Enqueue(neighbor);
                }
            }

            return biomes;
        }
    }
}
