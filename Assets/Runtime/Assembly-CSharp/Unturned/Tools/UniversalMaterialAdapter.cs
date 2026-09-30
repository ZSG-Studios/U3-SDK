using UnityEngine;
using UnityEngine.Rendering;

namespace SDG.Unturned
{
    /// <summary>Import prebuilt Unity materials from Steam bundles into the active URP shader family.</summary>
    public static class UniversalMaterialAdapter
    {
        public static void UpgradeObject(UnityEngine.Object asset)
        {
            if (Dedicator.IsDedicatedServer) return;
            if (asset is Material material) Upgrade(material);
            else if (asset is GameObject prefab)
                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                    foreach (var shared in renderer.sharedMaterials) Upgrade(shared);
        }

        public static bool Upgrade(Material material)
        {
            if (material == null || UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null)
                return false;
            Shader original = material.shader;
            string name = original == null ? "Standard" : original.name;
            bool alias = name == "Standard (Decalable)" || name == "Standard (Specular setup, Decalable)"
                || name == "Standard (Specular setup) (Decalable)" || name == "Unturned/CloudParticles";
            if (original != null && material.GetTag("RenderPipeline", false) == "UniversalPipeline" && !alias)
                return false;
            bool particle = name.Contains("Particles/");
            particle |= name == "Unturned/CloudParticles";
            bool unlit = name.StartsWith("Unlit/") || name == "Sprites/Default";
            bool standard = name == "Standard" || name == "Standard (Specular setup)"
                || name.StartsWith("Legacy Shaders/") || particle || unlit || alias;
            if (!standard)
            {
                // Modern Steam bundles disable legacy shader consolidation by default.
                // Their authored equations still need the explicitly-referenced URP source shader.
                var authored = UniversalShaderCatalog.Find(ShaderConsolidator.redirectShaderName(name));
                if (authored == null || authored == original) return false;
                material.shader = authored;
                return true;
            }

            Texture albedo = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
            Vector2 scale = material.HasProperty("_MainTex") ? material.GetTextureScale("_MainTex") : Vector2.one;
            Vector2 offset = material.HasProperty("_MainTex") ? material.GetTextureOffset("_MainTex") : Vector2.zero;
            Color color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
            if (material.HasProperty("_TintColor")) color = material.GetColor("_TintColor");
            float smoothness = material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : 0f;
            float mode = material.HasProperty("_Mode") ? material.GetFloat("_Mode") : 0f;
            bool cutout = mode == 1 || material.IsKeywordEnabled("_ALPHATEST_ON") || material.GetTag("RenderType", false) == "TransparentCutout";
            bool transparent = !cutout && (mode >= 2 || material.IsKeywordEnabled("_ALPHABLEND_ON")
                || material.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON") || material.renderQueue >= 3000 || particle);
            bool premultiplied = material.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON") || mode == 3;
            bool specular = name.Contains("Specular setup");
            bool additive = name.Contains("Additive");

            Shader replacement = UniversalShaderCatalog.Find(particle ? (name == "Unturned/CloudParticles" ? "Universal Render Pipeline/Particles/Lit" : "Universal Render Pipeline/Particles/Unlit")
                : unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (replacement == null)
            {
                UnturnedLog.error($"Required URP shader is missing while importing material {material.name}");
                return false;
            }

            material.shader = replacement;
            material.shaderKeywords = System.Array.Empty<string>();
            material.SetTexture("_BaseMap", albedo);
            material.SetTextureScale("_BaseMap", scale);
            material.SetTextureOffset("_BaseMap", offset);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", additive ? 2 : premultiplied ? 1 : 0);
            material.SetFloat("_Surface", transparent ? 1 : 0);
            material.SetFloat("_AlphaClip", cutout ? 1 : 0);
            material.SetFloat("_ZWrite", transparent ? 0 : 1);
            material.SetFloat("_SrcBlend", transparent && !premultiplied ? (int)BlendMode.SrcAlpha : (int)BlendMode.One);
            material.SetFloat("_DstBlend", additive ? (int)BlendMode.One
                : transparent ? (int)BlendMode.OneMinusSrcAlpha : (int)BlendMode.Zero);
            material.SetFloat("_SrcBlendAlpha", (int)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", transparent ? (int)BlendMode.OneMinusSrcAlpha : (int)BlendMode.Zero);
            material.SetOverrideTag("RenderType", transparent ? "Transparent" : cutout ? "TransparentCutout" : "Opaque");
            material.renderQueue = transparent ? (int)RenderQueue.Transparent
                : cutout ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", transparent);
            SetKeyword(material, "_ALPHAPREMULTIPLY_ON", premultiplied);
            SetKeyword(material, "_ALPHATEST_ON", cutout);
            if (material.HasProperty("_WorkflowMode")) material.SetFloat("_WorkflowMode", specular ? 0 : 1);
            SetKeyword(material, "_SPECULAR_SETUP", specular);
            SetKeyword(material, "_NORMALMAP", material.HasProperty("_BumpMap") && material.GetTexture("_BumpMap") != null);
            SetKeyword(material, "_METALLICSPECGLOSSMAP", material.HasProperty(specular ? "_SpecGlossMap" : "_MetallicGlossMap")
                && material.GetTexture(specular ? "_SpecGlossMap" : "_MetallicGlossMap") != null);
            SetKeyword(material, "_EMISSION", material.HasProperty("_EmissionColor")
                && material.GetColor("_EmissionColor").maxColorComponent > 0f);
            material.SetShaderPassEnabled("ShadowCaster", !transparent);
            return true;
        }

        private static void SetKeyword(Material material, string name, bool enabled)
        {
            if (enabled) material.EnableKeyword(name);
            else material.DisableKeyword(name);
        }
    }
}
