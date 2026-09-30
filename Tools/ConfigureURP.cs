using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SDG.Unturned;

public static class ConfigureURP
{
    public static object Main()
    {
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
            new[] { GraphicsDeviceType.Direct3D12, GraphicsDeviceType.Vulkan });
        const string folder = "Assets/Settings/URP";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Settings", "URP");
        var forward = Renderer(folder + "/ForwardPlus.asset", RenderingMode.ForwardPlus);
        var deferred = Renderer(folder + "/DeferredPlus.asset", RenderingMode.DeferredPlus);
        string pipelinePath = folder + "/Unturned.asset";
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
        if (pipeline == null)
        {
            pipeline = UniversalRenderPipelineAsset.Create(forward);
            AssetDatabase.CreateAsset(pipeline, pipelinePath);
        }
        var serialized = new SerializedObject(pipeline);
        var renderers = serialized.FindProperty("m_RendererDataList");
        renderers.arraySize = 2;
        renderers.GetArrayElementAtIndex(0).objectReferenceValue = forward;
        renderers.GetArrayElementAtIndex(1).objectReferenceValue = deferred;
        serialized.FindProperty("m_MainLightShadowsSupported").boolValue = true;
        serialized.FindProperty("m_AdditionalLightShadowsSupported").boolValue = true;
        serialized.FindProperty("m_SoftShadowsSupported").boolValue = true;
        serialized.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        pipeline.supportsCameraDepthTexture = true;
        pipeline.supportsCameraOpaqueTexture = true;
        pipeline.supportsHDR = true;
        pipeline.msaaSampleCount = 1;
        pipeline.useSRPBatcher = true;
        pipeline.shadowDistance = 256;
        pipeline.shadowCascadeCount = 4;
        var presets = new UniversalRenderPipelineAsset[5];
        var distances = new[] { 0f, 100f, 200f, 300f, 400f };
        var resolutions = new[] { 512, 512, 1024, 2048, 4096 };
        var cascades = new[] { 1, 1, 2, 4, 4 };
        for (int i = 0; i < presets.Length; i++)
        {
            string path = folder + "/Unturned" + ((EGraphicQuality)i) + ".asset";
            var preset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (preset == null)
            {
                preset = UnityEngine.Object.Instantiate(pipeline);
                AssetDatabase.CreateAsset(preset, path);
            }
            var settings = new SerializedObject(preset);
            var list = settings.FindProperty("m_RendererDataList");
            list.arraySize = 2;
            list.GetArrayElementAtIndex(0).objectReferenceValue = forward;
            list.GetArrayElementAtIndex(1).objectReferenceValue = deferred;
            settings.FindProperty("m_MainLightShadowsSupported").boolValue = i > 0;
            settings.FindProperty("m_AdditionalLightShadowsSupported").boolValue = i > 0;
            settings.FindProperty("m_SoftShadowsSupported").boolValue = i == 4;
            settings.ApplyModifiedPropertiesWithoutUndo();
            preset.shadowDistance = distances[i];
            preset.shadowCascadeCount = cascades[i];
            preset.mainLightShadowmapResolution = resolutions[i];
            preset.additionalLightsShadowmapResolution = Mathf.Max(256, resolutions[i] / 2);
            presets[i] = preset;
            EditorUtility.SetDirty(preset);
        }
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = presets[3];
        int previousQuality = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = presets[Mathf.Clamp(i - 1, 0, 4)];
        }
        QualitySettings.SetQualityLevel(previousQuality, false);
        // Dynamically-created gameplay volume profiles must retain their post-processing variants.
        var stripping = UnityEngine.Rendering.GraphicsSettings.GetRenderPipelineSettings<URPShaderStrippingSetting>();
        if (stripping != null) stripping.stripUnusedPostProcessingVariants = false;
        EditorUtility.SetDirty(pipeline);
        AssetDatabase.SaveAssets();
        return new { pipeline = pipelinePath, rendererModes = new[] { "ForwardPlus", "DeferredPlus" } };
    }

    static UniversalRendererData Renderer(string path, RenderingMode mode)
    {
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(data, path);
        }
        data.renderingMode = mode;
        if (!data.rendererFeatures.OfType<UnturnedEffectsFeature>().Any())
        {
            var effects = ScriptableObject.CreateInstance<UnturnedEffectsFeature>();
            effects.name = "Unturned atmosphere and scope";
            effects.skyFogShader = Shader.Find("Hidden/Custom/SkyFog");
            effects.gaussianBlurShader = Shader.Find("Hidden/Custom/GaussianBlur");
            effects.scopeVignetteShader = Shader.Find("Hidden/Custom/ScopeVignette");
            AssetDatabase.AddObjectToAsset(effects, data);
            data.rendererFeatures.Add(effects);
        }
        if (!data.rendererFeatures.OfType<DecalRendererFeature>().Any())
        {
            var feature = ScriptableObject.CreateInstance<DecalRendererFeature>();
            feature.name = "URP projected decals";
            AssetDatabase.AddObjectToAsset(feature, data);
            data.rendererFeatures.Add(feature);
        }
        if (!data.rendererFeatures.OfType<ScreenSpaceReflectionRendererFeature>().Any())
        {
            var feature = ScriptableObject.CreateInstance<ScreenSpaceReflectionRendererFeature>();
            feature.name = "URP screen-space reflections";
            AssetDatabase.AddObjectToAsset(feature, data);
            data.rendererFeatures.Add(feature);
        }
        if (!data.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().Any())
        {
            var feature = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            feature.name = "URP ambient occlusion";
            AssetDatabase.AddObjectToAsset(feature, data);
            data.rendererFeatures.Add(feature);
        }
        foreach (var decal in data.rendererFeatures.OfType<DecalRendererFeature>())
        {
            // Unturned decals modify albedo/emission. Native screen-space projection avoids
            // a DBuffer prepass and selects GBuffer projection in Deferred+ automatically.
            var serialized = new SerializedObject(decal);
            serialized.FindProperty("m_Settings.technique").intValue = 2;
            serialized.FindProperty("m_Settings.screenSpaceSettings.normalBlend").intValue = 2;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(decal);
        }
        data.SetDirty();
        EditorUtility.SetDirty(data);
        return data;
    }
}
