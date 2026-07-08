#ifndef NEOMANTRA_ADDITIONALLIGHTS_INCLUDED
#define NEOMANTRA_ADDITIONALLIGHTS_INCLUDED

#ifndef SHADERGRAPH_PREVIEW
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
#endif

// 추가 라이트(포인트·스폿) 디퓨즈 + 스페큘러 합산.
// 메인 라이트와 같은 셀 램프(경계/선명도/AA)·셀 스페큘러(광택도/임계값/부드러움)를 라이트별로 적용.
// 한 루프에서 둘 다 계산(라이트 1회 조회로 효율적). 출력 분리:
//   Diffuse  = 순수 들어오는 빛(알베도 미포함) → 그래프에서 알베도 곱 후 가산
//   Specular = 하이라이트(알베도 미포함) → 그래프에서 색×강도×마스크 후 오버레이 가산
// 스페큘러는 라이트별 light.color를 곱하므로 라이트 없으면 자동 0(별도 presence 게이트 불필요).
// LIGHT_LOOP_BEGIN/END로 Forward·Forward+ 모두 대응. 라이트 0개면 루프 0회라 무비용.
void AdditionalLightsToon_float(
    float3 WorldPos, float3 WorldNormal, float3 WorldView, float2 ScreenUV,
    float Threshold, float Hardness, float EdgeAA,
    float SpecThreshold, float SpecGloss, float SpecSoftness,
    out float3 Diffuse, out float3 Specular)
{
    Diffuse = float3(0, 0, 0);
    Specular = float3(0, 0, 0);
#ifndef SHADERGRAPH_PREVIEW
    // Forward+ 클러스터 순회에 필요한 최소 InputData(스크린 UV·월드 위치).
    // 변수명은 반드시 inputData — LIGHT_LOOP_BEGIN 매크로가 이 이름을 그대로 참조한다.
    InputData inputData = (InputData)0;
    inputData.positionWS = WorldPos;
    inputData.normalizedScreenSpaceUV = ScreenUV;

    float3 V = normalize(WorldView); // 하프벡터용 정규화(View Direction이 단위가 아닐 수 있음)

    uint pixelLightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(pixelLightCount)
        // 3인자 버전: _ADDITIONAL_LIGHT_SHADOWS 활성 시 실시간 그림자 감쇠 포함
        Light light = GetAdditionalLight(lightIndex, WorldPos, half4(1, 1, 1, 1));
        float3 radiance = light.color * (light.distanceAttenuation * light.shadowAttenuation);

        // 디퓨즈 셀 밴드 (메인 그림자와 동일 폭 규약: (1-선명도)×0.5, AA는 fwidth 하한 ×0.5)
        float halfLambert = dot(WorldNormal, light.direction) * 0.5 + 0.5;
        float dw = (1.0 - Hardness) * 0.5;
        dw = max(dw, fwidth(halfLambert) * EdgeAA * 0.5);
        float dband = smoothstep(Threshold - dw, Threshold + dw, halfLambert);
        Diffuse += radiance * dband;

        // 스페큘러 셀 밴드 (메인 스페큘러와 동일: pow(NdotH, 광택도) → smoothstep(임계값, 임계값+부드러움))
        // "시작+폭" 한쪽 경계라 AA는 ×2 규칙(디퓨즈 ±의 ×0.5 대비 2배 = ×1.0).
        float3 H = normalize(light.direction + V);
        float ndoth = saturate(dot(WorldNormal, H));
        float powered = pow(ndoth, SpecGloss);
        float sw = max(SpecSoftness, fwidth(powered) * EdgeAA);
        float sband = smoothstep(SpecThreshold, SpecThreshold + sw, powered);
        Specular += radiance * sband;
    LIGHT_LOOP_END
#endif
}
#endif
