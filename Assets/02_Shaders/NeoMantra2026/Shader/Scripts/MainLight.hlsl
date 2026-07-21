#ifndef NEOMANTRA_MAINLIGHT_INCLUDED
#define NEOMANTRA_MAINLIGHT_INCLUDED

#ifndef SHADERGRAPH_PREVIEW
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
#endif

// 메인 라이트 정보를 전달하는 함수.
void MainLight_float(
    float3 WorldPos,
    out float3 Direction, out float3 Color, out float DistanceAtten, out float ShadowAtten)
{
#ifdef SHADERGRAPH_PREVIEW
    Direction = normalize(float3(0.5, 0.5, 0.25));
    Color = float3(1, 1, 1);
    DistanceAtten = 1;
    ShadowAtten = 1;
#else
#if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    // 화면 공간 그림자(SSShadows)일 경우.
    // 월드 위치를 화면 공간으로 변환 후 스크린 좌표로 변환하여 그림자 샘플링.
    float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(WorldPos));
#else
    // SSShadows가 아닐 경우, 즉 일반 섀도우맵일 때.
    // 월드 위치를 그림자맵 좌표로 바로 변환하여 그림자 샘플링.
    float4 shadowCoord = TransformWorldToShadowCoord(WorldPos);
#endif
    Light light = GetMainLight(shadowCoord);
    Direction = light.direction;
    Color = light.color;
    DistanceAtten = light.distanceAttenuation;
    ShadowAtten = light.shadowAttenuation;
#endif
}

// 얼굴 중심에 위치한 한 점에서 메인 라이트 실시간 그림자 감쇠를 샘플링(SDF 얼굴 매크로 dim용).
// 화면 공간 그림자 경로가 아니라 항상 섀도우맵 경로이므로 임의 월드 위치에 있는 얼굴 중심 샘플링에 적합.
// 0(가려진 상태) 또는 1(빛이 비추는 상태)를 반환. 얼굴 전체에 균일 적용하여 SDF 패턴을 해치지 않고 통짜로 그늘 처리.
// FaceCenterWS: 헬퍼가 머리 본 월드 위치(얼굴 중심에 위치한 한 점)을 _FaceCenter로 공급.
// LightPush: 얼굴 중심 점을 빛 쪽으로 미는 거리(머리 반경 정도). 안 밀면 자기 그림자로 인해 항상 가려진 상태가 되기 때문.
void FaceShadowAtten_float(
    float3 FaceCenterWS, float LightPush,
    out float ShadowAtten)
{
#ifdef SHADERGRAPH_PREVIEW
    ShadowAtten = 1;
#else
    // 빛 방향으로 얼굴 중심 점 위치를 밀어 머리 표면 밖으로 보내, 외부 물체만 가리도록 자기 그림자를 회피.
    float3 toLight = GetMainLight().direction;
    float3 samplePos = FaceCenterWS + toLight * LightPush;
    float4 shadowCoord = TransformWorldToShadowCoord(samplePos);
    ShadowAtten = GetMainLight(shadowCoord).shadowAttenuation;
#endif
}
#endif
