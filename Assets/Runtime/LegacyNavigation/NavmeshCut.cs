using UnityEngine;
using UnityEngine.AI;

namespace Pathfinding
{
    /// <summary>
    /// Reads the obstacle data embedded in existing Unturned bundles and implements
    /// it with Unity navigation. The assembly/type identity preserves bundle references.
    /// No A* implementation or proprietary code is included.
    /// </summary>
    public sealed class NavmeshCut : MonoBehaviour
    {
        public enum MeshType { Rectangle, Circle, CustomMesh, Box, Sphere, Capsule }
        public enum RadiusExpansionMode { DontExpand, ExpandByAgentRadius }
        public MeshType type;
        public Mesh mesh;
        public Vector2 rectangleSize = Vector2.one;
        public float circleRadius = 1;
        public int circleResolution = 6;
        public float height = 1;
        public float meshScale = 1;
        public Vector3 center;
        public float updateDistance = .4f;
        public bool isDual;
        public RadiusExpansionMode radiusExpansionMode = RadiusExpansionMode.ExpandByAgentRadius;
        public bool cutsAddedGeom = true;
        public float updateRotationDistance = 10;
        public bool useRotationAndScale;
        private NavMeshObstacle obstacle;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (obstacle == null) obstacle = gameObject.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
            obstacle.carvingMoveThreshold = Mathf.Max(.01f, updateDistance);
            obstacle.center = center;
            if (type == MeshType.Circle || type == MeshType.Sphere || type == MeshType.Capsule)
            {
                obstacle.shape = NavMeshObstacleShape.Capsule;
                obstacle.radius = Mathf.Max(.01f, circleRadius);
                obstacle.height = Mathf.Max(height, circleRadius * 2);
            }
            else
            {
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.size = new Vector3(Mathf.Max(.01f, rectangleSize.x), Mathf.Max(.01f, height), Mathf.Max(.01f, rectangleSize.y));
                if (type == MeshType.CustomMesh && mesh != null)
                {
                    obstacle.center += mesh.bounds.center * meshScale;
                    var size = mesh.bounds.size * Mathf.Abs(meshScale);
                    obstacle.size = new Vector3(Mathf.Max(.01f, size.x), Mathf.Max(height, size.y), Mathf.Max(.01f, size.z));
                }
            }
            // Dual cuts split areas without excluding them. Unity obstacles exclude
            // areas, so do not turn an authored dual cut into an impassable obstacle.
            obstacle.enabled = !isDual;
        }

        private void OnDisable() { if (obstacle != null) obstacle.enabled = false; }
        private void OnDestroy() { if (obstacle != null) Destroy(obstacle); }
    }
}
