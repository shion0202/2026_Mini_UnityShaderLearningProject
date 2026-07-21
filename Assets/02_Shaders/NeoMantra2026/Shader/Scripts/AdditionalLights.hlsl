#ifndef NEOMANTRA_ADDITIONALLIGHTS_INCLUDED
#define NEOMANTRA_ADDITIONALLIGHTS_INCLUDED

#ifndef SHADERGRAPH_PREVIEW
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
#endif

// 추가 라이트(포인트·스폿) 디퓨즈 + 스페큘러 합산.
// 메인 라이트와 같은 셀 램프(경계/선명도/AA) 및 셀 스페큘러(광택도/임계값/부드러움)를 라이트별로 적용.
// 한 루프에서 둘 다 계산하며 라이트를 1번만 조회하므로 효율적.
// 스페큘러는 라이트별 light.color를 곱하므로, 라이트가 없으면 자동으로 0이 되어 별도 presence 게이트가 불필요.
// LIGHT_LOOP_BEGIN/END로 Forward·Forward+ 모두 대응. 라이트가 0개면 루프도 0회라 무비용.
// Diffuse: 순수 들어오는 빛(알베도 미포함) → 그래프에서 알베도 곱 후 가산
// Specular: 하이라이트(알베도 미포함) → 그래프에서 색×강도×마스크 후 오버레이 가산
void AdditionalLightsToon_float(
    float3 WorldPos, float3 WorldNormal, float3 WorldView, float2 ScreenUV,
    float Threshold, float Hardness, float EdgeAA,
    float SpecThreshold, float SpecGloss, float SpecSoftness,
    out float3 Diffuse, out float3 Specular)
{
    Diffuse = float3(0, 0, 0);
    Specular = float3(0, 0, 0);
#ifndef SHADERGRAPH_PREVIEW
    // Forward+ 클러스터 순회에 필요한 최소 InputData(positionWS, normalizedScreenSpaceUV).
    // LIGHT_LOOP_BEGIN 매크로가 이름을 그대로 참조하기 때문에 변수명은 반드시 inputData를 사용.
    InputData inputData = (InputData)0;
    inputData.positionWS = WorldPos;
    inputData.normalizedScreenSpaceUV = ScreenUV;

    // View Direction이 단위가 아닐 수 있으므로 하프벡터용 정규화
    float3 V = normalize(WorldView);

    // 엣지 AA용 노멀 변화량: 라이트 루프는 픽셀마다 반복 횟수가 달라 루프 안에서 fwidth(gradient 명령)를 쓰면 미정의 동작 경고가 발생.
    // 미분을 루프 밖에서 한 번만 구하고, 루프 안에서는 이 값으로 근사.
    float normalAA = length(fwidth(WorldNormal));

    uint pixelLightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(pixelLightCount)
        // _ADDITIONAL_LIGHT_SHADOWS 활성 시 실시간 그림자 감쇠를 포함하는 3인자 버전
        // 2인자 버전은 그림자 감쇠를 제외하고 거리 감쇠만 주며, 3번째 인자는 ShadowMask이기에 전부 1이면 베이크 차폐가 없다.
        Light light = GetAdditionalLight(lightIndex, WorldPos, half4(1, 1, 1, 1));
        float3 radiance = light.color * (light.distanceAttenuation * light.shadowAttenuation);

        // 디퓨즈 셀 밴드
        // 메인 그림자와 동일한 폭 규약((1-선명도)×0.5)을 적용하며, AA는 (fwidth 하한)×0.5.
        float halfLambert = dot(WorldNormal, light.direction) * 0.5 + 0.5;
        float dw = (1.0 - Hardness) * 0.5;
        // fwidth(halfLambert) ≈ 0.5 × 노멀 변화량 (L은 픽셀 간 거의 상수)
        dw = max(dw, normalAA * 0.5 * EdgeAA * 0.5);
        float dband = smoothstep(Threshold - dw, Threshold + dw, halfLambert);
        Diffuse += radiance * dband;

        // 스페큘러 셀 밴드
        // 메인 스페큘러와 동일하게 pow(NdotH, 광택도) → smoothstep(임계값, 임계값+부드러움) 순서.
        // 디퓨즈는 양쪽 폭(dw)에 ×0.5를 하므로 ×1.0이 되지만, 스페큘러의 경우 한쪽 경계(sw)만 있으므로 AA에 ×2를 하여 fwidth 하한을 보정.
        float3 H = normalize(light.direction + V);
        float ndoth = saturate(dot(WorldNormal, H));
        float powered = pow(ndoth, SpecGloss);
        // fwidth(powered)를 연쇄법칙으로 근사: d(pow)/d(ndoth) = 광택도×pow(ndoth, 광택도-1), 여기에 노멀 변화량을 곱한다.
        // 지수는 max(…, 0)으로 방어하며, 광택도<1일 때 pow(0, 음수)=inf 방지.
        float dpow = SpecGloss * pow(ndoth, max(SpecGloss - 1.0, 0.0));
        float sw = max(SpecSoftness, dpow * normalAA * EdgeAA);
        float sband = smoothstep(SpecThreshold, SpecThreshold + sw, powered);
        Specular += radiance * sband;
    LIGHT_LOOP_END
#endif
}
#endif
