using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SDG.Framework.Water;

public static class InspectWaterRendering
{
    public static object Main()
    {
        var volume = UnityEngine.Object.FindObjectsByType<WaterVolume>()
            .Where(v => v.waterPlane != null && v.waterPlane.activeInHierarchy)
            .OrderByDescending(v => v.transform.localScale.x * v.transform.localScale.z).First();
        var renderer = volume.GetComponentsInChildren<Renderer>().First();
        var center = renderer.bounds.center;
        var fixture = new GameObject("Water rendering inspection");
        var camera = fixture.AddComponent<Camera>();
        camera.enabled = false;
        camera.CopyFrom(Camera.main);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;
        camera.orthographicSize = 40;
        camera.transform.SetPositionAndRotation(center + Vector3.up * 100, Quaternion.LookRotation(Vector3.down, Vector3.forward));
        var data = camera.GetUniversalAdditionalCameraData();
        data.SetRenderer(0);
        data.renderPostProcessing = false;
        data.volumeLayerMask = 0;
        var target = RenderTexture.GetTemporary(256, 256, 24);
        var previousActive = RenderTexture.active;
        var pixels = new Texture2D(256, 256, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.cullingMask = 1 << renderer.gameObject.layer;
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); pixels.Apply();
            var colors = pixels.GetPixels32();
            int visiblePixels = colors.Count(c => c.r > 5 || c.g > 5 || c.b > 5);
            string path = Path.GetFullPath("Logs/water-isolated-before.png");
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            return new { shader = volume.sharedMaterial.shader.name, visiblePixels, totalPixels = colors.Length,
                center = center.ToString(), screenshot = path, sample = pixels.GetPixel(128, 128).ToString() };
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(pixels);
            UnityEngine.Object.DestroyImmediate(fixture);
        }
    }
}
