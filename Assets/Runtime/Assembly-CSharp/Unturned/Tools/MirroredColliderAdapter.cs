using System.Collections.Generic;
using UnityEngine;

namespace SDG.Unturned
{
    /// <summary>Preserve authored mirrored box geometry using PhysX-supported convex meshes.</summary>
    public static class MirroredColliderAdapter
    {
        public static int ConvertBoxes(Transform root)
        {
            int converted = 0;
            foreach (var box in root.GetComponentsInChildren<BoxCollider>(true))
            {
                var size = box.size;
                var center = box.center;
                var vertices = new Vector3[8];
                for (int i = 0; i < 8; i++)
                    vertices[i] = center + Vector3.Scale(size * .5f, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var mesh = new Mesh { name = "Mirrored box collider geometry" };
                mesh.vertices = vertices;
                mesh.triangles = new[] { 0,2,3,0,3,1, 4,5,7,4,7,6, 0,4,6,0,6,2, 1,3,7,1,7,5, 0,1,5,0,5,4, 2,6,7,2,7,3 };
                mesh.RecalculateBounds();
                bool enabled = box.enabled;
                box.enabled = false;
                var replacement = box.gameObject.AddComponent<MeshCollider>();
                replacement.convex = true;
                replacement.sharedMesh = mesh;
                replacement.sharedMaterial = box.sharedMaterial;
                replacement.isTrigger = box.isTrigger;
                replacement.contactOffset = box.contactOffset;
                replacement.includeLayers = box.includeLayers;
                replacement.excludeLayers = box.excludeLayers;
                replacement.layerOverridePriority = box.layerOverridePriority;
                replacement.providesContacts = box.providesContacts;
                replacement.hasModifiableContacts = box.hasModifiableContacts;
                replacement.enabled = enabled;
                box.gameObject.GetOrAddComponent<MirroredColliderMeshLifetime>().meshes.Add(mesh);
                if (Application.isPlaying) Object.Destroy(box); else Object.DestroyImmediate(box);
                converted++;
            }
            return converted;
        }
    }

    public sealed class MirroredColliderMeshLifetime : MonoBehaviour
    {
        internal readonly List<Mesh> meshes = new List<Mesh>();
        private void OnDestroy()
        {
            foreach (var mesh in meshes)
                if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        }
    }
}
