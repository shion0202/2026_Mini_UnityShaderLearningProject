using Coffee.UIEffects;
using UnityEngine;

[RequireComponent(typeof(UIEffectTweener))]
public class UIEffectStateTweener : MonoBehaviour
{
    [SerializeField] private UIEffectTweenerPreset m_Normal;
    [SerializeField] private UIEffectTweenerPreset m_Hover;
    [SerializeField] private UIEffectTweenerPreset m_Pressed;

    private UIEffectTweener _tweener;
    private UIEffectTweener tweener =>
        _tweener != null ? _tweener : _tweener = GetComponent<UIEffectTweener>();

    // EventTrigger나 Button 이벤트에 연결
    public void PlayNormal() => Play(m_Normal);
    public void PlayHover() => Play(m_Hover);
    public void PlayPressed() => Play(m_Pressed);

    private void Play(UIEffectTweenerPreset preset)
    {
        if (preset == null) return;
        preset.ApplyTo(tweener);
        tweener.PlayForward(resetTime: true);   // 0에서 다시 재생
    }
}
