using System;
using UnityEngine;
using SDG.Unturned;
public static class VerifyMirroredColliders
{
    public static object Main()
    {
        var host = new GameObject("Mirrored collider geometry fixture");
        host.transform.position = new Vector3(12000, 100, 12000);
        var box = host.AddComponent<BoxCollider>();
        box.center = new Vector3(.3f, .2f, -.1f); box.size = new Vector3(2, 3, 4);
        var center = box.center; var size = box.size;
        try
        {
            int converted = MirroredColliderAdapter.ConvertBoxes(host.transform);
            host.transform.localScale = new Vector3(-2, 3, .5f);
            Physics.SyncTransforms();
            var collider = host.GetComponent<MeshCollider>();
            var expectedCenter = host.transform.TransformPoint(center);
            var expectedSize = Vector3.Scale(size, new Vector3(2, 3, .5f));
            if (Vector3.Distance(collider.bounds.center, expectedCenter) > .001f || Vector3.Distance(collider.bounds.size, expectedSize) > .001f)
                throw new InvalidOperationException("Mirrored convex geometry changed the authored box bounds");
            int hits = 0;
            foreach (var direction in new[] {Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back})
            {
                if (!collider.Raycast(new Ray(expectedCenter + direction * 10, -direction), out var hit, 20))
                    throw new InvalidOperationException("Mirrored convex collider failed a face raycast");
                hits++;
            }
            return new {passed=true,converted,faceRaycasts=hits};
        }
        finally { if (Application.isPlaying) UnityEngine.Object.Destroy(host); else UnityEngine.Object.DestroyImmediate(host); }
    }
}
