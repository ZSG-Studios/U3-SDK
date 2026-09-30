using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Compare request-based cubemap orientation with the engine's cubemap capture.</summary>
public static class VerifyURPReflections
{
    public static object Main()
    {
        if (RenderSettings.skybox == null) throw new InvalidOperationException("Load Menu or Tutorial before comparing sky reflections.");
        var fixture = new GameObject("URP reflection verification");
        var camera = fixture.AddComponent<Camera>();
        camera.enabled = false;
        camera.cullingMask = 0;
        camera.clearFlags = CameraClearFlags.Skybox;
        var reference = new RenderTexture(32, 32, 24) { dimension = TextureDimension.Cube };
        var actual = new RenderTexture(32, 32, 24) { dimension = TextureDimension.Cube };
        var scratch = RenderTexture.GetTemporary(32, 32, 0);
        var pixels = new Texture2D(32, 32, TextureFormat.RGB24, false);
        var previousActive = RenderTexture.active;
        try
        {
            reference.Create(); actual.Create();
            if (!camera.RenderToCubemap(reference)) throw new InvalidOperationException("Native reference capture failed");
            double difference = 0;
            var faceErrors = new double[6];
            var flipYErrors = new double[6];
            for (int face = 0; face < 6; face++)
            {
                global::Unturned.UnityEx.CameraRenderEx.RenderCubemapFace(camera, actual, face);
                Color32[] Read(RenderTexture cube)
                {
                    Graphics.CopyTexture(cube, face, 0, scratch, 0, 0);
                    RenderTexture.active = scratch;
                    pixels.ReadPixels(new Rect(0, 0, 32, 32), 0, 0);
                    pixels.Apply();
                    return pixels.GetPixels32();
                }
                var expected = Read(reference); var result = Read(actual);
                for (int i = 0; i < expected.Length; i++)
                {
                    double error = Math.Abs(expected[i].r - result[i].r)
                        + Math.Abs(expected[i].g - result[i].g) + Math.Abs(expected[i].b - result[i].b);
                    difference += error;
                    faceErrors[face] += error / (32 * 32 * 3);
                    var flipped = result[(31 - i / 32) * 32 + i % 32];
                    flipYErrors[face] += (Math.Abs(expected[i].r - flipped.r)
                        + Math.Abs(expected[i].g - flipped.g) + Math.Abs(expected[i].b - flipped.b)) / (double)(32 * 32 * 3);
                }
            }
            double meanError = difference / (6 * 32 * 32 * 3);
            if (meanError > 3.0) throw new InvalidOperationException("Cubemap orientation/color mismatch; mean RGB byte error " + meanError
                + "; faces: " + string.Join(", ", faceErrors) + "; vertical-flip comparison: " + string.Join(", ", flipYErrors));
            return new { passed = true, faces = 6, meanRgbByteError = meanError,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString() };
        }
        finally
        {
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(scratch);
            reference.Release(); actual.Release();
            UnityEngine.Object.DestroyImmediate(reference); UnityEngine.Object.DestroyImmediate(actual);
            UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(fixture);
        }
    }
}
