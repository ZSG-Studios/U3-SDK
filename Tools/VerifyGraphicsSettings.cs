using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SDG.Unturned;
using Settings = SDG.Unturned.GraphicsSettings;

public static class VerifyGraphicsSettings
{
    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    static void Callback(Type owner, string name, object value)
    {
        owner.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { null, value });
    }

    public static object Main()
    {
        Require(Level.isLoaded && Player.LocalPlayer != null, "Load a map before testing settings");
        var owner = typeof(Settings);
        var dataField = owner.GetField("graphicsSettingsData", BindingFlags.Static | BindingFlags.NonPublic);
        object original = dataField.GetValue(null);
        var overrideFields = new[] { "userOverrodeFrameRateLimit", "didCacheUIScaleOverride", "cachedUIScaleOverride" }
            .Select(n => owner.GetField(n, BindingFlags.Static | BindingFlags.NonPublic)).ToArray();
        var overrides = overrideFields.Select(f => f.GetValue(null)).ToArray();
        dataField.SetValue(null, JsonConvert.DeserializeObject<GraphicsSettingsData>(JsonConvert.SerializeObject(original)));
        var rows = new List<object>();
        GameObject host = null;
        Material material = null;
        RenderTexture target = null;
        Texture2D pixels = null;
        VolumeProfile fixtureProfile = null;
        var previousActive = RenderTexture.active;
        var mainCamera = MainCamera.instance;
        var previousMainTarget = mainCamera.targetTexture;
        var mainTarget = RenderTexture.GetTemporary(640, 360, 24);
        mainCamera.targetTexture = mainTarget;
        try
        {
            for (int mode = 0; mode < 2; mode++)
            {
                Callback(typeof(MenuConfigurationGraphicsUI), "onSwappedRenderState", mode);
                for (int quality = 0; quality < 5; quality++)
                {
                    Callback(typeof(MenuConfigurationGraphicsUI), "onSwappedLightingState", quality);
                    var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                    Require(pipeline != null, "Quality selected a non-URP pipeline");
                    Require(pipeline.supportsMainLightShadows == (quality > 0), "Lighting Off did not disable main shadows");
                    Require(pipeline.supportsAdditionalLightShadows == (quality > 0), "Lighting Off did not disable additional shadows");
                    Require(pipeline.mainLightShadowmapResolution == new[] {512,512,1024,2048,4096}[quality], "Shadow resolution did not follow lighting quality");
                    Require(Math.Abs(pipeline.shadowDistance - new[] {0f,100f,200f,300f,400f}[quality]) < .1f, "Shadow distance did not follow lighting quality");
                    int rendererIndex = Settings.renderMode == ERenderMode.DEFERRED ? 1 : 0;
                    Require(MainCamera.instance.GetUniversalAdditionalCameraData().scriptableRenderer == pipeline.GetRenderer(rendererIndex), "Main camera did not switch renderer");
                    Require(Player.LocalPlayer.animator.viewmodelCamera.GetUniversalAdditionalCameraData().scriptableRenderer == pipeline.GetRenderer(0), "Viewmodel lost Forward+ renderer");
                    global::Unturned.UnityEx.CameraRenderEx.Render(MainCamera.instance);
                    rows.Add(new { mode, quality, pipeline = pipeline.name, shadowDistance = pipeline.shadowDistance, resolution = pipeline.mainLightShadowmapResolution });
                }
                Callback(typeof(MenuConfigurationGraphicsUI), "onSwappedReflectionState", 3);
                var baseVolume = UnityEngine.Object.FindObjectsByType<Volume>().First(v => v.name == "Base");
                Require(baseVolume.sharedProfile.TryGet(out ScreenSpaceReflectionVolumeSettings reflection) && reflection.mode.value == ScreenSpaceReflectionVolumeSettings.ReflectionMode.OpaquesOnly, "SSR did not enable in renderer " + mode);
                Require(reflection.maxRaySteps.value == 64, "SSR ray budget did not update");
                global::Unturned.UnityEx.CameraRenderEx.Render(MainCamera.instance);
                Callback(typeof(MenuConfigurationGraphicsUI), "onSwappedReflectionState", 0);
                Require(reflection.mode.value == ScreenSpaceReflectionVolumeSettings.ReflectionMode.Disabled, "SSR Off did not deactivate its volume");
            }

            var cameras = new[] {MainCamera.instance, Player.LocalPlayer.look.scopeCamera, Player.LocalPlayer.animator.viewmodelCamera};
            for (int aa = 0; aa < 4; aa++)
            {
                Callback(typeof(MenuConfigurationGraphicsUI), "onSwappedAntiAliasingState", aa);
                foreach (var gameCamera in cameras)
                {
                    var additional = gameCamera.GetUniversalAdditionalCameraData();
                    var expected = aa == 0 ? AntialiasingMode.None : aa == 1 ? AntialiasingMode.FastApproximateAntialiasing
                        : aa == 2 && additional.renderType == CameraRenderType.Base && additional.cameraStack.Count == 0
                        ? AntialiasingMode.TemporalAntiAliasing : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    Require(additional.antialiasing == expected, "AA did not update " + gameCamera.name);
                }
                global::Unturned.UnityEx.CameraRenderEx.Render(MainCamera.instance);
            }
            var overlayCamera = Player.LocalPlayer.animator.viewmodelCamera;
            bool overlayEnabled = overlayCamera.enabled;
            try
            {
                Callback(typeof(MenuConfigurationGraphicsUI), "onToggledBloomToggle", true);
                overlayCamera.enabled = false;
                UnturnedPostProcess.instance.notifyPerspectiveChanged();
                var baseVolume = UnityEngine.Object.FindObjectsByType<Volume>().First(v => v.name == "Base");
                Require(baseVolume.sharedProfile.TryGet(out Bloom bloom) && bloom.active, "Bloom was left on a disabled viewmodel camera");
                global::Unturned.UnityEx.CameraRenderEx.Render(MainCamera.instance);
            }
            finally
            {
                overlayCamera.enabled = overlayEnabled;
                UnturnedPostProcess.instance.notifyPerspectiveChanged();
            }

            Callback(typeof(MenuConfigurationDisplayUI), "onToggledBufferToggle", false);
            Callback(typeof(MenuConfigurationDisplayUI), "OnTypedTargetFrameRate", 41u);
            Require(Settings.UseTargetFrameRate && Application.targetFrameRate == 41, "Typing the active launch cap did not preserve its enabled state");
            Callback(typeof(MenuConfigurationDisplayUI), "OnToggledUnfocusedTargetFrameRate", false);
            Callback(typeof(MenuConfigurationDisplayUI), "OnToggledTargetFrameRate", true);
            Callback(typeof(MenuConfigurationDisplayUI), "OnTypedTargetFrameRate", 37u);
            Require(Application.targetFrameRate == 37, "Launch frame limit blocked menu frame limit");
            Callback(typeof(MenuConfigurationDisplayUI), "onToggledBufferToggle", true);
            Require(QualitySettings.vSyncCount == 1 && Application.targetFrameRate == -1, "VSync did not override frame limit");
            Callback(typeof(MenuConfigurationDisplayUI), "onToggledBufferToggle", false);
            Require(Application.targetFrameRate == 37 && QualitySettings.vSyncCount == 0, "Frame limit did not resume after disabling VSync");
            Callback(typeof(MenuConfigurationDisplayUI), "onTypedUserInterfaceScale", 1.25f);
            Require(Math.Abs(Settings.userInterfaceScale - 1.25f) < .001f, "UI scale did not apply");
            var selected = new GraphicsSettingsResolution(new Resolution {width=1920, height=1080, refreshRateRatio=new RefreshRate { numerator=60000, denominator=1001 }});
            var roundtrip = JsonConvert.DeserializeObject<GraphicsSettingsResolution>(JsonConvert.SerializeObject(selected));
            Require(roundtrip.RefreshRateNumerator == 60000 && roundtrip.RefreshRateDenominator == 1001, "Fractional refresh rate was lost in serialization");

            host = new GameObject("Graphics settings image fixture");
            Settings.filmGrain = false; Settings.bloom = false; Settings.chromaticAberration = false;
            Settings.apply("isolate ambient occlusion image comparison");
            host.layer = UnturnedPostProcess.BASE_LAYER;
            var volume = host.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 1000;
            fixtureProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            fixtureProfile.Add<SkyFog>(true).effectEnabled.Override(false);
            volume.sharedProfile = fixtureProfile;
            var cameraObject = new GameObject("AO settings test camera"); cameraObject.transform.SetParent(host.transform);
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            var origin = new Vector3(10000, 1000, 10000);
            var lightObject = new GameObject("AO fixture lighting"); lightObject.transform.SetParent(host.transform);
            lightObject.transform.rotation = Quaternion.Euler(50, 20, 0);
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional;
            light.intensity = 2; light.cullingMask = 1; light.shadows = LightShadows.None;
            camera.transform.position = origin + new Vector3(3, 3, -5);
            camera.transform.LookAt(origin + Vector3.up * .3f);
            camera.nearClipPlane = .1f; camera.farClipPlane = 20;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.gray;
            camera.cullingMask = 1;
            var additionalData = camera.GetUniversalAdditionalCameraData();
            additionalData.SetRenderer(0); additionalData.renderPostProcessing = true;
            additionalData.volumeLayerMask = 1 << UnturnedPostProcess.BASE_LAYER;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.SetFloat("_Smoothness", 0);
            foreach (var position in new[] {Vector3.zero, new Vector3(1,0,0)})
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.transform.SetParent(host.transform);
                cube.transform.position = origin + position + Vector3.up * .5f;
                cube.GetComponent<Renderer>().sharedMaterial = material;
            }
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.SetParent(host.transform);
            floor.transform.position = origin + Vector3.down * .1f; floor.transform.localScale = new Vector3(10,.2f,10);
            floor.GetComponent<Renderer>().sharedMaterial = material;
            target = RenderTexture.GetTemporary(256,256,24); camera.targetTexture = target;
            pixels = new Texture2D(256,256,TextureFormat.RGB24,false);
            var request = new RenderPipeline.StandardRequest {destination=target};
            var changedByMode = new List<int>();
            foreach (int mode in new[] {0,1})
            {
                additionalData.SetRenderer(mode);
                Callback(typeof(MenuConfigurationGraphicsUI), "onToggledAmbientOcclusion", false);
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0,0,256,256),0,0); pixels.Apply();
                var off = pixels.GetPixels32();
                Callback(typeof(MenuConfigurationGraphicsUI), "onToggledAmbientOcclusion", true);
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0,0,256,256),0,0); pixels.Apply();
                var on = pixels.GetPixels32(); int changed = 0;
                for (int i=0;i<off.Length;i++)
                    if (Math.Abs(off[i].r-on[i].r)+Math.Abs(off[i].g-on[i].g)+Math.Abs(off[i].b-on[i].b)>2) changed++;
                Require(changed > 10, "AO toggle did not change rendered pixels in renderer " + mode + ": " + changed);
                changedByMode.Add(changed);
            }
            return new {passed=true, lightingPresets=rows, aaModes=4, displayChecks=new[] {"frame limit overrides launch limit", "VSync", "UI scale", "fractional refresh serialization"}, aoChangedPixels=changedByMode};
        }
        finally
        {
            mainCamera.targetTexture = previousMainTarget;
            RenderTexture.active = previousActive;
            if (host != null)
                foreach (var ownedCamera in host.GetComponentsInChildren<Camera>()) ownedCamera.targetTexture = null;
            if (host != null) UnityEngine.Object.DestroyImmediate(host);
            if (fixtureProfile != null)
            {
                foreach (var component in fixtureProfile.components) UnityEngine.Object.DestroyImmediate(component);
                UnityEngine.Object.DestroyImmediate(fixtureProfile);
            }
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
            if (target != null) RenderTexture.ReleaseTemporary(target);
            RenderTexture.ReleaseTemporary(mainTarget);
            dataField.SetValue(null, original);
            for (int i=0;i<overrideFields.Length;i++) overrideFields[i].SetValue(null, overrides[i]);
            Settings.apply("restore graphics/display qualification settings");
        }
    }
}
