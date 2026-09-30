Shader "Decal/Blast"
{
Properties
	{
		_MainTex ("Diffuse", 2D) = "white" {}
[HideInInspector] Normal_Blend("Normal Blend", Float) = 0
	}

	SubShader { Tags { "RenderPipeline"="UniversalPipeline" }
UsePass "Unturned/ProjectedDecalBlend/DBUFFERPROJECTOR"
UsePass "Unturned/ProjectedDecalBlend/DECALSCREENSPACEPROJECTOR"
UsePass "Unturned/ProjectedDecalBlend/DECALGBUFFERPROJECTOR"
}
Fallback Off
}
