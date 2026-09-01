using UnityEngine;
using GemstoneColony.Procedural;

namespace GemstoneColony.Procedural.Planet
{
    /// <summary>
    /// Thin scene-side adapter for one chunk of the small full-sphere demo: owns the
    /// Inspector-tunable (faceIndex, chunkI, chunkJ) + mesh outputs, no generation logic
    /// itself. The region graph is built once (a static cache) and shared across every chunk
    /// instance on every face.
    /// </summary>
    [ExecuteAlways]
    public class SmallPlanetChunkGenerator : MonoBehaviour
    {
        [SerializeField] private int faceIndex;
        [SerializeField] private int chunkI;
        [SerializeField] private int chunkJ;
        [SerializeField] private MeshFilter groundMeshFilter;
        [SerializeField] private MeshFilter waterMeshFilter;

        private static PlanetRegionGraph.Result cachedRegions;

        private void OnEnable() => Regenerate();
        private void OnValidate() => Regenerate();

        [ContextMenu("Regenerate")]
        private void Regenerate()
        {
            if (groundMeshFilter == null || waterMeshFilter == null)
                return;

            if (cachedRegions == null)
                cachedRegions = PlanetRegionGraph.Build(SmallPlanetConfig.RegionSubdivisions);

            var data = new SmallPlanetChunkVoxelData(faceIndex, chunkI, chunkJ, cachedRegions);
            groundMeshFilter.sharedMesh = SmallPlanetChunkMeshBuilder.Build(data, TerrainVoxelType.Ground);
            waterMeshFilter.sharedMesh = SmallPlanetChunkMeshBuilder.Build(data, TerrainVoxelType.Water);
        }
    }
}
