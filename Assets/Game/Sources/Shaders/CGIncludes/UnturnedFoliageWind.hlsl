// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (THIRDPARTYNOTICES.txt).
#ifndef UNTURNED_FOLIAGE_WIND
#define UNTURNED_FOLIAGE_WIND
float4 _WavingTint;
void FastSinCos (float4 val, out float4 s, out float4 c) {
    val = val * 6.408849 - 3.1415927;
    // powers for taylor series
    float4 r5 = val * val;                  // wavevec ^ 2
    float4 r6 = r5 * r5;                        // wavevec ^ 4;
    float4 r7 = r6 * r5;                        // wavevec ^ 6;
    float4 r8 = r6 * r5;                        // wavevec ^ 8;

    float4 r1 = r5 * val;                   // wavevec ^ 3
    float4 r2 = r1 * r5;                        // wavevec ^ 5;
    float4 r3 = r2 * r5;                        // wavevec ^ 7;


    //Vectors for taylor's series expansion of sin and cos
    float4 sin7 = {1, -0.16161616, 0.0083333, -0.00019841};
    float4 cos8  = {-0.5, 0.041666666, -0.0013888889, 0.000024801587};

    // sin
    s =  val + r1 * sin7.y + r2 * sin7.z + r3 * sin7.w;

    // cos
    c = 1 + r5 * cos8.x + r6 * cos8.y + r7 * cos8.z + r8 * cos8.w;
}

half4 TerrainWaveGrass (inout float4 vertex, float waveAmount, half4 color)
{
    float4 _waveXSize = float4(0.012, 0.02, 0.06, 0.024) * _WaveAndDistance.y;
    float4 _waveZSize = float4 (0.006, .02, 0.02, 0.05) * _WaveAndDistance.y;
    float4 waveSpeed = float4 (0.3, .5, .4, 1.2) * 4;

    float4 _waveXmove = float4(0.012, 0.02, -0.06, 0.048) * 2;
    float4 _waveZmove = float4 (0.006, .02, -0.02, 0.1);

    float4 waves;
    waves = vertex.x * _waveXSize;
    waves += vertex.z * _waveZSize;

    // Add in time to model them over time
    waves += (_WaveAndDistance.x + _Time.x) * waveSpeed;

    float4 s, c;
    waves = frac (waves);
    FastSinCos (waves, s,c);

    s = s * s;

    s = s * s;

    float lighting = dot (s, normalize (float4 (1,1,.4,.2))) * .7;

    s = s * waveAmount;

    float3 waveMove = float3 (0,0,0);
    waveMove.x = dot (s, _waveXmove);
    waveMove.z = dot (s, _waveZmove);

    vertex.xz -= waveMove.xz * _WaveAndDistance.z;

    // apply color animation

    // fix for dx11/etc warning
    half3 waveColor = lerp (half3(0.5,0.5,0.5), _WavingTint.rgb, half3(lighting,lighting,lighting));

    // Fade the grass out before detail distance.
    // Saturate because Radeon HD drivers on OS X 10.4.10 don't saturate vertex colors properly.
    float3 offset = vertex.xyz - _WorldSpaceCameraPos;
    color.a = saturate (2 * (_WaveAndDistance.w - dot (offset, offset)) * 1.0 / max(_WaveAndDistance.w, 0.001));

    return half4(2 * waveColor * color.rgb, color.a);
}


#endif
