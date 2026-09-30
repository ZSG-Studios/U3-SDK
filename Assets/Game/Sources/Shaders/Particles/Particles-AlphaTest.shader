// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnturnedObjectToClip(*)'

Shader "Particles/Cutout"
{
	Properties {
		_TintColor("Tint Color", Color) = (0.5,0.5,0.5,0.5)
		_MainTex("Particle Texture", 2D) = "white" {}
	}

	Category
	{
		Tags { "RenderPipeline"="UniversalPipeline"
			"Queue"="AlphaTest"
			"IgnoreProjector"="True"
			"RenderType"="TransparentCutout"
		}

		SubShader
		{
			Pass
			{
				HLSLPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma multi_compile _ SOFTPARTICLES_ON
				#pragma multi_compile_fog

				#include "Assets/Game/Sources/Shaders/CGIncludes/UnturnedUnlit.hlsl"

				half4 _TintColor;
				TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
				float4 _MainTex_ST;

				struct appdata_t
				{
					float4 vertex : POSITION;
					half4 color : COLOR;
					float2 texcoord : TEXCOORD0;
				};

				struct v2f {
					float4 vertex : SV_POSITION;
					half4 color : COLOR;
					float2 texcoord : TEXCOORD0;
					half fogCoord : TEXCOORD1;
				};

				v2f vert (appdata_t v)
				{
					v2f o;

					o.vertex = UnturnedObjectToClip(v.vertex);
					o.color = v.color;
					o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
					o.fogCoord = ComputeFogFactor(o.vertex.z);

					return o;
				}

				half4 frag (v2f i) : SV_Target
				{
					half4 col = 2.0f * i.color * _TintColor * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord);
					col.rgb = MixFogColor(col.rgb, half3(0,0,0), i.fogCoord); // fog towards black due to our blend mode

					if(col.a < 1.0) discard;

					return col;
				}

				ENDHLSL
			}
		}
	}
}
