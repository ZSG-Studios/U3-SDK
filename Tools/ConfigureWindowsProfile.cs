using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Create the native Windows build profile and select DXC only for DirectX 12.</summary>
public static class ConfigureWindowsProfile
{
    public static object Main()
    {
        const string profileName = "Unturned Windows DX12 Vulkan";
        var profile = BuildProfile.GetAllBuildProfiles().FirstOrDefault(p => p.name == profileName);
        if (profile == null)
        {
            var platforms = BuildProfile.GetInstalledPlatformModules();
            var windows = platforms.FirstOrDefault(p => p.displayName == "Windows");
            if (windows.platformGuid.Equals(default(UnityEngine.GUID)))
                throw new InvalidOperationException("Windows platform missing; installed: " + string.Join(", ", platforms.Select(p => p.displayName)));
            profile = BuildProfile.CreateBuildProfile(windows.platformGuid, profileName);
        }
        string path = AssetDatabase.GetAssetPath(profile);
        var profileData = new SerializedObject(profile);
        profileData.FindProperty("m_Subtarget").intValue = (int)StandaloneBuildSubtarget.Player;
        profileData.ApplyModifiedPropertiesWithoutUndo();
        EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Player;
        const string graphicsTypeName = "UnityEditor.Build.Profile.BuildProfileGraphicsSettings";
        var graphics = AssetDatabase.LoadAllAssetsAtPath(path).OfType<ScriptableObject>()
            .FirstOrDefault(o => o.GetType().FullName == graphicsTypeName);
        if (graphics == null)
        {
            // In 6000.7.0b2 the native graphics component and its copy-from-global initializer
            // are internal. Limit reflection to setup; gameplay contains no profile reflection.
            var type = typeof(BuildProfile).Assembly.GetType(graphicsTypeName, true);
            graphics = ScriptableObject.CreateInstance(type);
            type.GetMethod("Instantiate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Invoke(graphics, null);
            typeof(BuildProfile).GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Single(m => m.Name == "AddComponent" && m.IsGenericMethodDefinition)
                .MakeGenericMethod(type).Invoke(profile, new object[] { graphics });
        }
        var serialized = new SerializedObject(graphics);
        // Preserve dynamic Steam-loaded shader coverage and the project's stripping settings.
        var global = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
        var property = global.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (property.name == "m_ObjectHideFlags" || property.name == "m_Script") continue;
            var destination = serialized.FindProperty(property.propertyPath);
            if (destination == null) continue;
            if (property.isArray && property.propertyType != SerializedPropertyType.String)
            {
                // Native and managed serialized structs can have different underlying types.
                // The shared top-level arrays contain Shader/ShaderVariantCollection references.
                destination.arraySize = property.arraySize;
                for (int i = 0; i < property.arraySize; i++)
                    destination.GetArrayElementAtIndex(i).objectReferenceValue = property.GetArrayElementAtIndex(i).objectReferenceValue;
            }
            else switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Enum: destination.intValue = property.intValue; break;
                case SerializedPropertyType.Boolean: destination.boolValue = property.boolValue; break;
                case SerializedPropertyType.Float: destination.floatValue = property.floatValue; break;
                case SerializedPropertyType.String: destination.stringValue = property.stringValue; break;
                case SerializedPropertyType.ObjectReference: destination.objectReferenceValue = property.objectReferenceValue; break;
            }
        }
        var compilers = serialized.FindProperty("m_ShaderBuildSettings.compilerSettings");
        if (compilers == null) throw new InvalidOperationException("The installed editor lacks native shader compiler settings.");
        compilers.arraySize = 1;
        var row = compilers.GetArrayElementAtIndex(0);
        row.FindPropertyRelative("graphicsAPI").intValue = (int)GraphicsDeviceType.Direct3D12;
        // ShaderBuildSettings.ShaderCompilerToolchain.DXC is internal in this beta; value verified in the installed assembly.
        row.FindPropertyRelative("compilerToolchainOverride").intValue = 2;
        row.FindPropertyRelative("optimizationLevel").intValue = 0;
        row.FindPropertyRelative("enableDebugSymbols").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(graphics);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        BuildProfile.SetActiveBuildProfile(profile);
        return new { profile = path, graphicsApis = new[] { "Direct3D12", "Vulkan" },
            compiler = "Direct3D12: DXC; Vulkan: native default" };
    }
}
