////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SDG.Unturned
{
    /// <summary>Single-render scope state, consumed by the URP RenderGraph feature.</summary>
    [System.Serializable, VolumeComponentMenu("Unturned/Single render scope")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class SrScope : VolumeComponent, IPostProcessComponent
    {
        public FloatParameter standardDeviation = new FloatParameter(-1f);
        public ClampedFloatParameter scopeAlpha = new ClampedFloatParameter(0f, 0f, 1f);
        public TextureParameter renderTarget = new TextureParameter(null);
        public bool IsActive() => active && renderTarget.value is RenderTexture;
    }
}
