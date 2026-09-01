using UnityEngine;

namespace GemstoneColony.Procedural
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class CubeSphereChunkDebugVisual : MonoBehaviour
    {
        [SerializeField] private float radius = 4f;
        [SerializeField] [Min(1)] private int chunksPerFaceEdge = 5;

        private void OnEnable() => Regenerate();
        private void OnValidate() => Regenerate();

        [ContextMenu("Regenerate")]
        private void Regenerate()
        {
            GetComponent<MeshFilter>().sharedMesh = CubeSphereChunkMeshBuilder.Build(radius, chunksPerFaceEdge);
        }
    }
}
