Shader "Hidden/Custom/SkyFog"
{
	HLSLINCLUDE

				#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
		float4 _FogColor;




		uniform float4x4 _InverseProjectionMatrix;
		uniform float4x4 _CameraToWorld;

		uniform float3 _SkyColor;
		uniform float3 _EquatorColor;
		uniform float3 _GroundColor;

		// Global skybox amount.
		uniform float _AtmosphericFog;

		uniform float3 _WaterColor;
		uniform float _IsCameraUnderwater;
		uniform int _WaterCount;
		uniform float4x4 _WaterMatrices[3];

		float IsWithinWater(float3 originWS)
		{
			const float HALF_BOUND_PLUS_ONE = asfloat(0x3f000001); // 0.5f with LSB set (public issue #5022)
			for (int index = 0; index < _WaterCount; ++index)
			{
				float4x4 worldToLocal = _WaterMatrices[index];
				float3 originLocal = mul(worldToLocal, float4(originWS, 1.0)).xyz;
				originLocal = abs(originLocal);
				if (originLocal.x <= HALF_BOUND_PLUS_ONE && originLocal.y <= HALF_BOUND_PLUS_ONE && originLocal.z <= HALF_BOUND_PLUS_ONE)
				{
					return 1.0;
				}
			}

			return 0.0;
		}

		float4 Frag(Varyings input) : SV_Target
		{
			float4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
			float rawDepth = SampleSceneDepth(input.texcoord);
			float depth = LinearEyeDepth(rawDepth, _ZBufferParams);
#if UNITY_REVERSED_Z
			float farDepth = 0.00001;
#else
			float farDepth = 0.99999;
#endif
			float3 farPosition = ComputeWorldSpacePosition(input.texcoord, farDepth, UNITY_MATRIX_I_VP);
			float3 viewDir = normalize(farPosition - _WorldSpaceCameraPos);

			// 3rd column is the camera's -Z axis in world space.
			float3 forward = float3(-_CameraToWorld._m02, -_CameraToWorld._m12, -_CameraToWorld._m22);

			// Right angle triangle where adjacent is depth and we want to calculate hypotenuse (spherical depth).
			// cos = adj / hyp -> hyp = adj / cos
			float3 viewportDir = viewDir;
			float sphericalDepth = depth / dot(viewportDir, forward);

			// Treat below horizon as "not skybox" so that ocean along horizon gets fog.
			#if UNITY_REVERSED_Z
			float notSkybox = rawDepth > 0 || viewportDir.y < 0.0;
#else
			float notSkybox = rawDepth < 1 || viewportDir.y < 0.0;
#endif

			float farClipDist = _ProjectionParams.z;
			float fogStartDist = farClipDist * 0.5;
			float fogTransitionDist = farClipDist - fogStartDist;
			float fogAlpha = saturate((sphericalDepth - fogStartDist) / fogTransitionDist);
			fogAlpha = pow(fogAlpha, 2.0);

			// Identical to sky gradient in skybox shader.
			float3 skyColor;
			float skyFactor = 1 - pow(1 - abs(viewportDir.y), 4);
			if (viewportDir.y > 0)
			{
				skyColor = lerp(_EquatorColor, _SkyColor, skyFactor);
			}
			else
			{
				skyColor = lerp(_EquatorColor, _GroundColor, skyFactor);
			}

			skyColor = lerp(skyColor, _FogColor.rgb, _AtmosphericFog);

			float3 outputColor = lerp(color.rgb, skyColor, fogAlpha * notSkybox);

			// _ProjectionParams.y is near clip plane distance.
			float3 viewPos = _WorldSpaceCameraPos + viewportDir * (_ProjectionParams.y / max(dot(viewportDir, forward), 0.0001));
			if (abs(_IsCameraUnderwater - IsWithinWater(viewPos)) > 0.5)
			{
				// Water mask when fragment is underwater without global fog enabled, or if fragment is not underwater
				// and camera is underwater.
				outputColor = _WaterColor;
			}

			return float4(outputColor, color.a);
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
