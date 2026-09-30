// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Framework/Alpha Blended Lit Anim" {
Properties {
	_TintColor ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
	_MainTex ("Particle Texture", 2D) = "white" {}
	_InvFade ("Soft Particles Factor", Range(0.01,3.0)) = 1.0
	_Anim("Anim", Vector) = (0, 0, 0, 0)
}

Category {
	Tags { "RenderPipeline"="UniversalPipeline"  "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
	Blend SrcAlpha OneMinusSrcAlpha
	ColorMask RGB
	Cull Off Lighting Off ZWrite Off

	SubShader {
		Pass {

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 2.0
			#pragma multi_compile _ SOFTPARTICLES_ON
			#pragma multi_compile_fog

			#include "Assets/Game/Sources/Shaders/CGIncludes/UnturnedUnlit.hlsl"

			TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
			half4 _TintColor;
			half4 _Anim;
			float4 _AlphaParticleLightingColor; // Set globally by LevelLighting

			struct appdata_t {
				float4 vertex : POSITION;
				half4 color : COLOR;
				float2 texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				half4 color : COLOR;
				float2 texcoord : TEXCOORD0;
				half fogCoord : TEXCOORD1;
				#ifdef SOFTPARTICLES_ON
				float4 projPos : TEXCOORD2;
				#endif
				UNITY_VERTEX_OUTPUT_STEREO
			};

			float4 _MainTex_ST;

			v2f vert (appdata_t v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				o.vertex = UnturnedObjectToClip(v.vertex);
				#ifdef SOFTPARTICLES_ON
				o.projPos = ComputeScreenPos (o.vertex);
				o.projPos.z = -TransformWorldToView(TransformObjectToWorld(v.vertex.xyz)).z;
				#endif
				o.color = v.color * _TintColor * _AlphaParticleLightingColor;
				float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				o.texcoord = float2(_Anim.x, _Anim.y) * _Time.y + float2((abs(worldPos.x) + abs(worldPos.z)) / 16, abs(worldPos.y) / 16) + TRANSFORM_TEX(v.texcoord,_MainTex);
				o.fogCoord = ComputeFogFactor(o.vertex.z);
				return o;
			}


			float _InvFade;

			half4 frag (v2f i) : SV_Target
			{
				#ifdef SOFTPARTICLES_ON
				float sceneZ = UnturnedLinearEyeDepth(SampleSceneDepth(i.projPos.xy / i.projPos.w));
				float partZ = i.projPos.z;
				float fade = saturate (_InvFade * (sceneZ-partZ));
				i.color.a *= fade;
				#endif

				half4 col = 2.0f * i.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord);
				col.rgb = MixFog(col.rgb, i.fogCoord);
				return col;
			}
			ENDHLSL
		}
	}
}
}
