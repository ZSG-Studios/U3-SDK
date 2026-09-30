Shader "Decal/Emissive"
{
Properties
	{
		_MainTex ("Diffuse", 2D) = "white" {}
		_EmissionMap ("Emission", 2D) = "black" {}
		_Cutoff ("Cutoff", float) = 0.5
[HideInInspector] Normal_Blend("Normal Blend", Float) = 0
	}

	SubShader { Tags { "RenderPipeline"="UniversalPipeline" }
UsePass "Unturned/ProjectedDecalEmissive/DBUFFERPROJECTOR"
UsePass "Unturned/ProjectedDecalEmissive/DECALPROJECTORFORWARDEMISSIVE"
UsePass "Unturned/ProjectedDecalEmissive/DECALSCREENSPACEPROJECTOR"
UsePass "Unturned/ProjectedDecalEmissive/DECALGBUFFERPROJECTOR"
}
Fallback Off
}
