#include "Assets/Game/Sources/Shaders/CGIncludes/UnturnedSurface.hlsl"
CBUFFER_START(UnityPerMaterial)
half4 _ColorBase;
half4 _ColorSkin;
float4 _AlbedoBase_ST;
float4 _AlbedoSkin_ST;
CBUFFER_END


		TEXTURE2D(_AlbedoBase); SAMPLER(sampler_AlbedoBase);

		TEXTURE2D(_AlbedoSkin); SAMPLER(sampler_AlbedoSkin);
		TEXTURE2D(_MetallicBase); SAMPLER(sampler_MetallicBase);
		TEXTURE2D(_MetallicSkin); SAMPLER(sampler_MetallicSkin);
		TEXTURE2D(_NormalBase); SAMPLER(sampler_NormalBase);
		TEXTURE2D(_NormalSkin); SAMPLER(sampler_NormalSkin);
		TEXTURE2D(_EmissionBase); SAMPLER(sampler_EmissionBase);
		TEXTURE2D(_EmissionSkin); SAMPLER(sampler_EmissionSkin);

		struct Input {
			float2 uv_AlbedoBase;
			float2 uv_AlbedoSkin;
		};

		void surf(Input IN, inout UnturnedSurface OUT)
		{
			half4 albedoBase = SAMPLE_TEXTURE2D(_AlbedoBase, sampler_AlbedoBase, IN.uv_AlbedoBase);
			half4 albedoSkin = SAMPLE_TEXTURE2D(_AlbedoSkin, sampler_AlbedoSkin, IN.uv_AlbedoSkin);

			half4 metallicBase = SAMPLE_TEXTURE2D(_MetallicBase, sampler_MetallicBase, IN.uv_AlbedoBase);
			half4 metallicSkin = SAMPLE_TEXTURE2D(_MetallicSkin, sampler_MetallicSkin, IN.uv_AlbedoSkin);

			half4 normalBase = SAMPLE_TEXTURE2D(_NormalBase, sampler_NormalBase, IN.uv_AlbedoBase);
			half4 normalSkin = SAMPLE_TEXTURE2D(_NormalSkin, sampler_NormalSkin, IN.uv_AlbedoSkin);

			half4 emissionBase = SAMPLE_TEXTURE2D(_EmissionBase, sampler_EmissionBase, IN.uv_AlbedoBase);
			half4 emissionSkin = SAMPLE_TEXTURE2D(_EmissionSkin, sampler_EmissionSkin, IN.uv_AlbedoSkin);

			half4 albedo = albedoBase * _ColorBase * albedoBase.a + albedoSkin * _ColorSkin * (1.0 - albedoBase.a);
			half4 metallic = metallicBase * albedoBase.a + metallicSkin * (1.0 - albedoBase.a);
			half4 normal = normalBase * albedoBase.a + normalSkin * (1.0 - albedoBase.a);
			half4 emission = emissionBase * albedoBase.a + emissionSkin * (1.0 - albedoBase.a);

			OUT.Albedo = albedo.rgb;
			OUT.Alpha = albedo.a;

			OUT.Metallic = metallic.r;
			OUT.Normal = UnpackNormal(normal);
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
    input.uv_AlbedoBase = i.uv * _AlbedoBase_ST.xy + _AlbedoBase_ST.zw; input.uv_AlbedoSkin = i.uv * _AlbedoSkin_ST.xy + _AlbedoSkin_ST.zw;
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
