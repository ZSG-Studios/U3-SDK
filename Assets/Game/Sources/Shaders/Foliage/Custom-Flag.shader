Shader "Custom/Flag"
{
	Properties
	{
		_Color("Main Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_WaveAndDistance("Wave and distance", Vector) = (12, 3.6, 1, 1)
	}

	SubShader
{
Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
Cull Off
Pass { Name "Forward" Tags { "LightMode"="UniversalForwardOnly" }

HLSLPROGRAM
#pragma target 4.5
#pragma vertex UnturnedVert
#pragma fragment UnturnedFrag
#pragma multi_compile_instancing
#pragma multi_compile ___ NICE_FOLIAGE_ON
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
#include "Assets/Game/Sources/Shaders/Foliage/Custom-Flag.hlsl"
ENDHLSL
}
Pass { Name "ShadowCaster" Tags { "LightMode"="ShadowCaster" }
ColorMask 0
HLSLPROGRAM
#pragma target 4.5
#pragma vertex UnturnedVert
#pragma fragment UnturnedFrag
#pragma multi_compile_instancing
#pragma multi_compile ___ NICE_FOLIAGE_ON
#pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
#define UNTURNED_SHADOW_PASS 1
float3 _LightDirection;
float3 _LightPosition;
#include "Assets/Game/Sources/Shaders/Foliage/Custom-Flag.hlsl"
ENDHLSL
}
Pass { Name "DepthOnly" Tags { "LightMode"="DepthOnly" }
ColorMask 0
HLSLPROGRAM
#pragma target 4.5
#pragma vertex UnturnedVert
#pragma fragment UnturnedFrag
#pragma multi_compile_instancing
#pragma multi_compile ___ NICE_FOLIAGE_ON
#define UNTURNED_DEPTH_PASS 1
#include "Assets/Game/Sources/Shaders/Foliage/Custom-Flag.hlsl"
ENDHLSL
}
Pass { Name "DepthNormals" Tags { "LightMode"="DepthNormalsOnly" }

HLSLPROGRAM
#pragma target 4.5
#pragma vertex UnturnedVert
#pragma fragment UnturnedFrag
#pragma multi_compile_instancing
#pragma multi_compile ___ NICE_FOLIAGE_ON
#pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
#pragma multi_compile _ _WRITE_SMOOTHNESS
#define UNTURNED_NORMALS_PASS 1
#include "Assets/Game/Sources/Shaders/Foliage/Custom-Flag.hlsl"
ENDHLSL
}
Pass { Name "Meta" Tags { "LightMode"="Meta" }
Cull Off
HLSLPROGRAM
#pragma target 4.5
#pragma vertex UnturnedVert
#pragma fragment UnturnedFrag
#pragma multi_compile_instancing
#pragma multi_compile ___ NICE_FOLIAGE_ON
#define UNTURNED_META_PASS 1
#include "Assets/Game/Sources/Shaders/Foliage/Custom-Flag.hlsl"
ENDHLSL
}
}
Fallback Off
}
