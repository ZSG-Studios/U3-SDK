#ifndef UNTURNED_UNLIT_INCLUDED
#define UNTURNED_UNLIT_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

float UnturnedLinearEyeDepth(float depth) { return LinearEyeDepth(depth, _ZBufferParams); }
float4 UnturnedObjectToClip(float4 positionOS) { return TransformObjectToHClip(positionOS.xyz); }
float _UnturnedOrtho;
struct UnturnedUnlitAttributes
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float4 texcoord : TEXCOORD0;
};
float4 UnturnedGeometryToClip(float4 positionWS)
{
    return lerp(TransformWorldToHClip(positionWS.xyz), float4(positionWS.xy * 2 - 1, 0, 1), _UnturnedOrtho);
}
#endif
