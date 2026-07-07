using System.Collections;
using UnityEngine;
using Coffee.UIEffects;

public class CharacterStateEffect : MonoBehaviour
{
    public enum CharacterState { Normal, Trauma, Awakening }

    [SerializeField] private UIEffect targetEffect;

    [SerializeField] private string normalPresetName = "Normal";
    [SerializeField] private string traumaPresetName = "Trauma";
    [SerializeField] private string awakeningPresetName = "Awakening";

    [Header("Transition")]
    [Tooltip("페이드 인/아웃에 걸리는 시간(초).")]
    [SerializeField, Range(0.01f, 5f)] private float transitionDuration = 0.3f;

    [Tooltip("0→1 보간 커브. (아웃은 이 커브를 역방향으로 사용)")]
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("Time.timeScale의 영향을 받지 않게 하려면 체크. (일시정지 중에도 동작)")]
    [SerializeField] private bool useUnscaledTime = true;

    [Tooltip("SetRate로 애니메이션할 필터. 프리셋이 쓰는 필터와 맞춰줄 것.")]
    [SerializeField]
    private UIEffectTweener.CullingMask cullingMask =
        UIEffectTweener.CullingMask.Tone | UIEffectTweener.CullingMask.Color |
        UIEffectTweener.CullingMask.Sampling | UIEffectTweener.CullingMask.Transition |
        UIEffectTweener.CullingMask.GradiationOffset | UIEffectTweener.CullingMask.GradiationRotation |
        UIEffectTweener.CullingMask.EdgeShiny;

    private CharacterState currentState = CharacterState.Normal;
    private Coroutine transitionRoutine;

    // 현재 표시 중인 이펙트 강도(0~1). 중간에 전환이 끊겨도 여기서부터 이어받는다.
    private float rate;

    // ↓ 버튼 OnClick()에 바로 연결할 테스트용 메서드 (파라미터 없음)
    public void SetNormal() => SetState(CharacterState.Normal);
    public void SetTrauma() => SetState(CharacterState.Trauma);
    public void SetAwakening() => SetState(CharacterState.Awakening);

    public void SetState(CharacterState newState)
    {
        if (currentState == newState) return;
        currentState = newState;

        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        transitionRoutine = StartCoroutine(TransitionTo(newState));
    }

    private IEnumerator TransitionTo(CharacterState newState)
    {
        // 1) 현재 이펙트를 서서히 아웃 (현재 rate → 0)
        if (rate > 0f)
            yield return Fade(rate, 0f);

        // 2) 이펙트가 꺼진 상태에서 프리셋 교체
        string presetName = newState switch
        {
            CharacterState.Trauma => traumaPresetName,
            CharacterState.Awakening => awakeningPresetName,
            _ => normalPresetName
        };
        targetEffect.LoadPreset(presetName);

        // 3) Normal이 아니면 새 이펙트를 서서히 인 (0 → 1)
        if (newState != CharacterState.Normal)
            yield return Fade(0f, 1f);

        transitionRoutine = null;
    }

    private IEnumerator Fade(float from, float to)
    {
        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float k = transitionCurve.Evaluate(Mathf.Clamp01(elapsed / transitionDuration));
            rate = Mathf.Lerp(from, to, k);
            targetEffect.SetRate(rate, cullingMask);
            yield return null;
        }

        rate = to;
        targetEffect.SetRate(rate, cullingMask);
    }
}
