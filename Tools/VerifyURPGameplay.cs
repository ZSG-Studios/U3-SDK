using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SDG.Unturned;
using SDG.Framework.Rendering;

public static class VerifyURPGameplay
{
    public static object Main()
    {
        if (!Level.isLoaded || Player.LocalPlayer == null) throw new InvalidOperationException("Load Tutorial before running the gameplay fixture.");
        var camera = Camera.main;
        var data = camera.GetUniversalAdditionalCameraData();
        if (data.cameraStack.Count != 1 || data.cameraStack[0].GetUniversalAdditionalCameraData().renderType != CameraRenderType.Overlay)
            throw new InvalidOperationException("Expected one native viewmodel overlay camera");
        SrScope scope = null;
        foreach (var volume in UnityEngine.Object.FindObjectsByType<Volume>())
            if (volume.gameObject.name == "Base") volume.sharedProfile.TryGet(out scope);
        if (scope == null) throw new InvalidOperationException("Missing native scope volume");
        bool previousActive = scope.active, previousDark = SDG.Unturned.GraphicsSettings.WantsDarkScopePeripheral;
        var previousScopeTarget = scope.renderTarget.value;
        float previousDeviation = scope.standardDeviation.value, previousAlpha = scope.scopeAlpha.value;
        var previousCameraTarget = camera.targetTexture;
        var previousRenderTarget = RenderTexture.active;
        var target = RenderTexture.GetTemporary(640, 360, 24, RenderTextureFormat.ARGBHalf);
        var scopeTarget = RenderTexture.GetTemporary(256, 256, 0, RenderTextureFormat.ARGBHalf);
        var pixels = new Texture2D(256, 256, TextureFormat.RGB24, false);
        var geometry = camera.GetComponent<GLRenderer>();
        bool ownsGeometry = geometry == null;
        if (ownsGeometry) geometry = camera.gameObject.AddComponent<GLRenderer>();
        try
        {
            camera.targetTexture = target;
            scope.renderTarget.Override(scopeTarget);
            scope.standardDeviation.Override(2f);
            scope.scopeAlpha.Override(1f);
            scope.active = true;
            SDG.Unturned.GraphicsSettings.WantsDarkScopePeripheral = false;
            RuntimeGizmos.Get().Line(camera.transform.position + camera.transform.forward * 2f - camera.transform.right,
                camera.transform.position + camera.transform.forward * 2f + camera.transform.right, Color.green, 0.5f);
            global::Unturned.UnityEx.CameraRenderEx.Render(camera);
            int geometryDraws = geometry.draws.Count;
            if (geometryDraws == 0) throw new InvalidOperationException("The RenderGraph gizmo pass did not record geometry");
            RenderTexture.active = scopeTarget;
            pixels.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
            pixels.Apply();
            double brightness = 0;
            foreach (var pixel in pixels.GetPixels32()) brightness += pixel.r + pixel.g + pixel.b;
            if (brightness == 0) throw new InvalidOperationException("The scope crop remained black");
            SDG.Unturned.GraphicsSettings.WantsDarkScopePeripheral = true;
            global::Unturned.UnityEx.CameraRenderEx.Render(camera);
            return new { passed = true, pipeline = RenderPipelineManager.currentPipeline.GetType().Name,
                overlayCameras = data.cameraStack.Count, geometryDraws, scopeBrightness = brightness,
                effects = new[] { "single-render scope crop", "separable peripheral blur", "dark peripheral vignette", "RenderGraph gizmo geometry" } };
        }
        finally
        {
            camera.targetTexture = previousCameraTarget;
            scope.active = previousActive;
            scope.renderTarget.value = previousScopeTarget;
            scope.standardDeviation.value = previousDeviation;
            scope.scopeAlpha.value = previousAlpha;
            SDG.Unturned.GraphicsSettings.WantsDarkScopePeripheral = previousDark;
            RenderTexture.active = previousRenderTarget;
            RenderTexture.ReleaseTemporary(target);
            RenderTexture.ReleaseTemporary(scopeTarget);
            UnityEngine.Object.DestroyImmediate(pixels);
            if (ownsGeometry) UnityEngine.Object.DestroyImmediate(geometry);
        }
    }
}
