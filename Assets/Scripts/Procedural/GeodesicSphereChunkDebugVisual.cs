using UnityEngine;

namespace GemstoneColony.Procedural
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class GeodesicSphereChunkDebugVisual : MonoBehaviour
    {
        [SerializeField] private float radius = 4f;
        [SerializeField] [Range(0, 5)] private int subdivisions = 2;

        private void OnEnable() => Regenerate();
        private void OnValidate() => Regenerate();

        [ContextMenu("Regenerate")]
        private void Regenerate()
        {
            GetComponent<MeshFilter>().sharedMesh = GeodesicSphereChunkMeshBuilder.Build(radius, subdivisions);
        }
    }
}
