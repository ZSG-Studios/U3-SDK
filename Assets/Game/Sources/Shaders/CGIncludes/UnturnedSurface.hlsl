// Shared URP lighting for Unturned's authored material equations.
#ifndef UNTURNED_SURFACE_INCLUDED
#define UNTURNED_SURFACE_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/PackNormalsTexture.hlsl"

struct UnturnedSurface
{
    half3 Albedo, Normal, Emission, Specular;
    half Metallic, Smoothness, Occlusion, Alpha;
};

struct UnturnedAttributes
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
    float4 texcoord : TEXCOORD0;
    float4 texcoord1 : TEXCOORD1;
    float4 texcoord2 : TEXCOORD2;
    half4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct UnturnedVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
    half4 tangentWS : TEXCOORD2;
    float2 uv : TEXCOORD3;
    float3 localPos : TEXCOORD4;
    half4 color : COLOR;
    float4 screenPos : TEXCOORD5;
    DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 6);
    half fogFactor : TEXCOORD7;
    float2 custom : TEXCOORD8;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

UnturnedSurface UnturnedDefaultSurface()
{
    UnturnedSurface s = (UnturnedSurface)0;
    s.Normal = half3(0, 0, 1);
    s.Occlusion = 1;
    s.Alpha = 1;
    return s;
}

half3 UnturnedWorldNormal(UnturnedVaryings i, half3 normalTS)
{
    half3 bitangent = cross(i.normalWS, i.tangentWS.xyz) * i.tangentWS.w;
    return NormalizeNormalPerPixel(TransformTangentToWorld(normalTS,
        half3x3(i.tangentWS.xyz, bitangent, i.normalWS)));
}

half4 UnturnedDepthNormals(UnturnedVaryings i, UnturnedSurface s)
{
    half smoothness = 0;
#if defined(_WRITE_SMOOTHNESS)
    smoothness = s.Smoothness;
#endif
    return half4(PackNormalWSToTexture(UnturnedWorldNormal(i, s.Normal)), smoothness);
}

half4 UnturnedLighting(UnturnedVaryings i, UnturnedSurface s)
{
    SurfaceData surface = (SurfaceData)0;
    surface.albedo = s.Albedo;
    surface.normalTS = s.Normal;
    surface.metallic = s.Metallic;
    surface.specular = s.Specular;
    surface.smoothness = s.Smoothness;
    surface.occlusion = s.Occlusion;
    surface.emission = s.Emission;
    surface.alpha = s.Alpha;
    InputData input = (InputData)0;
    input.positionWS = i.positionWS;
    input.preExposureMultiplier = GetPreExposureMultiplier();
    input.positionCS = i.positionCS;
    input.normalWS = UnturnedWorldNormal(i, s.Normal);
    input.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
    input.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
    input.fogCoord = i.fogFactor;
    input.vertexLighting = VertexLighting(i.positionWS, input.normalWS);
    input.bakedGI = SAMPLE_GI(i.lightmapUV, i.vertexSH, input.normalWS);
    input.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
    input.shadowMask = SAMPLE_SHADOWMASK(i.lightmapUV);
#if defined(_DBUFFER)
    ApplyDecalToSurfaceData(i.positionCS, surface, input);
#endif
    bool specularSetup = false, specularHighlights = true, alphaPremultiply = false;
    bool receiveShadows = true, transparent = false, environmentReflections = true;
#if defined(_SPECULAR_SETUP)
    specularSetup = true;
#endif
#if defined(_SPECULARHIGHLIGHTS_OFF)
    specularHighlights = false;
#endif
#if defined(_ALPHAPREMULTIPLY_ON)
    alphaPremultiply = true;
#endif
#if defined(_RECEIVE_SHADOWS_OFF)
    receiveShadows = false;
#endif
#if defined(_SURFACE_TYPE_TRANSPARENT)
    transparent = true;
#endif
#if defined(_ENVIRONMENTREFLECTIONS_OFF)
    environmentReflections = false;
#endif
    half4 color = UniversalFragmentPBR(input, surface, specularSetup, specularHighlights,
        alphaPremultiply, false, receiveShadows, transparent, environmentReflections);
    color.rgb = ClampExposed(input.preExposureMultiplier * MixFog(color.rgb, i.fogFactor));
    return color;
}
#endif
