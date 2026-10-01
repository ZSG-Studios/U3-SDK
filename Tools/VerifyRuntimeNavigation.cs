using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

public static class VerifyRuntimeNavigation
{
    public static object Main()
    {
        return Create(false);
    }
    public static object Create(bool lowFrameRate)
    {
        var host = new GameObject("Native navigation qualification fixture");
        var fixture = host.AddComponent<NativeNavigationQualification>();
        fixture.lowFrameRate = lowFrameRate;
        fixture.reportPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", lowFrameRate ? "native-navigation-low-fps-fixture.json" : "native-navigation-fixture.json");
        Directory.CreateDirectory(Path.GetDirectoryName(fixture.reportPath));
        if (File.Exists(fixture.reportPath)) File.Delete(fixture.reportPath);
        return new { reportPath = fixture.reportPath, status = "running" };
    }
}

public static class VerifyLowFrameRateNavigation
{
    public static object Main() => VerifyRuntimeNavigation.Create(true);
}

public sealed class NativeNavigationQualification : MonoBehaviour
{
    public string reportPath;
    public bool lowFrameRate;
    private NavMeshData data;
    private NavMeshDataInstance instance;
    private Mesh mesh;
    private float simulatedMovementSeconds, startedMovingAt, minimumTimeStep = float.MaxValue, maximumTimeStep;
    private int movementFrames;
    private IEnumerator Start()
    {
        var origin = new Vector3(10000, 0, 10000);
        mesh = new Mesh();
        mesh.vertices = new[] { new Vector3(-10,0,-10), new Vector3(10,0,-10), new Vector3(10,0,10), new Vector3(-10,0,10) };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var source = new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Mesh, sourceObject = mesh,
            transform = Matrix4x4.Translate(origin), area = 0 };
        data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), new List<NavMeshBuildSource> { source }, new Bounds(origin, new Vector3(20, 10, 20)), Vector3.zero, Quaternion.identity);
        if (data == null) { Finish(false, "Fixture bake failed", 0, 0); yield break; }
        instance = NavMesh.AddNavMeshData(data);
        var ground = new GameObject("Navigation fixture ground"); ground.transform.SetParent(transform); ground.layer = SDG.Unturned.LayerMasks.GROUND;
        ground.transform.position = origin + Vector3.down * .1f;
        var collision = ground.AddComponent<BoxCollider>(); collision.size = new Vector3(20, .2f, 20);
        var start = origin + new Vector3(-6, 0, 0); var end = origin + new Vector3(6, 0, 0);
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(start, end, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
        { Finish(false, "Open path failed", 0, 0); yield break; }
        int openCorners = path.corners.Length;
        var blocker = new GameObject("Migrated navigation blocker"); blocker.transform.SetParent(transform); blocker.transform.position = origin;
        var cut = blocker.AddComponent<Pathfinding.NavmeshCut>(); cut.enabled = false;
        cut.type = Pathfinding.NavmeshCut.MeshType.Rectangle; cut.rectangleSize = new Vector2(4, 8); cut.height = 4; cut.center = new Vector3(0, 1, 0); cut.useRotationAndScale = true;
        cut.enabled = true;
        float carveDeadline = Time.realtimeSinceStartup + 5;
        do
        {
            yield return null;
            NavMesh.CalculatePath(start, end, NavMesh.AllAreas, path);
        } while (path.corners.Length <= openCorners && Time.realtimeSinceStartup < carveDeadline);
        if (!NavMesh.CalculatePath(start, end, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete || path.corners.Length <= openCorners)
        { Finish(false, "Migrated obstacle did not carve a detour: " + Newtonsoft.Json.JsonConvert.SerializeObject(new { enabled = blocker.GetComponent<NavMeshObstacle>().enabled, carving = blocker.GetComponent<NavMeshObstacle>().carving, size = blocker.GetComponent<NavMeshObstacle>().size.ToString(), center = blocker.GetComponent<NavMeshObstacle>().center.ToString(), valid = instance.valid }), openCorners, path.corners.Length); yield break; }
        int closedCorners = path.corners.Length;
        var pawn = new GameObject("SDK movement adapter fixture"); pawn.transform.SetParent(transform); pawn.layer = SDG.Unturned.LayerMasks.AGENT; pawn.transform.position = start + Vector3.up;
        Physics.SyncTransforms();
        if (Physics.GetIgnoreLayerCollision(ground.layer, pawn.layer)) { Finish(false, "Fixture ground/agent collision is disabled", openCorners, closedCorners); yield break; }
        var controller = pawn.AddComponent<CharacterController>(); controller.height = 2; controller.radius = .4f;
        var target = new GameObject("Fixture destination"); target.transform.SetParent(transform); target.transform.position = end + Vector3.up;
        var movement = pawn.AddComponent<SDG.Unturned.UnityZombieNavigation>(); movement.TargetTransform = target.transform; movement.Speed = 6;
        float maximumLateralDeviation = 0;
        bool enteredBlockedFootprint = false;
        startedMovingAt = Time.realtimeSinceStartup;
        for (int frame = 0; frame < 500; frame++)
        {
            // Isolate waypoint traversal from replanning on a fixed, carved path.
            // A 3 FPS step can cross a corner between observations.
            float step = lowFrameRate ? 1f / 3f : Time.deltaTime;
            simulatedMovementSeconds += step;
            minimumTimeStep = Mathf.Min(minimumTimeStep, step);
            maximumTimeStep = Mathf.Max(maximumTimeStep, step);
            movementFrames++;
            movement.Move(step);
            if (lowFrameRate) movement.CanSearch = false;
            var relative = pawn.transform.position - origin;
            maximumLateralDeviation = Mathf.Max(maximumLateralDeviation, Mathf.Abs(relative.z));
            if (Mathf.Abs(relative.x) < 2 && Mathf.Abs(relative.z) < 4) enteredBlockedFootprint = true;
            if (Vector3.Distance(pawn.transform.position, target.transform.position) < 1.5f) break;
            yield return null;
        }
        float distance = Vector3.Distance(pawn.transform.position, target.transform.position);
        cut.enabled = false;
        yield return null; yield return null; yield return null;
        NavMesh.CalculatePath(start, end, NavMesh.AllAreas, path);
        bool reopened = path.status == NavMeshPathStatus.PathComplete && path.corners.Length == openCorners;
        Finish(distance < 1.5f && reopened && maximumLateralDeviation >= 4 && !enteredBlockedFootprint, "Movement remaining distance " + distance + "; obstacle reopened " + reopened + "; detour lateral distance " + maximumLateralDeviation + "; entered blocker " + enteredBlockedFootprint, openCorners, closedCorners);
    }
    private void Finish(bool passed, string detail, int openCorners, int closedCorners)
    {
        File.WriteAllText(reportPath, Newtonsoft.Json.JsonConvert.SerializeObject(new { passed, detail, openCorners, closedCorners,
            lowFrameRate, movementFrames, simulatedMovementSeconds, movementElapsedSeconds = startedMovingAt == 0 ? 0 : Time.realtimeSinceStartup - startedMovingAt,
            minimumTimeStep, maximumTimeStep }, Newtonsoft.Json.Formatting.Indented));
        Destroy(gameObject);
    }
    private void OnDestroy() { if (instance.valid) instance.Remove(); if (data != null) Destroy(data); if (mesh != null) Destroy(mesh); }
}
