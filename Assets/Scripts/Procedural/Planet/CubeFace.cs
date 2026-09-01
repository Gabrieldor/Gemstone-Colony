using UnityEngine;

namespace GemstoneColony.Procedural.Planet
{
    /// <summary>
    /// One of the 6 axis-aligned cube faces used to map a cube onto a sphere (same
    /// tangent/bitangent construction as CubeSphereChunkMeshBuilder). General-purpose --
    /// not tied to any single planet's config -- so both the single-face patch demo and a
    /// full 6-face demo can share it.
    /// </summary>
    public readonly struct CubeFace
    {
        public readonly Vector3 Normal;
        public readonly Vector3 Tangent;
        public readonly Vector3 Bitangent;

        public CubeFace(Vector3 normal)
        {
            Normal = normal;
            Vector3 tangent = normal.x != 0 ? Vector3.up : Vector3.right;
            Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;
            Tangent = Vector3.Cross(bitangent, normal).normalized;
            Bitangent = bitangent;
        }

        // Index order fixed to match Classify's return values below: +X,-X,+Y,-Y,+Z,-Z.
        public static readonly CubeFace[] All =
        {
            new CubeFace(new Vector3(1, 0, 0)),
            new CubeFace(new Vector3(-1, 0, 0)),
            new CubeFace(new Vector3(0, 1, 0)),
            new CubeFace(new Vector3(0, -1, 0)),
            new CubeFace(new Vector3(0, 0, 1)),
            new CubeFace(new Vector3(0, 0, -1)),
        };

        // Standard cube-map classification: the face whose axis has direction's largest
        // magnitude component, matching sign. Ties (exactly on a cube edge/corner) resolve
        // by a fixed check order (x, then y, then z) -- deterministic, so every caller
        // (chunk ownership, neighbor lookups) agrees on the same face even at a seam.
        public static int Classify(Vector3 direction)
        {
            float ax = Mathf.Abs(direction.x), ay = Mathf.Abs(direction.y), az = Mathf.Abs(direction.z);
            if (ax >= ay && ax >= az) return direction.x >= 0f ? 0 : 1;
            if (ay >= az) return direction.y >= 0f ? 2 : 3;
            return direction.z >= 0f ? 4 : 5;
        }

        public bool TryGetST(Vector3 direction, out float s, out float t)
        {
            float dotNormal = Vector3.Dot(direction, Normal);
            if (dotNormal <= 0.0001f)
            {
                s = 0f;
                t = 0f;
                return false;
            }

            s = Vector3.Dot(direction, Tangent) / dotNormal;
            t = Vector3.Dot(direction, Bitangent) / dotNormal;
            return true;
        }

        public Vector3 DirectionAt(float s, float t)
        {
            Vector3 cubePoint = Normal + Tangent * s + Bitangent * t;
            return cubePoint.normalized;
        }
    }
}
