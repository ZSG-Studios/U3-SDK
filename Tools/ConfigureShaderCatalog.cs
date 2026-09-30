using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using SDG.Unturned;

public static class ConfigureShaderCatalog
{
    public static object Main()
    {
        const string path = "Assets/Resources/UnturnedUniversalShaders.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<UniversalShaderCatalog>(path);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<UniversalShaderCatalog>();
            AssetDatabase.CreateAsset(catalog, path);
        }
        var shaders = new List<Shader>();
        foreach (string guid in AssetDatabase.FindAssets("t:Shader", new[] { "Assets/Game/Sources/Shaders", "Assets/Runtime/Assembly-CSharp/CustomPostProcess" }))
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
            if (shader != null) shaders.Add(shader);
        }
        foreach (string name in new[] { "Lit", "Unlit", "Particles/ParticlesLit", "Particles/ParticlesUnlit", "Terrain/TerrainLit" })
        {
            string shaderPath = "Packages/com.unity.render-pipelines.universal/Shaders/" + name + ".shader";
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            if (shader == null) throw new InvalidOperationException("Missing native shader " + shaderPath);
            shaders.Add(shader);
        }
        catalog.shaders = shaders.Distinct().OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        return new { catalog = path, shaders = catalog.shaders.Length };
    }
}
