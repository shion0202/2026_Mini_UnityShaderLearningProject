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

// 얼굴 중심 1점에서 메인 라이트 실시간 그림자 감쇠를 샘플(SDF 얼굴 매크로 dim용).
// 화면공간 그림자 경로가 아니라 항상 셰도우맵 경로 — 임의 월드 위치(얼굴 중심) 샘플에 적합.
// 반환 0=가려짐 / 1=빛. 얼굴 전체에 균일 적용하면 SDF 패턴을 해치지 않고 통짜로 그늘 처리.
// FaceCenterWS = 헬퍼가 머리 본 월드 위치를 _FaceCenter로 공급(머리 안쪽 점).
// LightPush = 그 점을 빛 쪽으로 미는 거리(머리 반경 정도, ~0.15). 안 밀면 자기그림자로 항상 가려짐 판정.
void FaceShadowAtten_float(float3 FaceCenterWS, float LightPush, out float ShadowAtten)
{
#ifdef SHADERGRAPH_PREVIEW
    ShadowAtten = 1;
#else
    // 빛 방향으로 밀어 머리 표면 밖으로 → 외부 물체만 가리게(자기그림자 회피)
    float3 toLight = GetMainLight().direction;
    float3 samplePos = FaceCenterWS + toLight * LightPush;
    float4 shadowCoord = TransformWorldToShadowCoord(samplePos);
    ShadowAtten = GetMainLight(shadowCoord).shadowAttenuation;
#endif
}
#endif
