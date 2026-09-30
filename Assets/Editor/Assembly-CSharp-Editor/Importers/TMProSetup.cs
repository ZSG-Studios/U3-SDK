////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Unturned.SystemEx;
using Unturned.UnityEx;

/// <summary>
/// Auto-executes on editor startup to import latest version of TMPro essential resources
/// if not already present.
/// </summary>
[InitializeOnLoad]
public static class TMProSetup
{
	static TMProSetup()
	{
		string expectedPath = PathEx.Join(UnityPaths.AssetsDirectory, "TextMesh Pro");
		if (Directory.Exists(expectedPath))
		{
			UpgradeShaderPragmas(expectedPath);
			// Already imported.
			return;

		}

		// In Unity 6, TextMesh Pro is part of uGUI. Resolve the package that owns
		// the text assembly rather than assuming the former package name.
		var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMPro.TMP_Text).Assembly);
		if (package == null)
		{
			Debug.LogError("Unable to locate the package containing TextMesh Pro.");
			return;
		}
		string importPath = Path.Join(package.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
		if (!File.Exists(importPath))
		{
			Debug.LogError($"Expected to find TextMesh Pro essential resources package at: {importPath}");
			return;
		}

		UnityEditor.AssetPackage.Package.Import(importPath, /*interactive*/ false);
		UpgradeShaderPragmas(expectedPath);
		Debug.Log("Imported TextMesh Pro essential resources!");
	}

	private static void UpgradeShaderPragmas(string resourcesPath)
	{
		if (!Directory.Exists(resourcesPath)) return;
		// The 6.7 uGUI essential-resources archive still contains the retired directive.
		// These resources are generated and ignored by Git, so repair existing and fresh imports.
		foreach (string path in Directory.EnumerateFiles(resourcesPath, "*.shader", SearchOption.AllDirectories))
		{
			string source = File.ReadAllText(path);
			string updated = source.Replace("#pragma enable_d3d11_debug_symbols", "#pragma enable_debug_symbols");
			// DXC requires SV_Target for fragment outputs. COLOR remains valid on vertex data.
			updated = Regex.Replace(updated, @"(\bfrag\s*\([^)]*\)\s*:\s*)COLOR\b", "$1SV_Target");
			if (source != updated) File.WriteAllText(path, updated);
		}
	}
}
