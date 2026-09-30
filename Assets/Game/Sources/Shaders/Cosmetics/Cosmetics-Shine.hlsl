#include "Assets/Game/Sources/Shaders/CGIncludes/UnturnedSurface.hlsl"
CBUFFER_START(UnityPerMaterial)
half3 _ShineScale;
half3 _ShineOffset;
float4 _Albedo_ST;
CBUFFER_END




		TEXTURE2D(_Albedo); SAMPLER(sampler_Albedo);
		TEXTURE2D(_Metallic); SAMPLER(sampler_Metallic);
		TEXTURE2D(_Emission); SAMPLER(sampler_Emission);
		TEXTURE2D(_Shine); SAMPLER(sampler_Shine);


		struct Input {
			float2 uv_Albedo;
			half shine;
		};

		void vert(inout UnturnedAttributes v, out Input OUT)
		{
			OUT = (Input)0;

			half3 vertex = v.vertex.xyz * _ShineOffset;
			half offset = vertex.x + vertex.y + vertex.z;
			OUT.shine = saturate(sin(offset + _Time.y * _ShineScale.x) * _ShineScale.y - _ShineScale.z);
		}

		void surf(Input IN, inout UnturnedSurface OUT)
		{
			half4 albedo = SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, IN.uv_Albedo);
			half4 metallic = SAMPLE_TEXTURE2D(_Metallic, sampler_Metallic, IN.uv_Albedo);
			half4 emission = SAMPLE_TEXTURE2D(_Emission, sampler_Emission, IN.uv_Albedo);
			half4 shine = SAMPLE_TEXTURE2D(_Shine, sampler_Shine, IN.uv_Albedo);

			OUT.Albedo = lerp(albedo.rgb, shine.rgb, IN.shine * shine.a);

			OUT.Metallic = metallic.r;
			OUT.Smoothness = metallic.a;

			OUT.Emission = emission.rgb;
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
    o.custom.x = customInput.shine;
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
    input.uv_Albedo = i.uv * _Albedo_ST.xy + _Albedo_ST.zw; input.shine = i.custom.x;
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
