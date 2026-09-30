using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SDG.Framework.Water;

public static class VerifyURPWater
{
    public static object Main()
    {
        var volume = UnityEngine.Object.FindObjectsByType<WaterVolume>()
            .Where(v => v.waterPlane != null && v.waterPlane.activeInHierarchy)
            .OrderByDescending(v => v.transform.localScale.x * v.transform.localScale.z).First();
        var renderer = volume.waterPlane.GetComponentInChildren<Renderer>();
        var fixture = new GameObject("Water and atmosphere regression camera");
        var camera = fixture.AddComponent<Camera>();
        camera.enabled = false;
        camera.CopyFrom(Camera.main);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.magenta;
        camera.orthographic = true; camera.orthographicSize = 40;
        camera.transform.SetPositionAndRotation(renderer.bounds.center + Vector3.up * 80, Quaternion.LookRotation(Vector3.down, Vector3.forward));
        var data = camera.GetUniversalAdditionalCameraData();
        data.SetRenderer(1); data.renderPostProcessing = false;
        data.volumeLayerMask = Camera.main.GetUniversalAdditionalCameraData().volumeLayerMask;
        data.volumeTrigger = Camera.main.transform;
        var overlayObject = new GameObject("Water camera-stack regression overlay");
        overlayObject.transform.SetParent(fixture.transform);
        var overlay = overlayObject.AddComponent<Camera>();
        overlay.CopyFrom(camera); overlay.enabled = true; overlay.cullingMask = 0;
        overlay.clearFlags = CameraClearFlags.Depth;
        var overlayData = overlay.GetUniversalAdditionalCameraData();
        overlayData.renderType = CameraRenderType.Overlay; overlayData.SetRenderer(0);
        overlayData.renderPostProcessing = false; overlayData.volumeLayerMask = 0;
        data.cameraStack.Add(overlay);
        var target = RenderTexture.GetTemporary(256, 256, 24);
        var previousActive = RenderTexture.active;
        var pixels = new Texture2D(256, 256, TextureFormat.RGB24, false);
        bool visible = volume.waterPlane.activeSelf;
        try
        {
            camera.targetTexture = target;
            camera.cullingMask = 1 << renderer.gameObject.layer;
            VolumeManager.instance.Update(camera.transform, data.volumeLayerMask);
            bool fogActive = VolumeManager.instance.stack.GetComponent<SDG.Unturned.SkyFog>()?.IsActive() == true;
            if (!fogActive) throw new InvalidOperationException("Atmosphere was not enabled for this regression test");
            var request = new RenderPipeline.StandardRequest { destination = target };
            RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); pixels.Apply();
            var water = pixels.GetPixels32();
            string screenshot = Path.GetFullPath("Logs/water-with-atmosphere.png");
            File.WriteAllBytes(screenshot, pixels.EncodeToPNG());
            volume.waterPlane.SetActive(false);
            RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); pixels.Apply();
            var empty = pixels.GetPixels32(); int changed = 0;
            for (int i = 0; i < water.Length; i++)
                if (Math.Abs(water[i].r - empty[i].r) + Math.Abs(water[i].g - empty[i].g) + Math.Abs(water[i].b - empty[i].b) > 30) changed++;
            if (changed < water.Length * .9) throw new InvalidOperationException("Transparent water disappeared with atmosphere enabled: " + changed);
            return new { passed = true, fogActive, overlayCameras = data.cameraStack.Count, changedPixels = changed, totalPixels = water.Length, screenshot, api = SystemInfo.graphicsDeviceType.ToString() };
        }
        finally
        {
            volume.waterPlane.SetActive(visible); camera.targetTexture = null;
            RenderTexture.active = previousActive; RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(fixture);
        }
    }
}
