Shader "Decal/Diffuse"
{
Properties
	{
		_MainTex ("Diffuse", 2D) = "white" {}
		_Cutoff ("Cutoff", float) = 0.5
[HideInInspector] Normal_Blend("Normal Blend", Float) = 0
	}

	SubShader { Tags { "RenderPipeline"="UniversalPipeline" }
UsePass "Unturned/ProjectedDecalCutout/DBUFFERPROJECTOR"
UsePass "Unturned/ProjectedDecalCutout/DECALSCREENSPACEPROJECTOR"
UsePass "Unturned/ProjectedDecalCutout/DECALGBUFFERPROJECTOR"
}
Fallback Off
}
