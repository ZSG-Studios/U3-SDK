// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnturnedObjectToClip(*)'

// Unlit alpha-blended shader.
// - no lighting
// - no lightmap support
// - no per-material color

Shader "Unlit/RenderTexture" {
Properties {
	_MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
}

SubShader {
	Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
	LOD 100

	ZWrite Off
	Blend SrcAlpha OneMinusSrcAlpha

	Pass {
		HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog

			#include "Assets/Game/Sources/Shaders/CGIncludes/UnturnedUnlit.hlsl"

			struct appdata_t {
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				half2 texcoord : TEXCOORD0;
				half fogCoord : TEXCOORD1;
			};

			TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
			float4 _MainTex_ST;

			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnturnedObjectToClip(v.vertex);
				o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
				o.fogCoord = ComputeFogFactor(o.vertex.z);
				return o;
			}

			half4 frag (v2f i) : SV_Target
			{
				half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord);
				col.rgb = MixFog(col.rgb, i.fogCoord);
				col = pow(max(col, 0), 1.0/2.2);
				return col;
			}
		ENDHLSL
	}
}

}
