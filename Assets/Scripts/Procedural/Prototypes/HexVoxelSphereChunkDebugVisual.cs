using UnityEngine;

namespace GemstoneColony.Procedural
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class HexVoxelSphereChunkDebugVisual : MonoBehaviour
    {
        [SerializeField] private float radius = 4f;
        [SerializeField] [Range(0, 4)] private int subdivisions = 2;
        [SerializeField] [Min(2)] private int voxelsPerRegionEdge = 8;
        [SerializeField] [Min(1)] private int shellThicknessVoxels = 3;

        private void OnEnable() => Regenerate();
        private void OnValidate() => Regenerate();

        [ContextMenu("Regenerate")]
        private void Regenerate()
        {
            GetComponent<MeshFilter>().sharedMesh =
                HexVoxelSphereMeshBuilder.Build(radius, subdivisions, voxelsPerRegionEdge, shellThicknessVoxels);
        }
    }
}
