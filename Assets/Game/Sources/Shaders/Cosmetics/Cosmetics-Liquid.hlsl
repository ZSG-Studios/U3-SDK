#include "Assets/Game/Sources/Shaders/CGIncludes/UnturnedSurface.hlsl"
CBUFFER_START(UnityPerMaterial)
half4 _Color;
half4 _RainbowScale;
float4 _Albedo0_ST;
CBUFFER_END


		TEXTURE2D(_Albedo0); SAMPLER(sampler_Albedo0);
		TEXTURE2D(_Albedo1); SAMPLER(sampler_Albedo1);
		TEXTURE2D(_Metallic0); SAMPLER(sampler_Metallic0);
		TEXTURE2D(_Metallic1); SAMPLER(sampler_Metallic1);
		TEXTURE2D(_Emission0); SAMPLER(sampler_Emission0);
		TEXTURE2D(_Emission1); SAMPLER(sampler_Emission1);


		struct Input {
			float2 uv_Albedo0;
		};

		void vert(inout UnturnedAttributes v, out Input OUT)
		{
			OUT = (Input)0;
		}

		void surf(Input IN, inout UnturnedSurface OUT)
		{
			half2 uv0 = float2(_RainbowScale.x, _RainbowScale.y) * _Time.y + IN.uv_Albedo0; // scale uv and offset by time
			half2 uv1 = float2(_RainbowScale.z, _RainbowScale.w) * _Time.y + IN.uv_Albedo0; // scale uv and offset by time

			half4 albedo0 = SAMPLE_TEXTURE2D(_Albedo0, sampler_Albedo0, uv0);
			half4 albedo1 = SAMPLE_TEXTURE2D(_Albedo1, sampler_Albedo1, uv1);

			half4 metallic0 = SAMPLE_TEXTURE2D(_Metallic0, sampler_Metallic0, uv0);
			half4 metallic1 = SAMPLE_TEXTURE2D(_Metallic1, sampler_Metallic1, uv1);

			half4 emission0 = SAMPLE_TEXTURE2D(_Emission0, sampler_Emission0, uv0);
			half4 emission1 = SAMPLE_TEXTURE2D(_Emission1, sampler_Emission1, uv1);

			half4 albedo = albedo0 * albedo0.a + albedo1 * (1.0 - albedo0.a);
			half4 metallic = metallic0 * (1.0 - albedo.a) * metallic0.a + metallic1 * (1.0 - albedo.a) * (1.0 - metallic0.a);
			half4 emission = emission0 * (1.0 - albedo.a) * emission0.a + emission1 * (1.0 - albedo.a) * (1.0 - emission0.a);

			OUT.Albedo = albedo.rgb * albedo.a;

			OUT.Metallic = metallic.r;
			OUT.Smoothness = metallic.a;

			// 2023-01-31: Multiplying by 2 is hack to make emissive glow consistent throughout the game.
			// Previously all standard materials with emissive were updated to use color 2.0. (public issue #3680)
			OUT.Emission = emission.rgb * 2.0;
		}



UnturnedVaryings UnturnedVert(UnturnedAttributes v)
{
    UnturnedVaryings o = (UnturnedVaryings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    Input customInput = (Input)0;
vert(v, customInput);
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
    input.uv_Albedo0 = i.uv * _Albedo0_ST.xy + _Albedo0_ST.zw;
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
