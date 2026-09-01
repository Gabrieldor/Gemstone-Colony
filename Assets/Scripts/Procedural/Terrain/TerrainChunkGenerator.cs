using UnityEngine;

namespace GemstoneColony.Procedural
{
    /// <summary>
    /// Thin scene-side adapter: generates one chunk's voxel data and hands the resulting
    /// ground/water meshes to two pre-wired MeshFilters. All actual generation logic lives
    /// in TerrainChunkVoxelData / TerrainChunkMeshBuilder -- this component only owns the
    /// Inspector-tunable parameters and the regenerate trigger.
    /// </summary>
    [ExecuteAlways]
    public class TerrainChunkGenerator : MonoBehaviour
    {
        [Header("Chunk dimensions")]
        [SerializeField] [Min(1)] private int width = 64;
        [SerializeField] [Min(1)] private int height = 32;
        [SerializeField] [Min(1)] private int depth = 64;
        [SerializeField] [Min(0.01f)] private float voxelSize = 1f;

        [Header("Heightmap noise")]
        [SerializeField] private float noiseScale = 0.06f;
        [SerializeField] private float heightBase = 8f;
        [SerializeField] private float heightAmplitude = 14f;
        [SerializeField] private int seed = 12345;

        [Header("Water")]
        [SerializeField] [Min(0)] private int waterLevel = 12;

        [Header("Mesh outputs")]
        [SerializeField] private MeshFilter groundMeshFilter;
        [SerializeField] private MeshFilter waterMeshFilter;

        private void OnEnable() => Regenerate();
        private void OnValidate() => Regenerate();

        [ContextMenu("Regenerate")]
        private void Regenerate()
        {
            if (groundMeshFilter == null || waterMeshFilter == null)
                return;

            var data = new TerrainChunkVoxelData(
                width, height, depth, noiseScale, heightBase, heightAmplitude, waterLevel, seed);

            groundMeshFilter.sharedMesh = TerrainChunkMeshBuilder.Build(data, TerrainVoxelType.Ground, voxelSize);
            waterMeshFilter.sharedMesh = TerrainChunkMeshBuilder.Build(data, TerrainVoxelType.Water, voxelSize);
        }
    }
}
