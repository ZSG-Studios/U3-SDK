using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SDG.Unturned;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class ConvertLegacyMapBundles
{
    public static IEnumerable<string> InstalledSources(string steam)
    {
        foreach (string map in Directory.GetDirectories(Path.Combine(steam, "Maps")).OrderBy(p => p, StringComparer.Ordinal))
            foreach (string name in new[] { "Ambience.unity3d", "Roads.unity3d" })
            {
                string source = Path.Combine(map, "Environment", name);
                if (File.Exists(source)) yield return source;
            }
    }

    public static bool InstalledCacheReady()
    {
        string steam = PortPreparation.FindSteamGame();
        foreach (string source in InstalledSources(steam))
        {
            string target = Path.Combine("Builds", "ConvertedBundles", Application.unityVersion, ModernBundleCache.Fingerprint(source) + ".unity3d");
            if (!ModernBundleCache.IsVerified(target)) return false;
        }
        return true;
    }
    [MenuItem("Tools/Unturned/Upgrade installed map ambience and road bundles")]
    public static void ConvertInstalledMaps()
    {
        string steam = PortPreparation.FindSteamGame();
        var results = new List<object>();
        foreach (string source in InstalledSources(steam)) results.Add(Convert(source));
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/legacy-map-bundle-conversion.json", Newtonsoft.Json.JsonConvert.SerializeObject(results, Newtonsoft.Json.Formatting.Indented));
        PortReproducibility.RecordPreparation(steam);
    }

    public static object Convert(string source)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before converting bundles");
        string hash = ModernBundleCache.Fingerprint(source);
        string output = Path.GetFullPath(Path.Combine("Builds", "ConvertedBundles", Application.unityVersion));
        Directory.CreateDirectory(output);
        string target = Path.Combine(output, hash + ".unity3d");
        if (ModernBundleCache.IsVerified(target)) return new { source, target, reused = true };
        string folder = "Assets/Generated/ModernMapBundles/" + hash;
        Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
        var bundle = AssetBundle.LoadFromFile(source);
        if (bundle == null) throw new InvalidDataException("Cannot read " + source);
        var paths = new List<string>();
        try
        {
            foreach (var asset in bundle.LoadAllAssets())
            {
                string path;
                if (asset is Texture2D texture)
                {
                    path = folder + "/" + texture.name + ".asset";
                    // Clone the complete native texture, including compressed mip data,
                    // wrap/filter settings, and color-space metadata. No PNG round trip.
                    // Editor serialization preserves native compressed mip data even
                    // when the source bundle's texture is non-readable. Instantiate
                    // rejects those textures in a fresh batch-mode import.
                    var clone = new Texture2D(2, 2);
                    EditorUtility.CopySerialized(texture, clone);
                    clone.name = texture.name;
                    var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (existing != null)
                    {
                        EditorUtility.CopySerialized(clone, existing);
                        UnityEngine.Object.DestroyImmediate(clone);
                        EditorUtility.SetDirty(existing);
                    }
                    else AssetDatabase.CreateAsset(clone, path);
                }
                else if (asset is AudioClip clip)
                {
                    // Native clip clones retain archive resource references. Decode
                    // from the loaded bundle and import a complete, self-contained WAV.
                    clip.UnloadAudioData();
                    var serialized = new SerializedObject(clip);
                    serialized.FindProperty("m_LoadType").intValue = (int)AudioClipLoadType.DecompressOnLoad;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    if (!clip.LoadAudioData() || clip.loadState != AudioDataLoadState.Loaded)
                        throw new InvalidOperationException("Audio not decoded: " + clip.name);
                    var samples = new float[checked(clip.samples * clip.channels)];
                    if (!clip.GetData(samples, 0)) throw new InvalidOperationException("Cannot decode " + clip.name);
                    path = folder + "/" + clip.name + ".wav";
                    WriteWave(path, clip, samples);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                    var settings = importer.defaultSampleSettings;
                    settings.loadType = AudioClipLoadType.DecompressOnLoad;
                    settings.compressionFormat = AudioCompressionFormat.PCM;
                    settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                    importer.defaultSampleSettings = settings;
                    importer.SaveAndReimport();
                    var imported = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (imported == null || imported.channels != clip.channels || imported.frequency != clip.frequency || Mathf.Abs(imported.samples - clip.samples) > 1)
                        throw new InvalidDataException("Audio import changed dimensions: " + clip.name);
                }
                else throw new NotSupportedException("Unexpected asset type in legacy map bundle: " + asset.GetType().FullName);
                paths.Add(path);
            }
            AssetDatabase.SaveAssets();
            var build = new AssetBundleBuild { assetBundleName = hash + ".unity3d", assetNames = paths.ToArray(),
                addressableNames = paths.Select(p => Path.GetFileName(p).ToLowerInvariant()).ToArray() };
            var manifest = BuildPipeline.BuildAssetBundles(output, new[] { build }, BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
            if (manifest == null || !File.Exists(target)) throw new InvalidOperationException("Failed to rebuild " + source);
            File.WriteAllText(target + ".verified", ModernBundleCache.VerificationText(target));
            return new { source, target, assets = paths.Count, reused = false };
        }
        finally { if (bundle != null) bundle.Unload(true); }
    }

    private static void WriteWave(string path, AudioClip clip, float[] samples)
    {
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            int bytes = checked(samples.Length * sizeof(float));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + bytes);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((ushort)3); writer.Write((ushort)clip.channels); writer.Write(clip.frequency);
            writer.Write(clip.frequency * clip.channels * sizeof(float)); writer.Write((ushort)(clip.channels * sizeof(float))); writer.Write((ushort)32);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(bytes);
            foreach (float sample in samples) writer.Write(sample);
        }
    }

}

public sealed class ModernMapBundleBuildCopy : IPostprocessBuildWithContext
{
    public int callbackOrder => 100;
    public void OnPostprocessBuild(BuildCallbackContext context)
    {
        var report = context.Report;
        // This modern callback also receives AssetBundle builds.
        if (!context.IsPlayerBuild) return;
        string source = Path.Combine("Builds", "ConvertedBundles", Application.unityVersion);
        if (!Directory.Exists(source)) throw new BuildFailedException("Required converted map cache was not prepared.");
        string target = Path.Combine(Path.GetDirectoryName(report.summary.outputPath), "ConvertedBundles", Application.unityVersion);
        Directory.CreateDirectory(target);
        var files = ConvertLegacyMapBundles.InstalledSources(PortPreparation.FindSteamGame())
            .Select(path => Path.Combine(source, ModernBundleCache.Fingerprint(path) + ".unity3d")).Distinct();
        foreach (string file in files)
        {
            if (!ModernBundleCache.IsVerified(file)) throw new BuildFailedException("Invalid prepared map bundle: " + Path.GetFileName(file));
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
            File.Copy(file + ".verified", Path.Combine(target, Path.GetFileName(file) + ".verified"), true);
        }
        // Direct development launches must initialize Steam rather than restart the installed game.
        File.Copy("steam_appid.txt", Path.Combine(Path.GetDirectoryName(report.summary.outputPath), "steam_appid.txt"), true);
    }
}
