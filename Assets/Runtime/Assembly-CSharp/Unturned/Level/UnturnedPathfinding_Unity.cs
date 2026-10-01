using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace SDG.Unturned
{
    public sealed class UnturnedPathfinding_Unity : IUnturnedPathfindingInterface
    {
        public void OnGameLevelInstantiated() { }
        public IUnturnedNavmeshInterface CreateNavmesh() => new UnityNavigationMesh();
        public IUnturnedPerNavmeshEditorInterface CreateFlag(Flag owner) => new UnityNavigationFlag(owner);
        public System.Type GetCutComponentType() => typeof(Pathfinding.NavmeshCut);
        public IUnturnedPathfindingMovementComponentInterface CreateMovementComponentForZombie(Zombie zombie)
            => zombie.gameObject.AddComponent<UnityZombieNavigation>();
        public IUnturnedNavmeshCutInterface CreateCutForIOBS(InteractableObjectBinaryState owner)
        {
            var cut = owner.GetComponentInChildren<Pathfinding.NavmeshCut>(true);
            return cut != null ? new UnityNavigationCut(cut) : null;
        }
    }

    internal sealed class UnityNavigationCut : IUnturnedNavmeshCutInterface
    {
        private readonly Pathfinding.NavmeshCut cut;
        public UnityNavigationCut(Pathfinding.NavmeshCut cut) { this.cut = cut; }
        public bool IsActive { get => cut != null && cut.enabled; set { if (cut != null) cut.enabled = value; } }
    }

    internal sealed class UnityNavigationMesh : UnturnedNavmesh_Empty, IUnturnedNavmeshInterface
    {
        private NavMeshData data;
        private NavMeshDataInstance instance;
        private UnityNavigationLifetime lifetime;
        public new bool ContainsAnyBakedData => data != null;

        protected override void OnDeserialized()
        {
            var sources = new List<NavMeshBuildSource>();
            var meshes = new List<Mesh>();
            try
            {
                for (int i = 0; i < vertexArrays.Length; i++)
                {
                    if (triangleArrays[i].Length == 0) continue;
                    var vertices = new Vector3[vertexArrays[i].Length];
                    // Existing Unturned navigation files store world coordinates in millimetres.
                    for (int v = 0; v < vertices.Length; v++) vertices[v] = (Vector3)vertexArrays[i][v] * .001f;
                    var mesh = new Mesh { name = "Imported Unturned navigation tile", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                    mesh.vertices = vertices;
                    mesh.triangles = triangleArrays[i];
                    mesh.RecalculateBounds();
                    meshes.Add(mesh);
                    sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Mesh, sourceObject = mesh, transform = Matrix4x4.identity, area = 0 });
                }
                if (sources.Count == 0) return;
                var settings = NavMesh.GetSettingsByIndex(0);
                if (settings.agentTypeID < 0) throw new System.InvalidOperationException("No Unity navigation agent settings available");
                data = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(boundsCenter, boundsSize), Vector3.zero, Quaternion.identity);
                if (data == null) throw new System.InvalidOperationException("Unity navigation could not convert baked Unturned geometry");
                instance = NavMesh.AddNavMeshData(data);
                var host = new GameObject("Unity navigation imported region");
                lifetime = host.AddComponent<UnityNavigationLifetime>();
                lifetime.data = data;
                lifetime.instance = instance;
            }
            finally { foreach (var mesh in meshes) Object.Destroy(mesh); }
        }

        public void Bake(Flag flag)
        {
            if (lifetime != null) Object.Destroy(lifetime.gameObject);
            var host = new GameObject("Unity navigation editor region");
            var surface = host.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            var bounds = flag.CalculateBakingBounds();
            surface.center = bounds.center;
            surface.size = bounds.size;
            surface.BuildNavMesh();
            data = surface.navMeshData;
            if (data == null) { Object.Destroy(host); throw new System.InvalidOperationException("Navigation bake produced no data"); }
            lifetime = host.AddComponent<UnityNavigationLifetime>();
            lifetime.data = data;
            // Keep the SDK navigation file format for maps. Export this region's triangles.
            var triangles = NavMesh.CalculateTriangulation();
            var indices = new List<int>();
            for (int i = 0; i < triangles.indices.Length; i += 3)
            {
                var center = (triangles.vertices[triangles.indices[i]] + triangles.vertices[triangles.indices[i + 1]] + triangles.vertices[triangles.indices[i + 2]]) / 3;
                if (bounds.Contains(center)) { indices.Add(triangles.indices[i]); indices.Add(triangles.indices[i + 1]); indices.Add(triangles.indices[i + 2]); }
            }
            // Partition into SDK tiles to respect the UInt16 vertex/index counts.
            var tileVertices = new List<Vector3Int[]>();
            var tileTriangles = new List<int[]>();
            var mapping = new Dictionary<int, int>();
            var verts = new List<Vector3Int>();
            var tris = new List<int>();
            for (int i = 0; i < indices.Count; i += 3)
            {
                if (tris.Count + 3 > ushort.MaxValue || verts.Count + 3 > ushort.MaxValue)
                {
                    tileVertices.Add(verts.ToArray()); tileTriangles.Add(tris.ToArray());
                    mapping.Clear(); verts.Clear(); tris.Clear();
                }
                for (int j = 0; j < 3; j++)
                {
                    int original = indices[i + j];
                    if (!mapping.TryGetValue(original, out int local)) { local = verts.Count; mapping.Add(original, local); verts.Add(Vector3Int.RoundToInt(triangles.vertices[original] * 1000)); }
                    tris.Add(local);
                }
            }
            if (tris.Count > 0) { tileVertices.Add(verts.ToArray()); tileTriangles.Add(tris.ToArray()); }
            if (tileVertices.Count > byte.MaxValue) throw new System.InvalidOperationException("Navigation region exceeds SDK tile limit");
            boundsCenter = bounds.center; boundsSize = bounds.size;
            tileXCount = tileVertices.Count; tileZCount = 1;
            vertexArrays = tileVertices.ToArray(); triangleArrays = tileTriangles.ToArray();
            flag.needsNavigationSave = true;
        }
    }

    public sealed class UnityNavigationLifetime : MonoBehaviour
    {
        internal NavMeshData data;
        internal NavMeshDataInstance instance;
        private void OnDestroy() { if (instance.valid) instance.Remove(); if (data != null) Destroy(data); }
    }

    internal sealed class UnityNavigationFlag : IUnturnedPerNavmeshEditorInterface
    {
        private readonly Flag flag;
        public UnityNavigationFlag(Flag flag) { this.flag = flag; }
        public int GraphIndexForUI => LevelNavigation.bounds?.FindIndex(b => b.Contains(flag.point)) ?? -1;
        public void OnDestroy() { }
        public void Bake() => ((UnityNavigationMesh)flag.navmeshInterface).Bake(flag);
    }

    public sealed class UnityZombieNavigation : MonoBehaviour, IUnturnedPathfindingMovementComponentInterface
    {
        public bool CanMove { get; set; } = true;
        public bool CanTurn { get; set; } = true;
        public bool CanSearch { get; set; } = true;
        public float Speed { get; set; }
        public Transform TargetTransform { get; set; }
        public Vector3 TargetDirection { get; set; }
        private CharacterController controller;
        private NavMeshPath path;
        private readonly Vector3[] corners = new Vector3[256];
        private int cornerCount, cornerIndex;
        private float nextSearch;
        private void Awake() { controller = GetComponent<CharacterController>(); path = new NavMeshPath(); }
        public void Move(float deltaTime)
        {
            if (controller == null || !controller.enabled || !CanMove || TargetTransform == null || deltaTime <= 0) return;
            if (CanSearch && Time.time >= nextSearch)
            {
                nextSearch = Time.time + .25f;
                cornerCount = 0;
                if (NavMesh.SamplePosition(transform.position, out var start, 3, NavMesh.AllAreas)
                    && NavMesh.SamplePosition(TargetTransform.position, out var end, 3, NavMesh.AllAreas)
                    && NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path))
                { cornerCount = path.GetCornersNonAlloc(corners); cornerIndex = cornerCount > 1 ? 1 : 0; }
            }
            while (cornerIndex < cornerCount - 1 && (corners[cornerIndex] - transform.position).GetHorizontal().sqrMagnitude < .25f) cornerIndex++;
            var offset = cornerIndex < cornerCount ? (corners[cornerIndex] - transform.position).GetHorizontal() : Vector3.zero;
            var direction = offset.normalized;
            if (CanTurn && direction.sqrMagnitude > .001f)
            {
                TargetDirection = direction;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 720 * deltaTime);
            }
            // A hitch must not step beyond a corner: that can leave subsequent
            // frames oscillating outside the corner's arrival tolerance.
            var displacement = Vector3.MoveTowards(Vector3.zero, offset, Speed * deltaTime);
            displacement.y = Physics.gravity.y * 2 * deltaTime;
            controller.Move(displacement);
        }
    }
}
