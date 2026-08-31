using UnityEngine;

namespace GemstoneColony.Procedural
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class HexLabeledVoxelSphereDebugVisual : MonoBehaviour
    {
        [SerializeField] private float voxelSize = 0.2f;
        [SerializeField] [Min(1)] private int outerRadiusInVoxels = 20;
        [SerializeField] [Min(1)] private int shellThicknessVoxels = 4;
        [SerializeField] [Range(0, 4)] private int regionSubdivisions = 2;

        private void OnEnable() => Regenerate();
        private void OnValidate() => Regenerate();

        [ContextMenu("Regenerate")]
        private void Regenerate()
        {
            GetComponent<MeshFilter>().sharedMesh =
                HexLabeledVoxelSphereMeshBuilder.Build(voxelSize, outerRadiusInVoxels, shellThicknessVoxels, regionSubdivisions);
        }
    }
}
