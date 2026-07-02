#ifndef NEOMANTRA_MAINLIGHT_INCLUDED
#define NEOMANTRA_MAINLIGHT_INCLUDED

#ifndef SHADERGRAPH_PREVIEW
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
#endif

void MainLight_float(float3 WorldPos, out float3 Direction, out float3 Color, out float DistanceAtten, out float ShadowAtten)
{
#ifdef SHADERGRAPH_PREVIEW
    Direction = normalize(float3(0.5, 0.5, 0.25));
    Color = float3(1, 1, 1);
    DistanceAtten = 1;
    ShadowAtten = 1;
#else
#if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(WorldPos));
#else
    float4 shadowCoord = TransformWorldToShadowCoord(WorldPos);
#endif
    Light light = GetMainLight(shadowCoord);
    Direction = light.direction;
    Color = light.color;
    DistanceAtten = light.distanceAttenuation;
    ShadowAtten = light.shadowAttenuation;
#endif
}
#endif
