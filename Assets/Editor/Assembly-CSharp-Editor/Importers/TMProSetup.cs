////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using UnityEditor;
using UnityEditor.Build;
using TMPro;

/// <summary>Validate required, version-controlled UI resources before Play or build.</summary>
public static class TMProSetup
{
    public static void EnsureReady()
    {
        const string path = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(path);
        if (settings == null || TMP_Settings.instance != settings)
            throw new BuildFailedException("Missing or conflicting TextMesh Pro settings. Restore the tracked Assets/TextMesh Pro resources from this repository.");
        var font = TMP_Settings.defaultFontAsset;
        if (font == null || font.material == null || font.material.shader == null ||
            font.atlasTextures == null || font.atlasTextures.Length == 0 || font.atlasTextures[0] == null ||
            TMP_Settings.defaultStyleSheet == null)
            throw new BuildFailedException("Incomplete TextMesh Pro font, atlas, material, or stylesheet. Restore the tracked Assets/TextMesh Pro resources before Play/build.");
    }
}
