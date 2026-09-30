#include "Assets/Game/Sources/Shaders/CGIncludes/UnturnedSurface.hlsl"
CBUFFER_START(UnityPerMaterial)
float3 _SkinColor;
float _FlipShirt;
float4 _ShirtAlbedoTexture_ST;
CBUFFER_END


		TEXTURE2D(_FaceAlbedoTexture); SAMPLER(sampler_FaceAlbedoTexture);
		TEXTURE2D(_FaceEmissionTexture); SAMPLER(sampler_FaceEmissionTexture);
        TEXTURE2D(_ShirtAlbedoTexture); SAMPLER(sampler_ShirtAlbedoTexture);
		TEXTURE2D(_ShirtEmissionTexture); SAMPLER(sampler_ShirtEmissionTexture);
		TEXTURE2D(_ShirtMetallicTexture); SAMPLER(sampler_ShirtMetallicTexture);
		TEXTURE2D(_PantsAlbedoTexture); SAMPLER(sampler_PantsAlbedoTexture);
		TEXTURE2D(_PantsEmissionTexture); SAMPLER(sampler_PantsEmissionTexture);
		TEXTURE2D(_PantsMetallicTexture); SAMPLER(sampler_PantsMetallicTexture);

        struct Input {
            float2 uv_ShirtAlbedoTexture;
        };

        void surf(Input input, inout UnturnedSurface output)
        {
			float2 faceUV = (input.uv_ShirtAlbedoTexture * 8.0) - float2(6.0, 7.0); // Offset face texture to upper-right.
			float4 faceAlbedo = SAMPLE_TEXTURE2D(_FaceAlbedoTexture, sampler_FaceAlbedoTexture, faceUV);
			float4 faceEmission = SAMPLE_TEXTURE2D(_FaceEmissionTexture, sampler_FaceEmissionTexture, faceUV);
			float faceMask = step(0.0, faceUV.x) * step(faceUV.x, 1.0) * step(0.0, faceUV.y) * step(faceUV.y, 1.0);
			float faceAlpha = faceAlbedo.a * faceMask;

			// Front of shirt occupies the upper left 1/4, and back of shirt is the next 1/4 to the right.
			float2 shirtUV = input.uv_ShirtAlbedoTexture;
			float flipShirtU = ceil(shirtUV.x * 4.0) * 0.25 - frac(shirtUV.x * 4.0) * 0.25;
			float flipShirtAlpha = _FlipShirt * (shirtUV.x < 0.5) * (shirtUV.y > 0.75);
			shirtUV.x = lerp(shirtUV.x, flipShirtU, flipShirtAlpha);

			float4 shirtAlbedo = SAMPLE_TEXTURE2D(_ShirtAlbedoTexture, sampler_ShirtAlbedoTexture, shirtUV);
			float4 pantsAlbedo = SAMPLE_TEXTURE2D(_PantsAlbedoTexture, sampler_PantsAlbedoTexture, input.uv_ShirtAlbedoTexture);
			output.Albedo = lerp(lerp(lerp(_SkinColor, faceAlbedo.rgb, faceAlpha), shirtAlbedo.rgb, shirtAlbedo.a), pantsAlbedo.rgb, pantsAlbedo.a);

			float4 shirtEmission = SAMPLE_TEXTURE2D(_ShirtEmissionTexture, sampler_ShirtEmissionTexture, shirtUV);
			float4 pantsEmission = SAMPLE_TEXTURE2D(_PantsEmissionTexture, sampler_PantsEmissionTexture, input.uv_ShirtAlbedoTexture);
			output.Emission = lerp(lerp(faceEmission.rgb * faceAlpha, shirtEmission.rgb, shirtAlbedo.a), pantsEmission.rgb, pantsAlbedo.a) * 2.0;

			// Nelson 2025-09-10: previously, this actually was Metallic output, but the character
			// was noticeably shiny without ambient light. As I understand it, the "0" metallic
			// corresponds to ~0.04 gray specular, and 1 to the albedo, so we just use albedo as
			// the specular color.
			float4 shirtMetallic = SAMPLE_TEXTURE2D(_ShirtMetallicTexture, sampler_ShirtMetallicTexture, shirtUV);
			float4 pantsMetallic = SAMPLE_TEXTURE2D(_PantsMetallicTexture, sampler_PantsMetallicTexture, input.uv_ShirtAlbedoTexture);
			output.Specular = output.Albedo.rgb * lerp(shirtMetallic.r * shirtAlbedo.a, pantsMetallic.r, pantsAlbedo.a);
			output.Smoothness = lerp(shirtMetallic.a * shirtAlbedo.a, pantsMetallic.a, pantsAlbedo.a);
        }


UnturnedVaryings UnturnedVert(UnturnedAttributes v)
{
    UnturnedVaryings o = (UnturnedVaryings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    Input customInput = (Input)0;
    o.localPos = v.vertex.xyz;

    VertexPositionInputs pos = GetVertexPositionInputs(v.vertex.xyz);
    VertexNormalInputs normal = GetVertexNormalInputs(v.normal, v.tangent);
    o.positionCS = pos.positionCS;
    o.positionWS = pos.positionWS;
    o.normalWS = normal.normalWS;
    o.tangentWS = half4(normal.tangentWS, v.tangent.w * GetOddNegativeScale());
    o.uv = v.texcoord.xy;
    o.color = v.color;
    o.screenPos = ComputeScreenPos(pos.positionCS);
    o.fogFactor = ComputeFogFactor(pos.positionCS.z);
    OUTPUT_LIGHTMAP_UV(v.texcoord1.xy, unity_LightmapST, o.lightmapUV);
    OUTPUT_SH(o.normalWS, o.vertexSH);
#if defined(UNTURNED_SHADOW_PASS)
    float3 lightDirection = _LightDirection;
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    lightDirection = normalize(_LightPosition - pos.positionWS);
#endif
    o.positionCS = TransformWorldToHClip(ApplyShadowBias(pos.positionWS, normal.normalWS, lightDirection));
#if UNITY_REVERSED_Z
    o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE * o.positionCS.w);
#else
    o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE * o.positionCS.w);
#endif
#endif
#if defined(UNTURNED_META_PASS)
    o.positionCS = UnityMetaVertexPosition(v.vertex.xyz, v.texcoord1.xy, v.texcoord2.xy, unity_LightmapST, unity_DynamicLightmapST);
#endif
    return o;
}
UnturnedSurface EvaluateMaterial(UnturnedVaryings i)
{
    Input input = (Input)0;
    input.uv_ShirtAlbedoTexture = i.uv * _ShirtAlbedoTexture_ST.xy + _ShirtAlbedoTexture_ST.zw;
    UnturnedSurface s = UnturnedDefaultSurface();
    surf(input, s);

    return s;
}
half4 UnturnedFrag(UnturnedVaryings i) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    UnturnedSurface s = EvaluateMaterial(i);
#if defined(UNTURNED_DEPTH_PASS) || defined(UNTURNED_SHADOW_PASS)
    return 0;
#elif defined(UNTURNED_NORMALS_PASS)
    return UnturnedDepthNormals(i, s);
#elif defined(UNTURNED_META_PASS)
    MetaInput meta = (MetaInput)0;
    meta.Albedo = s.Albedo;
    meta.Emission = s.Emission;
    return UnityMetaFragment(meta);
#else
    return UnturnedLighting(i, s);
#endif
}
