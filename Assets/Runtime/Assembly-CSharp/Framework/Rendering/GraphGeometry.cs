using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SDG.Framework.Rendering
{
    /// <summary>Records the SDK's line/triangle drawing API into reusable meshes for RenderGraph.</summary>
    public static class GraphGeometry
    {
        public const int LINES = 1, LINE_STRIP = 2, TRIANGLES = 4;
        public sealed class Draw
        {
            public Mesh mesh;
            public Material material;
            public int pass;
            public MaterialPropertyBlock properties = new MaterialPropertyBlock();
        }
        private static List<Draw> pool;
        private static int drawCount, topology, pass;
        private static Material material;
        private static UnityEngine.Color color;
        private static bool ortho;
        private static readonly Stack<bool> matrixStack = new Stack<bool>();
        private static readonly List<Vector3> vertices = new List<Vector3>();
        private static readonly List<UnityEngine.Color> colors = new List<UnityEngine.Color>();
        private static readonly List<int> indices = new List<int>();

        internal static void StartRecording(List<Draw> destination)
        {
            pool = destination;
            drawCount = 0;
            ortho = false;
            color = UnityEngine.Color.white;
            material = null;
            pass = 0;
            matrixStack.Clear();
        }
        internal static int FinishRecording() { pool = null; return drawCount; }
        public static bool SetMaterial(Material value, int shaderPass = 0)
        {
            material = value;
            pass = shaderPass;
            return value != null;
        }
        public static void PushMatrix() => matrixStack.Push(ortho);
        public static void PopMatrix() => ortho = matrixStack.Pop();
        public static void LoadOrtho() => ortho = true;
        public static void Color(UnityEngine.Color value) => color = value;
        public static void Vertex(Vector3 value) { vertices.Add(value); colors.Add(color); }
        public static void Begin(int mode)
        {
            if (pool == null) throw new InvalidOperationException("Geometry drawing requires a RenderGraph capture.");
            topology = mode;
            vertices.Clear(); colors.Clear(); indices.Clear();
        }
        public static void End()
        {
            if (vertices.Count == 0 || material == null) return;
            if (drawCount == pool.Count)
            {
                var mesh = new Mesh { name = "Unturned gizmo geometry", indexFormat = IndexFormat.UInt32 };
                mesh.MarkDynamic();
                pool.Add(new Draw { mesh = mesh });
            }
            var draw = pool[drawCount++];
            draw.material = material;
            draw.pass = pass;
            draw.properties.SetFloat("_UnturnedOrtho", ortho ? 1 : 0);
            draw.mesh.Clear();
            draw.mesh.SetVertices(vertices);
            draw.mesh.SetColors(colors);
            if (topology == LINE_STRIP)
                for (int i = 1; i < vertices.Count; i++) { indices.Add(i - 1); indices.Add(i); }
            else
                for (int i = 0; i < vertices.Count; i++) indices.Add(i);
            draw.mesh.SetIndices(indices, topology == TRIANGLES ? MeshTopology.Triangles : MeshTopology.Lines, 0);
        }
    }
}
