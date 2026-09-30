using System;
using UnityEngine;
using SDG.Unturned;

public static class VerifyURPMaterials
{
    public static object Main()
    {
        int assertions = 0;
        void Check(bool condition, string message) { assertions++; if (!condition) throw new InvalidOperationException(message); }
        var texture = new Texture2D(2, 2);
        var legacy = new Material(Shader.Find("Standard (Specular setup)"));
        var native = StandardShaderUtils.CreateStandardMaterial(true);
        try
        {
            var color = new Color(0.25f, 0.5f, 0.75f, 0.4f);
            legacy.mainTexture = texture;
            legacy.mainTextureScale = new Vector2(2f, 3f);
            legacy.mainTextureOffset = new Vector2(0.1f, 0.2f);
            legacy.color = color;
            legacy.SetFloat("_Glossiness", 0.7f);
            legacy.SetTexture("_BumpMap", texture);
            legacy.SetTexture("_SpecGlossMap", texture);
            legacy.SetTexture("_EmissionMap", texture);
            legacy.SetColor("_EmissionColor", Color.red * 2f);
            StandardShaderUtils.setModeToTransparent(legacy);
            Check(UniversalMaterialAdapter.Upgrade(legacy), "Legacy material was not converted");
            Check(legacy.shader.name == "Universal Render Pipeline/Lit", "Material did not select native Lit");
            Check(legacy.mainTexture == texture && legacy.mainTextureScale == new Vector2(2f, 3f)
                && legacy.mainTextureOffset == new Vector2(0.1f, 0.2f), "Albedo texture transform changed");
            Check(legacy.color == color && Mathf.Approximately(legacy.GetFloat("_Smoothness"), 0.7f), "Color or smoothness changed");
            Check(legacy.GetFloat("_WorkflowMode") == 0f && legacy.IsKeywordEnabled("_SPECULAR_SETUP"), "Specular workflow was lost");
            Check(legacy.IsKeywordEnabled("_NORMALMAP") && legacy.GetTexture("_BumpMap") == texture, "Normal map was lost");
            Check(legacy.IsKeywordEnabled("_METALLICSPECGLOSSMAP") && legacy.GetTexture("_SpecGlossMap") == texture, "Specular map was lost");
            Check(legacy.IsKeywordEnabled("_EMISSION") && legacy.GetTexture("_EmissionMap") == texture, "Emission map was lost");
            Check(legacy.GetFloat("_Surface") == 1f && legacy.GetFloat("_Blend") == 1f
                && legacy.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON"), "Premultiplied transparency changed");
            Check(!UniversalMaterialAdapter.Upgrade(legacy), "Native material conversion should be idempotent");
            StandardShaderUtils.setModeToFade(native);
            Check(StandardShaderUtils.isModeFade(native) && native.GetFloat("_ZWrite") == 0f, "Native fade mode failed");
            StandardShaderUtils.setModeToCutout(native);
            Check(native.GetFloat("_AlphaClip") == 1f && native.IsKeywordEnabled("_ALPHATEST_ON")
                && native.GetShaderPassEnabled("ShadowCaster"), "Native cutout mode failed");
            StandardShaderUtils.setModeToOpaque(native);
            Check(native.GetFloat("_Surface") == 0f && native.GetFloat("_AlphaClip") == 0f
                && !native.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), "Native opaque mode failed");
            return new { passed = true, assertions };
        }
        finally { UnityEngine.Object.DestroyImmediate(legacy); UnityEngine.Object.DestroyImmediate(native); UnityEngine.Object.DestroyImmediate(texture); }
    }
}
