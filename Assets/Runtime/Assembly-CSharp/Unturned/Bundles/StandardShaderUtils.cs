////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using UnityEngine;

namespace SDG.Unturned
{
	/// <summary>
	/// Standard shader mode changes are based on built-in StandardShaderGUI.cs
	/// </summary>
	public static class StandardShaderUtils
	{
		public static Shader StandardShader => global::SDG.Unturned.UniversalShaderCatalog.Find("Universal Render Pipeline/Lit");
		public static Material CreateStandardMaterial(bool specular = false)
		{
			var material = new Material(StandardShader);
			material.SetFloat("_WorkflowMode", specular ? 0f : 1f);
			if (specular) material.EnableKeyword("_SPECULAR_SETUP");
			return material;
		}
		public static int ColorProperty(Material material) => Shader.PropertyToID(material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color");
		public static int SmoothnessProperty(Material material) => Shader.PropertyToID(material.HasProperty("_Smoothness") ? "_Smoothness" : "_Glossiness");
		public static bool IsUniversalLit(Material material) => material != null && material.shader != null && material.shader.name == "Universal Render Pipeline/Lit";
		private static bool SetUniversalMode(Material material, int mode)
		{
			if (!material.HasProperty("_Surface")) return false;
			bool transparent = mode >= 2;
			material.SetFloat("_Surface", transparent ? 1f : 0f);
			material.SetFloat("_Blend", mode == 3 ? 1f : 0f);
			material.SetFloat("_AlphaClip", mode == 1 ? 1f : 0f);
			material.SetFloat("_ZWrite", transparent ? 0f : 1f);
			material.SetFloat("_SrcBlend", mode == 2 ? (int)UnityEngine.Rendering.BlendMode.SrcAlpha : (int)UnityEngine.Rendering.BlendMode.One);
			material.SetFloat("_DstBlend", transparent ? (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha : (int)UnityEngine.Rendering.BlendMode.Zero);
			material.SetFloat("_SrcBlendAlpha", 1f);
			material.SetFloat("_DstBlendAlpha", transparent ? (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha : 0f);
			SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", transparent);
			SetKeyword(material, "_ALPHATEST_ON", mode == 1);
			SetKeyword(material, "_ALPHAPREMULTIPLY_ON", mode == 3);
			material.DisableKeyword("_ALPHABLEND_ON");
			material.SetOverrideTag("RenderType", transparent ? "Transparent" : mode == 1 ? "TransparentCutout" : "Opaque");
			material.renderQueue = (int)(transparent ? UnityEngine.Rendering.RenderQueue.Transparent : mode == 1 ? UnityEngine.Rendering.RenderQueue.AlphaTest : UnityEngine.Rendering.RenderQueue.Geometry);
			material.SetShaderPassEnabled("ShadowCaster", !transparent);
			material.SetShaderPassEnabled("DepthOnly", !transparent);
			return true;
		}
		private static void SetKeyword(Material material, string keyword, bool enabled)
		{
			if (enabled) material.EnableKeyword(keyword); else material.DisableKeyword(keyword);
		}
		/// <summary>
		/// Does shader name match any of the standard shaders?
		/// Standard, StandardSpecular and the Unturned "Decalable" variants all share nearly identical parameters.
		/// </summary>
		public static bool isNameStandard(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return false;
			}
			else
			{
				return name == "Universal Render Pipeline/Lit" || name.StartsWith("Standard", System.StringComparison.Ordinal) && (name.Length == 8 || name.EndsWith(" (Decalable)", System.StringComparison.Ordinal) || name.EndsWith(" (Specular setup)", System.StringComparison.Ordinal));
			}
		}

		public static bool isMaterialUsingStandardShader(Material material)
		{
			return material != null
				&& material.shader != null
				&& isNameStandard(material.shader.name);
		}

		public static bool isModeFade(Material material)
		{
			return material.HasProperty("_Surface") ? material.GetFloat("_Surface") == 1f && material.GetFloat("_Blend") == 0f : material.IsKeywordEnabled("_ALPHABLEND_ON");
		}

		public static bool isModeTransparent(Material material)
		{
			return material.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON");
		}

		public static void setModeToOpaque(Material material)
		{
			if (SetUniversalMode(material, 0)) return;
			material.SetFloat("_Mode", 0f);

			material.SetOverrideTag("RenderType", "");
			material.SetInt("_SrcBlend", (int) UnityEngine.Rendering.BlendMode.One);
			material.SetInt("_DstBlend", (int) UnityEngine.Rendering.BlendMode.Zero);
			material.SetInt("_ZWrite", 1);
			material.DisableKeyword("_ALPHATEST_ON");
			material.DisableKeyword("_ALPHABLEND_ON");
			material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
			material.renderQueue = -1;
		}

		public static void setModeToCutout(Material material)
		{
			if (SetUniversalMode(material, 1)) return;
			material.SetFloat("_Mode", 1f);

			material.SetOverrideTag("RenderType", "TransparentCutout");
			material.SetInt("_SrcBlend", (int) UnityEngine.Rendering.BlendMode.One);
			material.SetInt("_DstBlend", (int) UnityEngine.Rendering.BlendMode.Zero);
			material.SetInt("_ZWrite", 1);
			material.EnableKeyword("_ALPHATEST_ON");
			material.DisableKeyword("_ALPHABLEND_ON");
			material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
			material.renderQueue = (int) UnityEngine.Rendering.RenderQueue.AlphaTest;
		}

		public static void setModeToFade(Material material)
		{
			if (SetUniversalMode(material, 2)) return;
			material.SetFloat("_Mode", 2f);

			material.SetOverrideTag("RenderType", "Transparent");
			material.SetInt("_SrcBlend", (int) UnityEngine.Rendering.BlendMode.SrcAlpha);
			material.SetInt("_DstBlend", (int) UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
			material.SetInt("_ZWrite", 0);
			material.DisableKeyword("_ALPHATEST_ON");
			material.EnableKeyword("_ALPHABLEND_ON");
			material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
			material.renderQueue = (int) UnityEngine.Rendering.RenderQueue.Transparent;
		}

		public static void setModeToTransparent(Material material)
		{
			if (SetUniversalMode(material, 3)) return;
			material.SetFloat("_Mode", 3f);

			material.SetOverrideTag("RenderType", "Transparent");
			material.SetInt("_SrcBlend", (int) UnityEngine.Rendering.BlendMode.One);
			material.SetInt("_DstBlend", (int) UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
			material.SetInt("_ZWrite", 0);
			material.DisableKeyword("_ALPHATEST_ON");
			material.DisableKeyword("_ALPHABLEND_ON");
			material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
			material.renderQueue = (int) UnityEngine.Rendering.RenderQueue.Transparent;
		}

		/// <summary>
		/// Based on fixup routine in StandardShaderGUI SetMaterialKeywords.
		/// </summary>
		public static void fixupEmission(Material material)
		{
			Color emissionColor = material.GetColor("_EmissionColor");
			if (emissionColor.maxColorComponent < 0.01f)
			{
				material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
			}
			else
			{
				material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeIndirectEmission;
			}

			bool shouldEmissionBeEnabled = (material.globalIlluminationFlags & MaterialGlobalIlluminationFlags.EmissiveIsBlack) == 0;
			if (shouldEmissionBeEnabled)
			{
				// Note: this step is not in the StandardShaderGUI, but aims to avoid incorrectly enabling emission.
				// Perhaps emission color on an older material was set incorrectly, as most materials with a color also have a texture.
				// If the texture is null we assume the color was an accident and disable emission.
				Texture emissionMap = material.GetTexture("_EmissionMap");
				shouldEmissionBeEnabled = emissionMap != null;
			}

			if (shouldEmissionBeEnabled)
			{
				material.EnableKeyword("_EMISSION");
			}
			else
			{
				material.DisableKeyword("_EMISSION");
			}
		}

		/// <summary>
		/// Conditionally fixup older standard materials.
		/// </summary>
		/// <returns>True if material was edited.</returns>
		public static bool maybeFixupMaterial(Material material)
		{
			if (isMaterialUsingStandardShader(material) == false)
				return false;

			bool shouldFixupEmission;
			if (isModeFade(material))
			{
				setModeToFade(material);
				shouldFixupEmission = true;
			}
			else if (isModeTransparent(material))
			{
				setModeToTransparent(material);
				shouldFixupEmission = true;
			}
			else
			{
				shouldFixupEmission = false;
			}

			// We ran into some unexpected cases, so only fixup if already making changes.
			// https://github.com/SmartlyDressedGames/Unturned-3.x-Community/issues/1193
			if (shouldFixupEmission)
			{
				fixupEmission(material);
			}

			return true;
		}
	}
}
