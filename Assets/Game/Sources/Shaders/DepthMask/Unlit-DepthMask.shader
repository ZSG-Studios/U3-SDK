Shader "Unlit/DepthMask"
{
	SubShader
	{
		Tags
		{
			"RenderPipeline" = "UniversalPipeline"
			"Queue" = "Geometry+10"
		}

		ColorMask 0
		ZWrite On

		Pass
		{
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			float4 Vert(float3 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS); }
			half4 Frag() : SV_Target { return 0; }
			ENDHLSL
		}
	}
}
