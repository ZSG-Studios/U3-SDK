// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnturnedObjectToClip(*)'

Shader "Unlit/Aurora Borealis"
{
	Properties
	{
		_MainTex ("Color Strip", 2D) = "white" {}
		_Pattern("Pattern", 2D) = "white" {}
		_Intensity ("Intensity", Float) = 1
	}

	SubShader
	{
		Tags { "RenderPipeline"="UniversalPipeline"
			"Queue" = "Transparent"
			"IgnoreProjector" = "True"
			"RenderType" = "Transparent"
		}

		LOD 100
		Cull Off
		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment frag

			#include "Assets/Game/Sources/Shaders/CGIncludes/UnturnedUnlit.hlsl"

			struct appdata_t
			{
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
				float4 color : COLOR;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				half2 texcoord0 : TEXCOORD0; // color
				half2 texcoord1 : TEXCOORD1; // pattern
				float4 color : COLOR;
			};

			TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
			float4 _MainTex_ST;
			TEXTURE2D(_Pattern); SAMPLER(sampler_Pattern);
			float4 _Pattern_ST;
			float _Intensity;
			half _AtmosphericFog;

			v2f vert(appdata_t v)
			{
				v2f o;
				o.vertex = UnturnedObjectToClip(v.vertex);
				o.texcoord1 = TRANSFORM_TEX(v.texcoord, _Pattern);
				o.color = v.color;

				float vertexTimeOffsetWave = v.vertex.x * 2 + v.vertex.y * 2 + sin(_Time.w / 64 + v.vertex.y * 4) / 16;
				float vertexTimeOffset = v.vertex.x * 3 + abs(sin(_Time.w / 64 + vertexTimeOffsetWave)) * 2;

				o.vertex.x += sin(_Time.w / 128 + vertexTimeOffset) * 32;

				float colorTimeOffset = v.vertex.y / 8;

				o.texcoord0.x = _Time.w / 256 + colorTimeOffset;

				float alphaTimeOffsetWave = v.vertex.x / 4 + v.vertex.y / 4 + sin(_Time.w / 32 + v.vertex.y / 4) / 16;
				float alphaTimeOffset = v.vertex.x / 4 + abs(sin(_Time.w / 64 + alphaTimeOffsetWave)) * 2;

				o.texcoord0.y = abs(sin(_Time.w / 64 + alphaTimeOffset));

				float patternTimeOffset = v.vertex.y * 8;

				o.texcoord1.x += _Time.w / 1024 + patternTimeOffset;

				return o;
			}

			half4 frag(v2f i) : SV_Target
			{
				half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord0);
				half4 pat = SAMPLE_TEXTURE2D(_Pattern, sampler_Pattern, i.texcoord1);
				col.a *= pat.a;
				col.a *= i.texcoord0.y;
				col.a *= _Intensity;
				col.a *= i.color.r;
				col.a *= (1 - _AtmosphericFog); // Fade out during storm

				return col;
			}

			ENDHLSL
		}
	}
}
