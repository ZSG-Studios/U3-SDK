// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see THIRDPARTYNOTICES.txt)

// Unlit alpha-cutout shader.
// - no lighting
// - no lightmap support
// - no per-material color

Shader "Unlit/Atmosphere" {
Properties {

    _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
	_EmissionColor("Emission", Color) = (1, 1, 1, 1)
    _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
	_Intensity("Intensity", Range(0,10)) = 1.33
}
SubShader {
    Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}
    LOD 100

    Lighting Off

    Pass {
        HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Assets/Game/Sources/Shaders/CGIncludes/UnturnedUnlit.hlsl"

            struct appdata_t {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
			half4 _EmissionColor;
            half _Cutoff;
			half _Intensity;
			half _AtmosphericFog;

            v2f vert (appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnturnedObjectToClip(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord);
                clip(col.a - _Cutoff);

				// Reduce intensity as fog increases, otherwise the sun is unusually bright through the fog.
				float blendedIntensity = lerp(_Intensity, min(1.0, _Intensity), _AtmosphericFog);
                return lerp(col * _EmissionColor * blendedIntensity, unity_FogColor, _AtmosphericFog);
            }
        ENDHLSL
    }
}

}
