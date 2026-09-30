using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SDG.Unturned;

public static class MigrateURPAssets
{
    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before migrating assets.");
        int materials = 0, components = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) continue;
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (UniversalMaterialAdapter.Upgrade(material)) { EditorUtility.SetDirty(material); materials++; }
        }
        foreach (string path in new[] { "Assets/Resources/Characters/Player_Server.prefab", "Assets/Resources/Edit/Editor.prefab" })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                components += RemoveLegacyEffects(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        var menu = EditorSceneManager.OpenScene("Assets/Game/Sources/Scenes/Menu.unity", OpenSceneMode.Additive);
        try
        {
            foreach (var root in menu.GetRootGameObjects()) components += RemoveLegacyEffects(root);
            EditorSceneManager.SaveScene(menu);
        }
        finally { EditorSceneManager.CloseScene(menu, true); }
        // Shader.Find is used for dynamically imported Steam assets, so these shaders must survive builds.
        var shaderNames = new HashSet<string>(new[] {
            "Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Particles/Lit", "Universal Render Pipeline/Particles/Unlit",
            "Unturned/ProjectedDecalCutout", "Unturned/ProjectedDecalBlend", "Unturned/ProjectedDecalEmissive"
        });
        var shaders = AssetDatabase.FindAssets("t:Shader", new[] { "Assets/Game/Sources/Shaders", "Assets/Runtime/Assembly-CSharp/CustomPostProcess" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(shader => shader != null).Concat(shaderNames.Select(Shader.Find)).Where(shader => shader != null).Distinct().ToArray();
        var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        var included = graphics.FindProperty("m_AlwaysIncludedShaders");
        for (int i = 0; i < included.arraySize; i++)
        {
            var shader = included.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
            if (shader != null) shaders = shaders.Append(shader).Distinct().ToArray();
        }
        included.arraySize = shaders.Length;
        for (int i = 0; i < shaders.Length; i++) included.GetArrayElementAtIndex(i).objectReferenceValue = shaders[i];
        graphics.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        return new { materials, migratedPostProcessingComponents = components, retainedShaders = shaders.Length };
    }
    static int RemoveLegacyEffects(GameObject root)
    {
        int removed = 0;
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null) continue;
            string name = component.GetType().FullName;
            if (name == "UnityEngine.Rendering.PostProcessing.PostProcessLayer"
                || name == "UnityEngine.Rendering.PostProcessing.PostProcessVolume")
            {
                UnityEngine.Object.DestroyImmediate(component);
                removed++;
            }
        }
        return removed;
    }
}
