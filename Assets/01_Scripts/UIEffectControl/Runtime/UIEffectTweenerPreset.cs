using Coffee.UIEffects;
using UnityEngine;

namespace UIEffectControl
{
    // UIEffectTweener 설정을 이름으로 재사용하기 위한 프리셋 에셋.
    // UIEffect 는 패키지가 프리셋을 지원하지만 트위너는 지원하지 않아 직접 만든 것이다.
    //
    // 만드는 법
    //  1) UI 에 UIEffectTweener 를 붙이고 인스펙터에서 원하는 느낌이 나올 때까지 값을 조정한다.
    //  2) 같은 오브젝트의 UIEffectController 인스펙터 하단 "트위너 프리셋 도구" 에서
    //     [현재 트위너 값 → 새 프리셋 저장] 을 누른다. 값을 손으로 옮겨적을 필요는 없다.
    [CreateAssetMenu(fileName = "TweenerPreset", menuName = "UI Effect/Tweener Preset")]
    public class UIEffectTweenerPreset : ScriptableObject
    {
        // 프리셋을 적용한 직후의 재생 방식.
        public enum PlayPolicy
        {
            None,    // 설정만 바꾸고 재생하지 않는다(현재 값 보존).
            Forward, // 정방향(0→1) 재생.
            Reverse  // 역방향(1→0) 재생.
        }

        [Header("Identity")]
        [Tooltip("이 프리셋을 지목할 이름. 비워두면 에셋 파일 이름을 쓴다.")]
        [SerializeField] private string m_PresetName;

        [Header("Tween")]
        [Tooltip("트윈이 건드릴 속성 범위. 여기 포함된 필터의 강도/진행도만 0→1 로 구동된다.")]
        [SerializeField]
        private UIEffectTweener.CullingMask m_CullingMask =
            UIEffectTweener.CullingMask.Tone | UIEffectTweener.CullingMask.Color |
            UIEffectTweener.CullingMask.Sampling | UIEffectTweener.CullingMask.Transition |
            UIEffectTweener.CullingMask.GradiationOffset | UIEffectTweener.CullingMask.GradiationRotation |
            UIEffectTweener.CullingMask.EdgeShiny;

        [Tooltip("트윈의 기본 진행 방향.")]
        [SerializeField] private UIEffectTweener.Direction m_Direction = UIEffectTweener.Direction.Forward;

        [Tooltip("트윈 재생 시간(초).")]
        [Range(0.05f, 10f)]
        [SerializeField] private float m_Duration = 1f;

        [Tooltip("재생 시작 전 지연(초).")]
        [Range(0f, 10f)]
        [SerializeField] private float m_Delay;

        [Tooltip("반복 사이의 간격(초).")]
        [Range(0f, 10f)]
        [SerializeField] private float m_Interval;

        [Tooltip("반복 방식. Once=한 번, Loop=반복, PingPong=왕복.")]
        [SerializeField] private UIEffectTweener.WrapMode m_WrapMode = UIEffectTweener.WrapMode.Loop;

        [Tooltip("시간 갱신 방식. Unscaled 는 타임스케일 영향을 받지 않고, Manual 은 직접 갱신해야 한다.")]
        [SerializeField] private UIEffectTweener.UpdateMode m_UpdateMode = UIEffectTweener.UpdateMode.Normal;

        [Tooltip("진행도 곡선. 이징(가속/감속) 느낌을 여기서 만든다.")]
        [SerializeField] private AnimationCurve m_Curve = AnimationCurve.Linear(0, 0, 1, 1);

        [Tooltip("체크 시 역방향 재생에 별도 곡선을 쓴다.")]
        [SerializeField] private bool m_SeparateReverseCurve;

        [Tooltip("역방향 전용 곡선. 위 옵션이 켜져 있을 때만 쓰인다.")]
        [SerializeField] private AnimationCurve m_ReverseCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Header("Play Policy")]
        [Tooltip("프리셋을 적용한 직후의 재생 방식. 팝업 등장처럼 바로 움직여야 하면 Forward, 설정만 바꿀 거면 None.")]
        [SerializeField] private PlayPolicy m_PlayOnApply = PlayPolicy.None;

        [Tooltip("체크 시 적용 직후 재생을 처음(또는 끝)부터 시작한다.")]
        [SerializeField] private bool m_ResetTimeOnApply = true;

        [Tooltip("오브젝트가 활성화될 때의 자동 재생 방식. None 이면 이펙트의 정적 값이 그대로 유지된다.\n" +
                 "이 값은 프리셋 적용 시 컨트롤러의 Play On Enable 설정도 함께 덮어쓴다.")]
        [SerializeField] private UIEffectTweener.PlayOnEnable m_PlayOnEnable = UIEffectTweener.PlayOnEnable.None;

        [Tooltip("체크 시 활성화될 때마다 재생 시간을 리셋한다.")]
        [SerializeField] private bool m_ResetTimeOnEnable = true;

        // 이 프리셋을 지목하는 이름. 이름 필드가 비어 있으면 에셋 이름을 대체 키로 쓴다.
        public string ResolvedId => string.IsNullOrEmpty(m_PresetName) ? name : m_PresetName;

        // 활성화 시 자동 재생 방식. 컨트롤러가 자기 설정을 맞추는 데 쓴다.
        public UIEffectTweener.PlayOnEnable PlayOnEnable => m_PlayOnEnable;

        // 프리셋 값을 트위너에 적용하고, 재생 정책에 따라 재생까지 처리한다.
        public void ApplyTo(UIEffectTweener tweener)
        {
            if (tweener == null) return;

            tweener.cullingMask = m_CullingMask;
            tweener.direction = m_Direction;
            tweener.duration = m_Duration;
            tweener.delay = m_Delay;
            tweener.interval = m_Interval;
            tweener.wrapMode = m_WrapMode;
            tweener.updateMode = m_UpdateMode;
            tweener.separateReverseCurve = m_SeparateReverseCurve;
            tweener.playOnEnable = m_PlayOnEnable;
            tweener.resetTimeOnEnable = m_ResetTimeOnEnable;

            // 곡선은 참조를 그대로 넘기면 런타임 조작이 에셋 원본을 오염시키므로 복제해서 넘긴다.
            tweener.curve = CloneCurve(m_Curve);
            tweener.reverseCurve = CloneCurve(m_ReverseCurve);

            switch (m_PlayOnApply)
            {
                case PlayPolicy.Forward:
                    tweener.PlayForward(m_ResetTimeOnApply);
                    break;
                case PlayPolicy.Reverse:
                    tweener.PlayReverse(m_ResetTimeOnApply);
                    break;
                // None 은 정지 처리하지 않는다. Stop() 하면 rate 가 0 이 되어 이펙트가 꺼져 보인다.
            }
        }

        // 트위너의 현재 값을 이 프리셋으로 흡수한다. 에디터에서 값을 맞춘 뒤 굽는 용도.
        // 재생 정책(Play On Apply / Reset Time On Apply)은 트위너에 없는 개념이라 건드리지 않는다.
        public void CaptureFrom(UIEffectTweener tweener)
        {
            if (tweener == null) return;

            m_CullingMask = tweener.cullingMask;
            m_Direction = tweener.direction;
            m_Duration = tweener.duration;
            m_Delay = tweener.delay;
            m_Interval = tweener.interval;
            m_WrapMode = tweener.wrapMode;
            m_UpdateMode = tweener.updateMode;
            m_SeparateReverseCurve = tweener.separateReverseCurve;
            m_PlayOnEnable = tweener.playOnEnable;
            m_ResetTimeOnEnable = tweener.resetTimeOnEnable;
            m_Curve = CloneCurve(tweener.curve);
            m_ReverseCurve = CloneCurve(tweener.reverseCurve);
        }

        private static AnimationCurve CloneCurve(AnimationCurve source)
        {
            if (source == null) return AnimationCurve.Linear(0, 0, 1, 1);

            return new AnimationCurve(source.keys)
            {
                preWrapMode = source.preWrapMode,
                postWrapMode = source.postWrapMode
            };
        }
    }
}
