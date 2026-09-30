Shader "GL/LineDepthCheckeredColor"
{
	SubShader
	{
		Tags { "RenderPipeline"="UniversalPipeline"
			"RenderType" = "Opaque"
		}

		Pass
		{
			ZWrite Off
			Cull Off
			Blend SrcAlpha OneMinusSrcAlpha

			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#include "Assets/Game/Sources/Shaders/CGIncludes/UnturnedUnlit.hlsl"



			struct appdata
			{
				float4 vertex : POSITION;
				float4 color : COLOR;
			};

			struct v2f
			{
				float4 vertex : POSITION;
				float4 color : COLOR;
				float4 ref : TEXCOORD0;
			};

			v2f vert(appdata v)
			{
				v2f OUT;
				OUT.vertex = UnturnedGeometryToClip(v.vertex);
				OUT.color = v.color;
				OUT.ref = ComputeScreenPos(OUT.vertex);
				OUT.ref.z = -TransformWorldToView(TransformObjectToWorld(v.vertex.xyz)).z;

				return OUT;
			}

			float4 frag(v2f IN) : SV_Target
			{
				float sceneDepth = UnturnedLinearEyeDepth(SampleSceneDepth(IN.ref.xy / IN.ref.w));
				float objectDepth = IN.ref.z;

				if(sceneDepth > objectDepth) // 0 = close, 1 = far
				{
					return IN.color;
				}
				else
				{
					clip(frac((IN.ref.x + IN.ref.y) / IN.ref.w * 64) - 0.5);
					return IN.color * 0.5;
				}
			}

			ENDHLSL
		}
	}
}