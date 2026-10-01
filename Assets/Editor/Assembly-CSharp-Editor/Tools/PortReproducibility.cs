using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SDG.Unturned;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Native Unity batch entry point: no private caches, CLI server, or local build preferences required.</summary>
public static class PortReproducibility
{
    public const string EditorVersion = "6000.7.0b2";

    public static void UpgradeEmbeddedImportMetadata()
    {
        AssetDatabase.ForceReserializeAssets(new[]
        {
            "Assets/TextMesh Pro/Fonts/LiberationSans.ttf",
            "Packages/com.unity.render-pipelines.universal/Shaders/AutodeskInteractive/AutodeskInteractive.shadergraph",
            "Packages/com.unity.render-pipelines.universal/Shaders/AutodeskInteractive/AutodeskInteractiveMasked.shadergraph",
            "Packages/com.unity.render-pipelines.universal/Shaders/AutodeskInteractive/AutodeskInteractiveTransparent.shadergraph",
            "Packages/com.unity.render-pipelines.core/Editor/StyleSheets/RenderGraphViewer.uss",
        }, ForceReserializeAssetsOptions.ReserializeMetadata);
    }

    public static void RecordPreparation(string steam)
    {
        var inputs = new List<object>();
        foreach (string source in ConvertLegacyMapBundles.InstalledSources(steam))
            inputs.Add(new
            {
                relativePath = Path.GetRelativePath(steam, source).Replace('\\', '/'),
                sha256 = ModernBundleCache.Fingerprint(source),
            });
        string manifest = Path.Combine(new DirectoryInfo(steam).Parent.Parent.FullName, "appmanifest_304930.acf");
        string buildId = File.Exists(manifest) ? Regex.Match(File.ReadAllText(manifest), "\"buildid\"\\s*\"([^\"]+)\"").Groups[1].Value : null;
        bool? matchesBaseline = null;
        if (File.Exists("Tools/reproduction-baseline.json"))
        {
            var baseline = JObject.Parse(File.ReadAllText("Tools/reproduction-baseline.json"));
            matchesBaseline = (string)baseline["editor"] == Application.unityVersion &&
                (string)baseline["steamBuildId"] == buildId && JToken.DeepEquals(baseline["mapBundleInputs"], JToken.FromObject(inputs));
        }
        File.WriteAllText("Logs/project-preparation.json", JsonConvert.SerializeObject(new
        {
            editor = Application.unityVersion,
            steamBuildId = buildId,
            sourceMatchesRecordedBaseline = matchesBaseline,
            mapBundleInputs = inputs,
        }, Formatting.Indented));
    }

    public static void Prepare()
    {
        if (Application.unityVersion != EditorVersion)
            throw new BuildFailedException("Use the pinned Unity " + EditorVersion + " editor for this port; running " + Application.unityVersion);
        TMProSetup.EnsureReady();
        ConvertLegacyMapBundles.ConvertInstalledMaps();
    }

    [MenuItem("Tools/Unturned/Build reproducible Windows development client")]
    public static void BuildWindowsDevelopment()
    {
        if (Application.unityVersion != EditorVersion)
            throw new BuildFailedException("Use Unity " + EditorVersion + " to reproduce this port.");
        TMProSetup.EnsureReady();
        const string profilePath = "Assets/Settings/Build Profiles/Unturned Windows DX12 Vulkan.asset";
        var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(profilePath);
        if (profile == null) throw new BuildFailedException("Missing committed build profile: " + profilePath);
        Directory.CreateDirectory("Logs");
        Directory.CreateDirectory("Temp");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions
        {
            buildProfile = profile,
            locationPathName = "Builds/Windows64/Unturned.exe",
            options = BuildOptions.Development | BuildOptions.CompressWithLz4,
        });
        var summary = report.summary;
        // SRP populates its runtime-only settings list while serializing a player.
        // Restore the native Editor representation after the build, so a build
        // does not leave a machine's generated runtime cache in tracked source.
        AssetDatabase.ForceReserializeAssets(new[] { "Assets/UniversalRenderPipelineGlobalSettings.asset" });
        var result = new
        {
            buildId = "reproduce_" + Guid.NewGuid().ToString("N"),
            status = "completed",
            result = summary.result.ToString(),
            totalErrors = summary.totalErrors,
            totalWarnings = summary.totalWarnings,
            totalSeconds = summary.totalTime.TotalSeconds,
            editor = Application.unityVersion,
            profile = profilePath,
            outputPath = summary.outputPath,
        };
        string json = JsonConvert.SerializeObject(result, Formatting.Indented);
        File.WriteAllText("Logs/reproducible-build.json", json);
        File.WriteAllText("Temp/pipeline_build_status.json", json);
        if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0 || summary.totalWarnings != 0)
            throw new BuildFailedException("Reproduction build did not pass the zero-error/warning gate. See Logs/reproducible-build.json and the Editor log.");
        Debug.Log("Reproduction build passed: " + json);
    }
}
