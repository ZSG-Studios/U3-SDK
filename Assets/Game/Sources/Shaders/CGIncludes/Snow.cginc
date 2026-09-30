#ifndef SNOW_CGINC_INCLUDED
#define SNOW_CGINC_INCLUDED

#include "Assets/Game/Sources/Shaders/CGIncludes/ProjectionMapping.cginc"

static const float SNOW_SPARKLE_UV_SCALE = 0.5f;

TEXTURE2D(_Snow_Sparkle_Map); SAMPLER(sampler_Snow_Sparkle_Map);

void snow(float3 worldPos, float3 blend, float3 viewDir, float mask, inout half3 Albedo)
{
	float3 sparkleNormal = triplanarSample3(TEXTURE2D_ARGS(_Snow_Sparkle_Map, sampler_Snow_Sparkle_Map), worldPos, blend, 4);
	sparkleNormal = (sparkleNormal * 2) - 1;
	sparkleNormal = normalize(sparkleNormal); // Maybe the texture itself should be normalized

	float sparkle = saturate(abs(dot(sparkleNormal, viewDir)) - 0.98);
	Albedo = saturate(Albedo + sparkle * 1000 * mask);
}

#endif