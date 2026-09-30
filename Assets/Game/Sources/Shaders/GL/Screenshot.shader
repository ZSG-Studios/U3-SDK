Shader "Unturned/Screenshot"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Assets/Game/Sources/Shaders/CGIncludes/UnturnedUnlit.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnturnedObjectToClip(v.vertex);
                o.uv = v.uv;
                return o;
            }

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            half4 frag (v2f i) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
				// The purpose of this shader is to override alpha to 1.0 during CaptureScreenshotAsTexture. (public issue #3670)
				col.a = 1.0;
                return col;
            }
            ENDHLSL
        }
    }
}
