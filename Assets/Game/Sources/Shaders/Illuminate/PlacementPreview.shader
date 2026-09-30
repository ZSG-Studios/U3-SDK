Shader "Unturned/Placement Preview"
{
	Properties
	{
		_Color ("Main Color", Color) = (1,1,1,1)
	}
	SubShader
{
Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
Cull Off
Pass { Name "Forward" Tags { "LightMode"="UniversalForwardOnly" }

Blend SrcAlpha OneMinusSrcAlpha
ZWrite Off
HLSLPROGRAM
#pragma target 4.5
#pragma vertex UnturnedVert
#pragma fragment UnturnedFrag
#pragma multi_compile_instancing

#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
#pragma multi_compile _ _CLUSTER_LIGHT_LOOP
#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
#pragma multi_compile_fragment _ _SHADOWS_SOFT
#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
#pragma multi_compile _ LIGHTMAP_ON
#pragma multi_compile _ DIRLIGHTMAP_COMBINED
#pragma multi_compile_fog
#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
#include "Assets/Game/Sources/Shaders/Illuminate/PlacementPreview.hlsl"
ENDHLSL
}
Pass { Name "Meta" Tags { "LightMode"="Meta" }
Cull Off
HLSLPROGRAM
#pragma target 4.5
#pragma vertex UnturnedVert
#pragma fragment UnturnedFrag
#pragma multi_compile_instancing

#define UNTURNED_META_PASS 1
#include "Assets/Game/Sources/Shaders/Illuminate/PlacementPreview.hlsl"
ENDHLSL
}
}
Fallback Off
}
