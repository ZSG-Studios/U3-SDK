Shader "Custom/Intersect-Transparent"
{
	Properties
	{
		_IntersectColor("Intersect Color", Color) = (1, 1, 1, 1)

		// x = Size0
		// y = Alpha0
		// z = Size1
		// w = Alpha1
		_IntersectParams("Intersect Params", Vector) = (1, 1, 1, 1)
	}

	Subshader
	{
		Tags { "RenderPipeline"="UniversalPipeline"
			"Queue" = "Transparent"
			"IgnoreProjector" = "True"
			"RenderType" = "Transparent"
		}

		LOD 200
		ZWrite Off
		Cull Off
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#include "Assets/Game/Sources/Shaders/CGIncludes/UnturnedUnlit.hlsl"

			float4 _IntersectColor;
			float4 _IntersectParams;
			 //Depth Texture

			struct v2f
			{
				float4 pos : SV_POSITION;
				float4 texcoord : TEXCOORD0;
				float4 ref : TEXCOORD1;
			};

			v2f vert(UnturnedUnlitAttributes v)
			{
				v2f o;

				o.pos = UnturnedObjectToClip(v.vertex);
				o.texcoord = v.texcoord;
				o.ref = ComputeScreenPos(o.pos);
				o.ref.z = -TransformWorldToView(TransformObjectToWorld(v.vertex.xyz)).z;

				return o;
			}

			float4 frag(v2f i) : SV_Target
			{
				float sceneZ = UnturnedLinearEyeDepth(SampleSceneDepth(i.ref.xy / i.ref.w));
				float objectZ = i.ref.z;

				float sharpDepthAlpha = (1 - saturate((sceneZ - objectZ) / _IntersectParams.x)) * _IntersectParams.y;
				float fadeDepthAlpha = (1 - saturate((sceneZ - objectZ) / _IntersectParams.z)) * _IntersectParams.w;
				float depthAlpha = max(sharpDepthAlpha, fadeDepthAlpha);
				float wallAlpha = pow(i.texcoord.y, 2) / 10.0;
				float finalAlpha = max(max(sharpDepthAlpha, fadeDepthAlpha), wallAlpha);

				return float4(_IntersectColor.rgb, finalAlpha);//half4(_IntersectColor.xyz, finalAlpha);
			}

			ENDHLSL
		}
	}
}
