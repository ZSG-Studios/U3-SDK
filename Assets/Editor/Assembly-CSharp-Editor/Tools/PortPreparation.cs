using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Scripting.LifecycleManagement;
using UnityEngine;

/// <summary>Recreate required, untracked Steam-derived content on a fresh checkout.</summary>
public static partial class PortPreparation
{
    public static string FindSteamGame()
    {
        string explicitPath = Environment.GetEnvironmentVariable("UNTURNED_ASSET_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            if (IsGame(explicitPath)) return Path.GetFullPath(explicitPath);
            throw new DirectoryNotFoundException("UNTURNED_ASSET_DIRECTORY must point to the installed Unturned folder containing Maps and Bundles.");
        }
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
#if UNITY_EDITOR_WIN
        using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
        {
            string steamPath = key?.GetValue("SteamPath") as string;
            if (!string.IsNullOrEmpty(steamPath)) roots.Add(steamPath);
        }
#endif
        roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        var libraries = new HashSet<string>(roots, StringComparer.OrdinalIgnoreCase);
        foreach (string root in roots)
        {
            string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) continue;
            foreach (Match match in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s*\"((?:\\\\.|[^\"\\\\])*)\""))
                libraries.Add(match.Groups[1].Value.Replace(@"\\", @"\"));
        }
        foreach (string library in libraries)
        {
            string manifest = Path.Combine(library, "steamapps", "appmanifest_304930.acf");
            string folder = "Unturned";
            if (File.Exists(manifest))
            {
                var match = Regex.Match(File.ReadAllText(manifest), "\"installdir\"\\s*\"([^\"]+)\"");
                if (match.Success) folder = match.Groups[1].Value;
            }
            string game = Path.Combine(library, "steamapps", "common", folder);
            if (IsGame(game)) return Path.GetFullPath(game);
        }
        throw new DirectoryNotFoundException("Install Unturned (Steam app 304930) before playing/building this SDK, or set UNTURNED_ASSET_DIRECTORY to its installation folder.");
    }

    private static bool IsGame(string path) => Directory.Exists(Path.Combine(path, "Maps")) && Directory.Exists(Path.Combine(path, "Bundles"));

    [InitializeOnLoadMethod]
    private static void SchedulePreparation()
    {
        if (!Application.isBatchMode) EditorApplication.delayCall += PrepareAfterImport;
    }

    private static void PrepareAfterImport()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += PrepareAfterImport;
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { ConvertLegacyMapBundles.ConvertInstalledMaps(); }
        catch (DirectoryNotFoundException error) { Debug.Log("SDK preparation: " + error.Message); }
        catch (Exception error) { Debug.LogException(error); }
    }

    [OnExitingEditMode]
    private static void PrepareForPlay()
    {
        // This attribute also runs before code reload. Only act on a Play request.
        if (!EditorApplication.isPlayingOrWillChangePlaymode) return;
        try
        {
            if (ConvertLegacyMapBundles.InstalledCacheReady()) return;
            // Cancel the transition before importing/building missing content. Once
            // ready, resume the original Play request without another user action.
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += () =>
            {
                try
                {
                    ConvertLegacyMapBundles.ConvertInstalledMaps();
                    EditorApplication.EnterPlaymode();
                }
                catch (Exception error) { Debug.LogError("Cannot prepare Play mode: " + error.Message); }
            };
        }
        catch (Exception error)
        {
            EditorApplication.isPlaying = false;
            Debug.LogError("Cannot start Play mode: " + error.Message);
        }
    }
}

public sealed class PortBuildPreparation : BuildPlayerProcessor
{
    public override int callbackOrder => -1000;
    public override void PrepareForBuild(BuildPlayerContext context)
    {
        if (context.BuildPlayerOptions.target != BuildTarget.StandaloneWindows64)
            throw new BuildFailedException("This Unity port is qualified for Windows 64-bit with DX12/Vulkan. Select the Unturned Windows DX12 Vulkan profile.");
        try { ConvertLegacyMapBundles.ConvertInstalledMaps(); }
        catch (Exception error) { throw new BuildFailedException("SDK preparation failed: " + error.Message); }
    }
}
