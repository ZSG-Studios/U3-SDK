////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>Compare scheduled instancing with explicit native RenderGraph draws.</summary>
public class EditorInstancingComparison : MonoBehaviour
{
    public enum EImplementation { RenderMeshInstanced, RenderGraph, RenderGraphLate }
    public EImplementation implementation;
    public Mesh mesh;
    public Material material;
    public Camera targetCamera;
    public int shaderPass;
    internal static readonly List<EditorInstancingComparison> Active = new List<EditorInstancingComparison>();
    private readonly List<Matrix4x4[]> matrices = new List<Matrix4x4[]>();
    private void OnEnable() => Active.Add(this);
    private void OnDisable() => Active.Remove(this);

    private void Start()
    {
        for (int batchX = 0; batchX < 10; ++batchX)
        for (int batchY = 0; batchY < 10; ++batchY)
        {
            // 500 fits RenderMeshInstanced's default object-to-world/world-to-object capacity.
            var batch = new List<Matrix4x4>(500);
            for (int x = 0; x < 10; ++x)
            for (int y = 0; y < 10; ++y)
            for (int z = 0; z < 10; ++z)
            {
                batch.Add(Matrix4x4.Translate(new Vector3(batchX * 10 + x, y, batchY * 10 + z)));
                if (batch.Count == 500) { matrices.Add(batch.ToArray()); batch.Clear(); }
            }
        }
    }

    private void Update()
    {
        if (implementation != EImplementation.RenderMeshInstanced || mesh == null || material == null) return;
        var parameters = new RenderParams(material) { camera = targetCamera, shadowCastingMode = ShadowCastingMode.On, receiveShadows = true };
        foreach (var batch in matrices) Graphics.RenderMeshInstanced(parameters, mesh, 0, batch);
    }

    internal sealed class ComparisonPass : ScriptableRenderPass
    {
        private readonly EditorInstancingComparison owner;
        private sealed class Data { public EditorInstancingComparison owner; }
        internal ComparisonPass(EditorInstancingComparison owner)
        {
            this.owner = owner;
            renderPassEvent = owner.implementation == EImplementation.RenderGraphLate
                ? RenderPassEvent.AfterRenderingTransparents : RenderPassEvent.AfterRenderingOpaques;
        }
        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            using (var builder = graph.AddRasterRenderPass<Data>("Unturned instancing comparison", out var data))
            {
                data.owner = owner;
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.ReadWrite);
                builder.SetRenderFunc((Data pass, RasterGraphContext context) =>
                {
                    foreach (var batch in pass.owner.matrices)
                        context.cmd.DrawMeshInstanced(pass.owner.mesh, 0, pass.owner.material, pass.owner.shaderPass, batch, batch.Length);
                });
            }
        }
    }
}
#endif
