using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class VerifyURPShaders
{
    public static object Main()
    {
        if (UnityEngine.Rendering.RenderPipelineManager.currentPipeline == null)
        {
            var fixture = new GameObject("URP validation camera");
            var target = new RenderTexture(64, 64, 24);
            try
            {
                var camera = fixture.AddComponent<Camera>();
                camera.targetTexture = target;
                global::Unturned.UnityEx.CameraRenderEx.Render(camera);
            }
            finally { UnityEngine.Object.DestroyImmediate(fixture); target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        }
        var errors = new List<object>();
        var warnings = new List<object>();
        int shaders = 0, passes = 0;
        bool previous = ShaderUtil.allowAsyncCompilation;
        var previousTarget = RenderTexture.active;
        var validationTarget = RenderTexture.GetTemporary(64, 64, 24);
        ShaderUtil.allowAsyncCompilation = false;
        // SetPass can create a native Vulkan render pass; keep valid attachments bound.
        Graphics.SetRenderTarget(validationTarget);
        try
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Shader", new[] { "Assets/Game/Sources/Shaders", "Assets/Runtime/Assembly-CSharp/CustomPostProcess" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null) continue;
                var material = new Material(shader);
                try
                {
                    if (material.GetTag("RenderPipeline", false) != "UniversalPipeline")
                    {
                        errors.Add(new { path, message = "The active subshader is not a URP pass." });
                        continue;
                    }
                    shaders++;
                    foreach (string keyword in new[] { "NICE_FOLIAGE_ON", "GRASS_WIND_ON", "GRASS_DISPLACEMENT_ON", "IS_SNOWING", "IS_RAINING", "TRIPLANAR_MAPPING_ON", "_GBUFFER_NORMALS_OCT", "_WRITE_SMOOTHNESS" })
                        material.EnableKeyword(keyword);
                    for (int i = 0; i < material.passCount; i++)
                    {
                        if (!material.SetPass(i)) errors.Add(new { path, message = "Could not bind pass " + i });
                        passes++;
                    }
                    foreach (var message in ShaderUtil.GetShaderMessages(shader))
                        if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                            errors.Add(new { path, message.message, message.line, platform = message.platform.ToString() });
                        else if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Warning)
                            warnings.Add(new { path, message.message, message.line, platform = message.platform.ToString() });
                }
                finally { UnityEngine.Object.DestroyImmediate(material); }
            }
        }
        finally
        {
            ShaderUtil.allowAsyncCompilation = previous;
            Graphics.SetRenderTarget(previousTarget);
            RenderTexture.ReleaseTemporary(validationTarget);
        }
        return new { shaders, passes, errors, warnings, passed = errors.Count == 0 && warnings.Count == 0,
            graphicsApi = SystemInfo.graphicsDeviceType.ToString() };
    }
}
