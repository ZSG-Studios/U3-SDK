// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnturnedObjectToClip(*)'

// Unlit shader. Simplest possible colored shader.
// - no lighting
// - no lightmap support
// - no texture

Shader "Unlit/Rope" {
Properties {
	_Color ("Main Color", Color) = (1,1,1,1)
}

SubShader {
	Tags { "RenderPipeline"="UniversalPipeline"  "RenderType"="Opaque" }
	LOD 100

	Pass {
		HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog

			#include "Assets/Game/Sources/Shaders/CGIncludes/UnturnedUnlit.hlsl"

			struct appdata_t {
				float4 vertex : POSITION;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				half fogCoord : TEXCOORD0;
			};

			half4 _Color;

			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnturnedObjectToClip(v.vertex);
				o.fogCoord = ComputeFogFactor(o.vertex.z);
				return o;
			}

			half4 frag (v2f i) : SV_Target
			{
				half4 col = UNITY_LIGHTMODEL_AMBIENT*_Color;//_Color;
				col.rgb = MixFog(col.rgb, i.fogCoord);
				col.a = 1;
				return col;
			}
		ENDHLSL
	}
}

}
