Shader "Hidden/Custom/ScopeVignette"
{
	HLSLINCLUDE

				#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"


		uniform float _ScopeAlpha;

		float4 Frag(Varyings input) : SV_Target
		{
// 			float2 pixelCoord = input.texcoord * _ScreenParams.xy;
// 			float2 screenCenter = _ScreenParams.xy * 0.5;
// 			float pixelDistFromCenter = distance(pixelCoord, screenCenter);
// 			float halfMinDimension = min(_ScreenParams.x, _ScreenParams.y) * 0.5;
//
// 			// 0 in the center of the screen, 1 at the edge of the closest side of screen
// 			float normalizedDistance = saturate(pixelDistFromCenter / halfMinDimension);
//
// 			// 1 in the center of the screen, 0 at the edge of the closest side of screen
// 			float alpha = pow(1.0 - normalizedDistance, 2);
//
// 			float4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
// 			color.rgb *= lerp(0.01, 0.5, alpha);
// 			color.a = 0;

			float3 sampledColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord.xy).rgb;
			sampledColor = lerp(sampledColor, float3(0.0, 0.0, 0.0), _ScopeAlpha);
			return float4(sampledColor, 0.0);
		}

	ENDHLSL

	SubShader
	{
		Tags { "RenderPipeline"="UniversalPipeline" }
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			HLSLPROGRAM
				#pragma vertex Vert
				#pragma fragment Frag
			ENDHLSL
		}
	}
}
