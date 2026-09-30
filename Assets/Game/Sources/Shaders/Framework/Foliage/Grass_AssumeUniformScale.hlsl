#include "Assets/Game/Sources/Shaders/CGIncludes/UnturnedSurface.hlsl"
CBUFFER_START(UnityPerMaterial)
half4 _Color;
float4 _MainTex_ST;
float _Cutoff;
CBUFFER_END







		#include "Assets/Game/Sources/Shaders/CGIncludes/Landscapes/FoliageWind.cginc"


		TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);




		struct Input {
			float2 uv_MainTex;
		};

		void vert(inout UnturnedAttributes IN, out Input OUT)
		{
			OUT = (Input)0;

#ifdef GRASS_WIND_ON
			float windInfluence = IN.color.r;
			float4 worldPos = mul(unity_ObjectToWorld, IN.vertex);
			windAnimation(worldPos, windInfluence);
			IN.vertex = mul(unity_WorldToObject, worldPos);
#endif
		}

		void surf(Input IN, inout UnturnedSurface OUT)
		{
			half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv_MainTex) * _Color;
			OUT.Albedo = c.rgb;
			OUT.Alpha = c.a;
			OUT.Specular = 0.0;
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
    input.uv_MainTex = i.uv * _MainTex_ST.xy + _MainTex_ST.zw;
    UnturnedSurface s = UnturnedDefaultSurface();
    surf(input, s);
    clip(s.Alpha - _Cutoff);
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
