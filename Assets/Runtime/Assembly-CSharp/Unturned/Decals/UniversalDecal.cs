#if GAME
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SDG.Unturned
{
    /// <summary>Adapts the SDK's serialized decal volume to a native URP projector.</summary>
    internal sealed class UniversalDecal : MonoBehaviour
    {
        private Decal source;
        private DecalProjector projector;
        private Material sourceMaterial;
        private Material ownedMaterial;

        internal void Initialize(Decal decal)
        {
            source = decal;
            projector = gameObject.GetOrAddComponent<DecalProjector>();
            projector.scaleMode = DecalScaleMode.InheritFromHierarchy;
            projector.size = Vector3.one;
            projector.pivot = Vector3.zero;
            Synchronize();
        }

        internal void Synchronize()
        {
            if (source == null || projector == null) return;
            if (sourceMaterial != source.material)
            {
                if (ownedMaterial != null) Destroy(ownedMaterial);
                sourceMaterial = source.material;
                if (sourceMaterial != null)
                {
                    string name = sourceMaterial.shader.name;
                    string style = name.Contains("Emissive") ? "Emissive"
                        : name.Contains("Alpha") || name.Contains("Blast") ? "Blend" : "Cutout";
                    ownedMaterial = new Material(global::SDG.Unturned.UniversalShaderCatalog.Find("Unturned/ProjectedDecal" + style));
                    ownedMaterial.SetTexture("_MainTex", sourceMaterial.GetTexture("_MainTex"));
                    if (sourceMaterial.HasProperty("_Cutoff")) ownedMaterial.SetFloat("_Cutoff", sourceMaterial.GetFloat("_Cutoff"));
                    if (sourceMaterial.HasProperty("_EmissionMap")) ownedMaterial.SetTexture("_EmissionMap", sourceMaterial.GetTexture("_EmissionMap"));
                    ownedMaterial.enableInstancing = true;
                    projector.material = ownedMaterial;
                    projector.uvScale = sourceMaterial.GetTextureScale("_MainTex");
                    projector.uvBias = sourceMaterial.GetTextureOffset("_MainTex");
                }
            }
            projector.drawDistance = (GraphicsSettings.WantsCinematicMode && MainCamera.instance != null
                ? MainCamera.instance.farClipPlane : 128 + GraphicsSettings.normalizedDrawDistance * 128) * source.lodBias;
            projector.enabled = source.isActiveAndEnabled && sourceMaterial != null;
        }
        private void LateUpdate() => Synchronize();
        private void OnDisable() { if (projector != null) projector.enabled = false; }
        private void OnDestroy() { if (projector != null) Destroy(projector); if (ownedMaterial != null) Destroy(ownedMaterial); }
    }
}
#endif
