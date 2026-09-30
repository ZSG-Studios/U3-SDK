using System;
using System.IO;
using UnityEngine;

public static class CaptureURPCamera
{
    public static object Main()
    {
        var camera = Camera.main;
        if (camera == null) throw new InvalidOperationException("No main camera");
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGBHalf);
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        string path = Path.GetFullPath("Logs/urp-" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.ToLowerInvariant() + ".png");
        try
        {
            camera.targetTexture = target;
            global::Unturned.UnityEx.CameraRenderEx.Render(camera);
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(path, ImageConversion.EncodeToPNG(pixels));
            return new { path, pipeline = UnityEngine.Rendering.RenderPipelineManager.currentPipeline?.GetType().Name };
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(pixels);
        }
    }
}
