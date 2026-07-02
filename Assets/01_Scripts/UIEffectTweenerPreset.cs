using Coffee.UIEffects;
using UnityEngine;

[CreateAssetMenu(menuName = "UIEffect/Tweener Preset")]
public class UIEffectTweenerPreset : ScriptableObject
{
    public UIEffectTweener.CullingMask cullingMask = UIEffectTweener.CullingMask.Transition;
    public UIEffectTweener.Direction direction = UIEffectTweener.Direction.Forward;
    public UIEffectTweener.WrapMode wrapMode = UIEffectTweener.WrapMode.Once;
    public UIEffectTweener.UpdateMode updateMode = UIEffectTweener.UpdateMode.Unscaled;

    public AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public bool separateReverseCurve;
    public AnimationCurve reverseCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Range(0f, 10f)] public float delay;
    [Range(0.05f, 10f)] public float duration = 0.25f;
    [Range(0f, 10f)] public float interval;

    public void ApplyTo(UIEffectTweener t)
    {
        t.cullingMask = cullingMask;
        t.direction = direction;
        t.wrapMode = wrapMode;
        t.updateMode = updateMode;
        t.curve = curve;
        t.separateReverseCurve = separateReverseCurve;
        t.reverseCurve = reverseCurve;
        t.delay = delay;
        t.duration = duration;
        t.interval = interval;
    }
}
