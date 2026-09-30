// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnturnedObjectToClip(*)'

// Unlit shader. Simplest possible colored shader.
// - no lighting
// - no lightmap support
// - no texture

Shader "Unlit/Bubbles" {
Properties {
	_Color ("Main Color", Color) = (1,1,1,1)
	_MainTex("Base (RGB) Trans (A)", 2D) = "white" {}
}

SubShader {
	Tags { "RenderPipeline"="UniversalPipeline"  "Queue" = "AlphaTest" "IgnoreProjector" = "True" "RenderType" = "TransparentCutout" }
	LOD 200

	Pass {
		HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "Assets/Game/Sources/Shaders/CGIncludes/UnturnedUnlit.hlsl"

			struct appdata_t {
				float4 vertex : POSITION;
				float4 texcoord : TEXCOORD0;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				float4 uv : TEXCOORD0;
			};

			TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
			half4 _Color;

			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnturnedObjectToClip(v.vertex);
				o.uv = v.texcoord;
				return o;
			}

			half4 frag (v2f i) : SV_Target
			{
				half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv.xy) * _Color;
				half4 col = UNITY_LIGHTMODEL_AMBIENT*c;//_Color;
				clip(-0.5 + col.a);
				return col;
			}
		ENDHLSL
	}
}

}
