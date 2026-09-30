#include "Assets/Game/Sources/Shaders/CGIncludes/UnturnedSurface.hlsl"
CBUFFER_START(UnityPerMaterial)
half4 _Color;
half4 _RainbowScale0;
half4 _RainbowScale1;
CBUFFER_END



		#include "Assets/Game/Sources/Shaders/CGIncludes/ProjectionMapping.cginc"

		TEXTURE2D(_Albedo0); SAMPLER(sampler_Albedo0);
		TEXTURE2D(_Albedo1); SAMPLER(sampler_Albedo1);
		TEXTURE2D(_Emission0); SAMPLER(sampler_Emission0);
		TEXTURE2D(_Emission1); SAMPLER(sampler_Emission1);


		struct Input {
			float3 worldPos;
			float3 worldNormal;
			float3 viewDir;
		};

		void surf(Input IN, inout UnturnedSurface OUT)
		{
			float3 worldPos0 = IN.worldPos + _RainbowScale0.xyz * _Time.y;
			float3 worldPos1 = IN.worldPos + _RainbowScale1.xyz * _Time.y;

			half4 albedo0 = planarSample4(TEXTURE2D_ARGS(_Albedo0, sampler_Albedo0), worldPos0, _RainbowScale0.w);
			half4 albedo1 = planarSample4(TEXTURE2D_ARGS(_Albedo1, sampler_Albedo1), worldPos1, _RainbowScale1.w);

			half4 emission0 = planarSample4(TEXTURE2D_ARGS(_Emission0, sampler_Emission0), worldPos0, _RainbowScale0.w);
			half4 emission1 = planarSample4(TEXTURE2D_ARGS(_Emission1, sampler_Emission1), worldPos1, _RainbowScale1.w);

			//float3 blend0 = triplanarBlend(worldPos0, IN.worldNormal, 2);
			//float3 blend1 = triplanarBlend(worldPos1, IN.worldNormal, 2);

			//half4 albedo0 = triplanarSample4(TEXTURE2D_ARGS(_Albedo0, sampler_Albedo0), worldPos0, blend0, _RainbowScale0.w);
			//half4 albedo1 = triplanarSample4(TEXTURE2D_ARGS(_Albedo1, sampler_Albedo1), worldPos1, blend1, _RainbowScale1.w);

			//half4 emission0 = triplanarSample4(TEXTURE2D_ARGS(_Emission0, sampler_Emission0), worldPos0, blend0, _RainbowScale0.w);
			//half4 emission1 = triplanarSample4(TEXTURE2D_ARGS(_Emission1, sampler_Emission1), worldPos1, blend1, _RainbowScale1.w);

			half4 albedo = albedo0 * albedo0.a + albedo1 * (1.0 - albedo0.a);
			half4 emission = emission0 * (1.0 - albedo.a) * emission0.a + emission1 * (1.0 - albedo.a) * (1.0 - emission0.a);

			OUT.Albedo = albedo.rgb;
			OUT.Alpha = albedo.a;

			OUT.Emission = emission.rgb;
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
    input.worldPos = i.positionWS; input.worldNormal = normalize(i.normalWS); input.viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
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
