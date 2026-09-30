Shader "Custom/Intersect"
{
	Properties
	{
		_IntersectColor("Intersect Color", Color) = (1, 1, 1, 1)
		_IntersectSize("Intersect Size", Float) = 2.5
	}

	Subshader
	{
		Tags { "RenderPipeline"="UniversalPipeline"
			"Queue" = "AlphaTest"
			"IgnoreProjector" = "True"
			"RenderType" = "TransparentCutout"
		}

		Cull Off

		Pass
		{
			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#include "Assets/Game/Sources/Shaders/CGIncludes/UnturnedUnlit.hlsl"

			half4 _IntersectColor;
			half _IntersectSize;
			 //Depth Texture

			struct v2f
			{
				float4 pos : SV_POSITION;
				float4 ref : TEXCOORD0;
			};

			v2f vert(UnturnedUnlitAttributes v)
			{
				v2f o;

				o.pos = UnturnedObjectToClip(v.vertex);
				o.ref = ComputeScreenPos(o.pos);
				o.ref.z = -TransformWorldToView(TransformObjectToWorld(v.vertex.xyz)).z;

				return o;
			}

			half4 frag(v2f i) : SV_Target
			{
				float sceneZ = UnturnedLinearEyeDepth(SampleSceneDepth(i.ref.xy / i.ref.w));
				float objectZ = i.ref.z;

				float diff = 1 - saturate((sceneZ - objectZ) / _IntersectSize);

				clip(diff - 0.5);
				return _IntersectColor;
			}

			ENDHLSL
		}
	}
}