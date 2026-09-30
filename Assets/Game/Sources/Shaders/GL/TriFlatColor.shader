Shader "GL/TriFlatColor"
{
	SubShader
	{
		Tags { "RenderPipeline"="UniversalPipeline"
			"RenderType" = "Opaque"
		}

		Pass
		{
			ZWrite Off
			Cull Back
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
			};

			v2f vert(appdata v)
			{
				v2f OUT;
				OUT.vertex = UnturnedGeometryToClip(v.vertex);
				OUT.color = v.color;
				return OUT;
			}

			float4 frag(v2f IN) : SV_Target
			{
				return IN.color;
			}

			ENDHLSL
		}
	}
}