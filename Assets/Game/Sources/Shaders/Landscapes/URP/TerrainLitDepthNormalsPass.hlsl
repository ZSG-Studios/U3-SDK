#ifndef UNIVERSAL_FORWARD_LIT_DEPTH_NORMALS_PASS_INCLUDED
#define UNIVERSAL_FORWARD_LIT_DEPTH_NORMALS_PASS_INCLUDED

#include "Assets/Game/Sources/Shaders/Landscapes/URP/TerrainLitPasses.hlsl"

// DepthNormal pass
struct AttributesDepthNormal
{
    float4 positionOS : POSITION;
    half3 normalOS : NORMAL;
    float2 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VaryingsDepthNormal
{
    float4 uvMainAndLM              : TEXCOORD0; // xy: control, zw: lightmap
    #ifndef TERRAIN_SPLAT_BASEPASS
        float4 uvSplat01                : TEXCOORD1; // xy: splat0, zw: splat1
        float4 uvSplat23                : TEXCOORD2; // xy: splat2, zw: splat3
    #endif

    #if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
        half4 normal                   : TEXCOORD3;    // xyz: normal, w: viewDir.x
        half4 tangent                  : TEXCOORD4;    // xyz: tangent, w: viewDir.y
        half4 bitangent                : TEXCOORD5;    // xyz: bitangent, w: viewDir.z
    #else
        half3 normal                   : TEXCOORD3;
    #endif

    float4 clipPos                  : SV_POSITION;
    float3 positionWS               : TEXCOORD6;
    UNITY_VERTEX_OUTPUT_STEREO
};

VaryingsDepthNormal DepthNormalOnlyVertex(AttributesDepthNormal v)
{
    VaryingsDepthNormal o = (VaryingsDepthNormal)0;

    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    TerrainInstancing(v.positionOS, v.normalOS, v.texcoord);

    const VertexPositionInputs attributes = GetVertexPositionInputs(v.positionOS.xyz);

    o.uvMainAndLM.xy = v.texcoord;
    o.uvMainAndLM.zw = v.texcoord * unity_LightmapST.xy + unity_LightmapST.zw;
    #ifndef TERRAIN_SPLAT_BASEPASS
        o.uvSplat01.xy = TRANSFORM_TEX(v.texcoord, _Splat0);
        o.uvSplat01.zw = TRANSFORM_TEX(v.texcoord, _Splat1);
        o.uvSplat23.xy = TRANSFORM_TEX(v.texcoord, _Splat2);
        o.uvSplat23.zw = TRANSFORM_TEX(v.texcoord, _Splat3);
    #endif

    o.normal.xyz = TransformObjectToWorldNormal(v.normalOS);
    #if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
        if (UseNormalMap())
        {
            half3 viewDirWS = GetWorldSpaceNormalizeViewDir(attributes.positionWS);
            float4 vertexTangent = float4(cross(float3(0, 0, 1), v.normalOS), 1.0);
            VertexNormalInputs normalInput = GetVertexNormalInputs(v.normalOS, vertexTangent);

            o.normal = half4(normalInput.normalWS, viewDirWS.x);
            o.tangent = half4(normalInput.tangentWS, viewDirWS.y);
            o.bitangent = half4(normalInput.bitangentWS, viewDirWS.z);
        }
    #endif

    o.clipPos = attributes.positionCS;
    o.positionWS = attributes.positionWS;
    return o;
}

void DepthNormalOnlyFragment(
    VaryingsDepthNormal IN
    , out half4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
    )
{
    #ifdef _ALPHATEST_ON
        ClipHoles(IN.uvMainAndLM.xy);
    #endif

    float2 splatUV = (IN.uvMainAndLM.xy * (_Control_TexelSize.zw - 1.0f) + 0.5f) * _Control_TexelSize.xy;
    half4 splatControl = SAMPLE_TEXTURE2D(_Control, sampler_Control, splatUV);

    half3 normalTS = half3(0.0h, 0.0h, 1.0h);
    // Terrain normal-map channels are Unturned snow masks, not tangent-space normals.

    half3 normalWS = IN.normal.xyz;
    #if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
        if (UseNormalMap())
            normalWS = TransformTangentToWorld(normalTS, half3x3(-IN.tangent.xyz, IN.bitangent.xyz, IN.normal.xyz));
    #elif defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
        float2 sampleCoords = (IN.uvMainAndLM.xy / _TerrainHeightmapRecipSize.zw + 0.5f) * _TerrainHeightmapRecipSize.xy;
        half3 perpixelNormalWS = TransformObjectToWorldNormal(normalize(SAMPLE_TEXTURE2D(_TerrainNormalmapTexture, sampler_TerrainNormalmapTexture, sampleCoords).rgb * 2 - 1));
        half3 tangentWS = cross(GetObjectToWorldMatrix()._13_23_33, perpixelNormalWS);
        normalWS = TransformTangentToWorld(normalTS, half3x3(-tangentWS, cross(perpixelNormalWS, tangentWS), perpixelNormalWS));
    #endif

    normalWS = NormalizeNormalPerPixel(normalWS, UseNormalMap());

    outNormalWS = half4(PackNormalWSToTexture(normalWS), 0.0);

    #if defined(_WRITE_SMOOTHNESS)
        // Match the visible terrain pass: dry ground is matte; rain creates puddles.
        #if defined(IS_RAINING)
            half3 ignoredAlbedo = 0, ignoredSpecular = 0;
            half smoothness = 0;
            rainSpecular(IN.positionWS, normalWS, 1, ignoredAlbedo, ignoredSpecular, smoothness);
            outNormalWS.a = smoothness;
        #endif
    #endif

    #ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
    #endif
}

#endif
