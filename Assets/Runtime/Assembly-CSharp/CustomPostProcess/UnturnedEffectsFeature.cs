using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace SDG.Unturned
{
    /// <summary>Atmosphere and single-render scopes using URP's native RenderGraph resource lifetimes.</summary>
    public sealed class UnturnedEffectsFeature : ScriptableRendererFeature
    {
        public Shader skyFogShader;
        public Shader gaussianBlurShader;
        public Shader scopeVignetteShader;
        private FogPass fogPass;
        private ScopePass scopePass;
        private GeometryPass geometryPass;

        public override void Create()
        {
            fogPass?.Dispose();
            scopePass?.Dispose();
            fogPass = new FogPass(skyFogShader);
            scopePass = new ScopePass(gaussianBlurShader, scopeVignetteShader);
            geometryPass = new GeometryPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.renderType != CameraRenderType.Base) return;
#if UNITY_EDITOR
            foreach (var comparison in global::EditorInstancingComparison.Active)
                if (comparison.targetCamera == renderingData.cameraData.camera && comparison.mesh != null
                    && comparison.material != null && comparison.implementation != global::EditorInstancingComparison.EImplementation.RenderMeshInstanced)
                    renderer.EnqueuePass(new global::EditorInstancingComparison.ComparisonPass(comparison));
#endif
            if (skyFogShader != null) renderer.EnqueuePass(fogPass);
            if (gaussianBlurShader != null && scopeVignetteShader != null) renderer.EnqueuePass(scopePass);
            renderer.EnqueuePass(geometryPass);
        }

        protected override void Dispose(bool disposing)
        {
            fogPass?.Dispose();
            scopePass?.Dispose();
        }

        private static TextureHandle TemporaryColor(RenderGraph graph, TextureHandle source, string name)
        {
            var descriptor = graph.GetTextureDesc(source);
            descriptor.name = name;
            descriptor.clearBuffer = false;
            descriptor.depthBufferBits = DepthBits.None;
            descriptor.msaaSamples = MSAASamples.None;
            return graph.CreateTexture(descriptor);
        }

        private sealed class GeometryPass : ScriptableRenderPass
        {
            private sealed class PassData
            {
                public SDG.Framework.Rendering.GLRenderer owner;
                public int count;
            }
            public GeometryPass()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var camera = frameData.Get<UniversalCameraData>().camera;
                var owner = camera.GetComponent<SDG.Framework.Rendering.GLRenderer>();
                if (owner == null || !owner.isActiveAndEnabled) return;
                int count = owner.RecordGeometry();
                if (count == 0) return;
                var resources = frameData.Get<UniversalResourceData>();
                using (var builder = graph.AddRasterRenderPass<PassData>("Unturned editor and runtime gizmos", out var data))
                {
                    data.owner = owner;
                    data.count = count;
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                    builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    builder.SetRenderFunc((PassData pass, RasterGraphContext context) =>
                    {
                        for (int i = 0; i < pass.count; i++)
                        {
                            var draw = pass.owner.draws[i];
                            context.cmd.DrawMesh(draw.mesh, Matrix4x4.identity, draw.material, 0, draw.pass, draw.properties);
                        }
                    });
                }
            }
        }

        private sealed class FogPass : ScriptableRenderPass
        {
            private readonly Material material;
            private readonly SkyFogRenderer properties = new SkyFogRenderer();

            public FogPass(Shader shader)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
                requiresIntermediateTexture = true;
                ConfigureInput(ScriptableRenderPassInput.Depth);
                if (shader != null) material = CoreUtils.CreateEngineMaterial(shader);
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var camera = frameData.Get<UniversalCameraData>().camera;
                var stack = camera.GetUniversalAdditionalCameraData().volumeStack ?? VolumeManager.instance.stack;
                if (material == null || stack.GetComponent<SkyFog>()?.IsActive() != true) return;
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                var source = resources.activeColorTexture;
                var output = TemporaryColor(graph, source, "Unturned atmosphere color");
                var parameters = new RenderGraphUtils.BlitMaterialParameters(source, output, material, 0)
                {
                    propertyBlock = properties.Prepare(camera)
                };
                using (var builder = graph.AddBlitPass(parameters, "Unturned sky and underwater fog", returnBuilder: true))
                {
                    builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                }
                // URP has already captured the attachment for its transparent pass at
                // this injection point. Preserve that attachment so transparent water
                // and particles contribute to the same color target as the atmosphere.
                graph.AddBlitPass(output, source, Vector2.one, Vector2.zero,
                    passName: "Unturned atmosphere to camera attachment");
            }

            public void Dispose() => CoreUtils.Destroy(material);
        }

        private sealed class ScopePass : ScriptableRenderPass
        {
            private readonly Material blurMaterial;
            private readonly Material vignetteMaterial;
            private RenderTexture scopeTarget;
            private RTHandle scopeHandle;

            public ScopePass(Shader blurShader, Shader vignetteShader)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                requiresIntermediateTexture = true;
                if (blurShader != null) blurMaterial = CoreUtils.CreateEngineMaterial(blurShader);
                if (vignetteShader != null) vignetteMaterial = CoreUtils.CreateEngineMaterial(vignetteShader);
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var cameraData = frameData.Get<UniversalCameraData>();
                var stack = cameraData.camera.GetUniversalAdditionalCameraData().volumeStack ?? VolumeManager.instance.stack;
                var scope = stack.GetComponent<SrScope>();
                if (scope?.IsActive() != true) return;
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                var source = resources.activeColorTexture;
                var target = (RenderTexture)scope.renderTarget.value;
                if (target != scopeTarget)
                {
                    scopeHandle?.Release();
                    scopeTarget = target;
                    scopeHandle = RTHandles.Alloc(target);
                }

                int width = cameraData.cameraTargetDescriptor.width;
                int height = cameraData.cameraTargetDescriptor.height;
                var scale = width >= height ? new Vector2((float)height / width, 1f)
                    : new Vector2(1f, (float)width / height);
                var offset = (Vector2.one - scale) * 0.5f;
                var importedTarget = graph.ImportTexture(scopeHandle);
                using (var builder = graph.AddBlitPass(source, importedTarget, scale, offset,
                    passName: "Unturned single-render scope crop", returnBuilder: true))
                {
                    builder.AllowPassCulling(false); // The weapon material samples this external texture.
                }

                if (GraphicsSettings.WantsDarkScopePeripheral && scope.scopeAlpha.value > 0.001f)
                {
                    var output = TemporaryColor(graph, source, "Unturned scope peripheral");
                    var properties = new MaterialPropertyBlock();
                    properties.SetFloat("_ScopeAlpha", scope.scopeAlpha.value);
                    graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, output, vignetteMaterial, 0)
                        { propertyBlock = properties }, "Unturned dark scope peripheral");
                    resources.cameraColor = output;
                }
                else if (!GraphicsSettings.WantsDarkScopePeripheral && scope.standardDeviation.value > 0.001f)
                {
                    float deviation = scope.standardDeviation.value * Mathf.Min(width, height) / 1080f;
                    var properties = new MaterialPropertyBlock();
                    properties.SetFloat("_StdDeviationSquared", deviation * deviation);
                    properties.SetInt("_HalfKernelSize", Mathf.CeilToInt(deviation * 3f));
                    var horizontal = TemporaryColor(graph, source, "Unturned scope horizontal blur");
                    var vertical = TemporaryColor(graph, source, "Unturned scope vertical blur");
                    graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, horizontal, blurMaterial, 0)
                        { propertyBlock = properties }, "Unturned scope horizontal blur");
                    graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(horizontal, vertical, blurMaterial, 1)
                        { propertyBlock = properties }, "Unturned scope vertical blur");
                    resources.cameraColor = vertical;
                }
            }

            public void Dispose()
            {
                scopeHandle?.Release();
                scopeHandle = null;
                CoreUtils.Destroy(blurMaterial);
                CoreUtils.Destroy(vignetteMaterial);
            }
        }
    }
}
