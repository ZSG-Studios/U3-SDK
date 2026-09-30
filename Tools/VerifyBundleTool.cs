using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using SDG.Unturned.Tools;

public static class VerifyBundleTool
{
    public static object Main()
    {
        string folderName = "__BundleToolVerification_" + Guid.NewGuid().ToString("N");
        string folder = "Assets/" + folderName;
        string output = Path.GetFullPath("Logs/BundleToolSmoke.UNITY3D");
        UnityEngine.Object[] previousSelection = Selection.objects;
        BundleTool window = null;
        AssetBundle bundle = null;
        GameObject cube = null;
        try
        {
            AssetDatabase.CreateFolder("Assets", folderName);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("Missing fixture shader");
            var material = new Material(shader) { color = Color.green };
            AssetDatabase.CreateAsset(material, folder + "/Material.mat");
            cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.GetComponent<MeshRenderer>().sharedMaterial = material;
            PrefabUtility.SaveAsPrefabAsset(cube, folder + "/Cube.prefab");
            UnityEngine.Object.DestroyImmediate(cube);
            cube = null;
            AssetDatabase.SaveAssets();

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder);
            window = ScriptableObject.CreateInstance<BundleTool>();
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            typeof(BundleTool).GetMethod("grabAssets", flags).Invoke(window, null);
            typeof(BundleTool).GetField("path", flags).SetValue(null, output);
            typeof(BundleTool).GetMethod("bundleAssets", flags).Invoke(window, null);
            bundle = AssetBundle.LoadFromFile(output);
            if (bundle == null) throw new InvalidOperationException("Bundle could not be loaded");
            var prefab = bundle.LoadAsset<GameObject>(folder.ToLowerInvariant() + "/cube.prefab");
            if (prefab == null || prefab.GetComponent<MeshFilter>().sharedMesh == null
                || prefab.GetComponent<MeshRenderer>().sharedMaterial == null)
                throw new InvalidOperationException("Prefab dependencies were not included");
            return new { passed = true, output, bytes = new FileInfo(output).Length, assets = bundle.GetAllAssetNames() };
        }
        finally
        {
            if (bundle != null) bundle.Unload(true);
            if (cube != null) UnityEngine.Object.DestroyImmediate(cube);
            if (window != null) UnityEngine.Object.DestroyImmediate(window);
            Selection.objects = previousSelection;
            AssetDatabase.DeleteAsset(folder);
        }
    }
}
