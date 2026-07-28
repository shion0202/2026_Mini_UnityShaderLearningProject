using System;
using System.Collections.Generic;
using Coffee.UIEffects;
using UnityEngine;

namespace UIEffectControl
{
    // UIEffect(coffee.ui-effect) 컴포넌트를 런타임 스크립트/이벤트에서 범용으로 제어하는 파사드.
    //
    // 세 가지를 한 컴포넌트로 다룬다.
    //  1) 프리셋 전환 : 중앙 레지스트리(Project Settings) 또는 로컬 목록에서 id/이름으로 불러오기(append 겹치기·초기화 지원)
    //  2) 트위너 제어 : UIEffectTweener 재생/역재생/토글/정지, duration·loop 조절
    //  3) 개별 속성   : 자주 쓰는 인텐시티/컬러 값을 UnityEvent(버튼·슬라이더) 친화 세터로 노출
    //
    // 여기 없는 나머지 속성(60여 개)은 Effect / Tweener 프로퍼티로 직접 접근한다.
    // 예) uiEffectController.Effect.gradationRotation = 90f;
    //
    // 확장: 모든 public 조작 메서드는 virtual 이므로 상속으로 커스터마이즈할 수 있다.
    // 규모가 커지면 프리셋/트위너/속성을 별도 컴포넌트로 분리하는 것을 고려.

    // 트위너 자신의 OnEnable 자동 재생보다 먼저 playOnEnable 을 통제하기 위해
    // 실행 순서를 앞당긴다(음수 = 더 일찍). 이래야 아래 OnEnable 이 트위너보다 먼저 돈다.
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(UIEffect))]
    public class UIEffectController : MonoBehaviour
    {
        #region Variables
        // 인스펙터에 등록하는 프리셋 항목. id 로 지목해서 불러온다.
        [Serializable]
        public class PresetEntry
        {
            [Tooltip("스크립트/이벤트에서 이 프리셋을 지목할 이름. 비워두면 preset 에셋 이름을 쓴다.")]
            public string id;

            [Tooltip("불러올 UIEffectPreset 에셋.")]
            public UIEffectPreset preset;

            [Tooltip("체크 시 현재 이펙트 위에 겹쳐 적용(None 이 아닌 필터만 덮어씀). 해제 시 전체 교체.")]
            public bool append;

            // id 가 비어 있으면 에셋 이름을 대체 키로 사용한다.
            public string ResolvedId => string.IsNullOrEmpty(id) ? (preset != null ? preset.name : null) : id;
        }

        [Header("References")]
        [Tooltip("제어할 UIEffect. 비워두면 같은 GameObject 에서 자동 획득한다.")]
        [SerializeField] private UIEffect m_Effect;

        [Tooltip("제어할 UIEffectTweener(선택). 비워두면 같은 GameObject 에서 자동 획득한다. 없으면 트윈 API 는 무시된다.")]
        [SerializeField] private UIEffectTweener m_Tweener;

        [Header("Presets")]
        [Tooltip("이 UI 전용(선택) 프리셋. 공용 프리셋은 Project Settings ▸ UI Effect ▸ Runtime Presets 에 등록해 이름으로 부른다.\n" +
                 "여기 등록한 id 는 같은 이름의 중앙 프리셋보다 우선 적용된다(로컬 오버라이드).")]
        [SerializeField] private List<PresetEntry> m_Presets = new List<PresetEntry>();

        [Header("Tweener")]
        [Tooltip("활성화 시 트위너 재생 방식. 기본 None = 트위너가 스스로 움직이지 않아 이펙트의 정적 값이 유지된다.\n" +
                 "이 값이 같은 오브젝트의 UIEffectTweener.playOnEnable 을 덮어써서, 자동 재생 여부를 컨트롤러가 통제한다.\n" +
                 "루프 샤인처럼 켜지자마자 재생이 필요한 경우에만 Forward/Reverse 로 바꾼다.")]
        [SerializeField] private UIEffectTweener.PlayOnEnable m_PlayOnEnable = UIEffectTweener.PlayOnEnable.None;

        [Tooltip("이 UI 전용(선택) 트위너 프리셋. 공용 프리셋은 UIEffectManager 의 트위너 프리셋 라이브러리에 등록해 이름으로 부른다.\n" +
                 "여기 등록한 이름은 같은 이름의 라이브러리 프리셋보다 우선 적용된다(로컬 오버라이드).")]
        [SerializeField] private List<UIEffectTweenerPreset> m_TweenerPresets = new List<UIEffectTweenerPreset>();
        #endregion

        #region Properties
        // 제어 대상 UIEffect. 필요 시 지연 획득한다. (고급 속성 직접 접근용)
        public UIEffect Effect => m_Effect != null ? m_Effect : m_Effect = GetComponent<UIEffect>();

        // 제어 대상 UIEffectTweener(없을 수 있음). (고급 제어 직접 접근용)
        public UIEffectTweener Tweener => m_Tweener != null ? m_Tweener : m_Tweener = GetComponent<UIEffectTweener>();

        // 등록된 프리셋 목록.
        public IReadOnlyList<PresetEntry> Presets => m_Presets;

        // 이 UI 전용으로 등록된 트위너 프리셋 목록.
        public IReadOnlyList<UIEffectTweenerPreset> TweenerPresets => m_TweenerPresets;
        #endregion

        #region Unity Methods
        private void Awake()
        {
            // 참조를 미리 채워 첫 호출 시 GetComponent 비용을 줄인다.
            if (m_Effect == null) m_Effect = GetComponent<UIEffect>();
            if (m_Tweener == null) m_Tweener = GetComponent<UIEffectTweener>();
        }

        private void OnEnable()
        {
            // 트위너의 기본 자동 재생(패키지 기본 Forward+Loop)이 이펙트의 정적 값을 덮어써
            // 의도치 않은 움직임을 만드는 것을 막는다. 기본 None → 명시적으로 Play* 를 부를 때만 동작.
            // DefaultExecutionOrder(-100) 덕분에 트위너 자신의 OnEnable 보다 먼저 실행된다.
            if (Tweener != null) Tweener.playOnEnable = m_PlayOnEnable;
        }

    #if UNITY_EDITOR
        private void Reset()
        {
            // 컴포넌트를 붙일 때 같은 오브젝트의 참조를 자동으로 채워준다.
            m_Effect = GetComponent<UIEffect>();
            m_Tweener = GetComponent<UIEffectTweener>();
        }
#endif
        #endregion

        #region Public Methods
        // ─────────────────────────────────────────────────────────────
        // 1) 프리셋 전환
        // ─────────────────────────────────────────────────────────────

        // id 로 프리셋을 불러온다. 로컬 목록(이 UI 전용 오버라이드)을 먼저 보고,
        // 없으면 중앙 레지스트리(Project Settings ▸ UI Effect ▸ Runtime Presets)에서 이름으로 조회한다.
        public virtual void LoadPreset(string id)
        {
            if (!HasEffect) return;

            if (!TryLoadPreset(id))
            {
                Debug.LogWarning($"[{nameof(UIEffectController)}] '{id}' 프리셋을 로컬 목록과 중앙 레지스트리 어디에서도 찾지 못했습니다.", this);
            }
        }

        // id 프리셋을 불러오고 성공 여부를 반환한다. 실패해도 경고하지 않으므로
        // 호출자(예: UIEffectManager)가 실패 처리를 직접 할 수 있다.
        //  id: 프리셋 id(로컬) 또는 중앙 레지스트리 이름.
        //  append: 겹쳐 적용 여부. null 이면 로컬 항목의 append 설정을 따르고, 중앙 프리셋은 전체 교체.
        public virtual bool TryLoadPreset(string id, bool? append = null)
        {
            if (Effect == null) return false;

            // 1) 이 UI 전용 로컬 프리셋이 있으면 우선 사용(인자 미지정 시 항목의 append 설정을 따른다)
            var entry = FindPreset(id);
            if (entry != null && entry.preset != null)
            {
                Effect.LoadPreset(entry.preset, append ?? entry.append);
                return true;
            }

            // 2) 없으면 중앙 레지스트리에서 이름으로 조회
            return TryLoadRegisteredPreset(id, append ?? false);
        }

        // id 프리셋이 로컬 목록 또는 중앙 레지스트리에 존재하는지 확인한다(적용하지 않음).
        public virtual bool HasPreset(string id)
        {
            var entry = FindPreset(id);
            if (entry != null && entry.preset != null) return true;

            return UIEffectProjectSettings.LoadPreset(id) != null;
        }

        // 등록 목록에서 인덱스로 프리셋을 불러온다. (항목의 append 설정을 따른다)
        public virtual void LoadPreset(int index)
        {
            if (index < 0 || index >= m_Presets.Count)
            {
                Debug.LogWarning($"[{nameof(UIEffectController)}] 프리셋 인덱스 {index} 가 범위를 벗어났습니다(0~{m_Presets.Count - 1}).", this);
                return;
            }

            var entry = m_Presets[index];
            LoadPresetAsset(entry.preset, entry.append);
        }

        // id 프리셋을 현재 이펙트 위에 강제로 겹쳐 적용한다(로컬→중앙 순서로 탐색, append 강제).
        public virtual void AppendPreset(string id)
        {
            if (!HasEffect) return;

            if (!TryLoadPreset(id, true))
            {
                Debug.LogWarning($"[{nameof(UIEffectController)}] '{id}' 프리셋을 로컬 목록과 중앙 레지스트리 어디에서도 찾지 못했습니다.", this);
            }
        }

        // 프리셋 에셋을 직접 불러온다. (목록 등록 없이 참조로 바로 적용)
        public virtual void LoadPresetAsset(UIEffectPreset preset, bool append = false)
        {
            if (!HasEffect) return;
            if (preset == null)
            {
                Debug.LogWarning($"[{nameof(UIEffectController)}] preset 이 비어 있어 불러올 수 없습니다.", this);
                return;
            }

            Effect.LoadPreset(preset, append);
        }

        // 프로젝트 설정에 등록된 프리셋을 이름으로 불러온다.
        // (Project Settings ▸ UI Effect ▸ Runtime Presets). 미등록 이름이면 경고한다.
        public virtual void LoadRegisteredPreset(string presetName, bool append = false)
        {
            if (!HasEffect) return;
            if (!TryLoadRegisteredPreset(presetName, append))
            {
                Debug.LogWarning($"[{nameof(UIEffectController)}] 중앙 레지스트리에 '{presetName}' 프리셋이 등록되어 있지 않습니다. (Project Settings ▸ UI Effect ▸ Runtime Presets)", this);
            }
        }

        // 이펙트 초기화 + 트윈 정지를 함께 수행한다. "이 UI 의 이펙트 상태를 통째로 끈다".
        // 트위너가 없으면 트윈 정지는 조용히 건너뛴다(경고 없음).
        public virtual void Clear()
        {
            ClearEffect();
            if (Tweener != null) Tweener.Stop();
        }

        // 이펙트만 기본값으로 초기화한다(도는 트윈은 그대로). 트윈까지 끄려면 Clear 를 쓴다.
        public virtual void ClearEffect()
        {
            if (!HasEffect) return;
            Effect.Clear();
        }

        // ─────────────────────────────────────────────────────────────
        // 2) 트위너 제어 (UIEffectTweener 가 없으면 조용히/경고 후 무시)
        // ─────────────────────────────────────────────────────────────

        // 트위너 프리셋 에셋을 직접 적용한다. 트위너가 없으면 false.
        public virtual bool ApplyTweenerPreset(UIEffectTweenerPreset preset)
        {
            if (preset == null || Tweener == null) return false;

            preset.ApplyTo(Tweener);

            // 프리셋이 이 UI 의 트윈 정책을 통째로 정하도록, OnEnable 에서 되돌리는 컨트롤러 값도 함께 맞춘다.
            // 이걸 빼먹으면 프리셋을 적용해도 다음 활성화 때 옛 설정으로 돌아간다.
            m_PlayOnEnable = preset.PlayOnEnable;
            return true;
        }

        // 이 UI 전용 목록에서 이름으로 트위너 프리셋을 찾아 적용한다. 없으면 false(경고 없음).
        // 공용 라이브러리 조회는 UIEffectManager 가 담당한다.
        public virtual bool TryLoadTweenerPreset(string id)
        {
            return ApplyTweenerPreset(FindTweenerPreset(id));
        }

        // 이 UI 전용 목록에 해당 이름의 트위너 프리셋이 있는지 확인한다.
        public virtual bool HasTweenerPreset(string id)
        {
            return FindTweenerPreset(id) != null;
        }

        // 정방향(0→1) 재생. resetTime=true 면 처음부터.
        public virtual void PlayForward(bool resetTime = true)
        {
            if (!HasTweener) return;
            Tweener.PlayForward(resetTime);
        }

        // 역방향(1→0) 재생. resetTime=true 면 끝에서부터.
        public virtual void PlayReverse(bool resetTime = true)
        {
            if (!HasTweener) return;
            Tweener.PlayReverse(resetTime);
        }

        // 현재 진행 방향의 반대로 재생(정방향↔역방향). 호버 인/아웃 같은 토글에 사용.
        public virtual void TogglePlay()
        {
            if (!HasTweener) return;
            if (Tweener.direction == UIEffectTweener.Direction.Forward)
            {
                Tweener.PlayReverse(false);
            }
            else
            {
                Tweener.PlayForward(false);
            }
        }

        // 트윈을 정지하고 시간을 리셋한다.
        public virtual void StopTween()
        {
            if (!HasTweener) return;
            Tweener.Stop();
        }

        // 트윈 일시정지/재개.
        public virtual void PauseTween(bool pause)
        {
            if (!HasTweener) return;
            Tweener.SetPause(pause);
        }

        // 트윈 재생 시간(초)을 바꾼다.
        public virtual void SetDuration(float seconds)
        {
            if (!HasTweener) return;
            Tweener.duration = seconds;
        }

        // 반복 여부를 켜고 끈다(true=Loop, false=Once). PingPong 등 세부 모드는 Tweener.wrapMode 로 직접 설정.
        public virtual void SetLoop(bool loop)
        {
            if (!HasTweener) return;
            Tweener.wrapMode = loop ? UIEffectTweener.WrapMode.Loop : UIEffectTweener.WrapMode.Once;
        }

        // ─────────────────────────────────────────────────────────────
        // 3) 개별 속성 — 자주 쓰는 값만 이벤트 친화 세터로 노출
        //    (그 외 전체 속성은 Effect.xxx 로 직접 접근)
        // ─────────────────────────────────────────────────────────────

        // 톤 필터 강도(0~1).
        public virtual void SetToneIntensity(float value)
        {
            if (!HasEffect) return;
            Effect.toneIntensity = value;
        }

        // 컬러 필터 강도(0~1).
        public virtual void SetColorIntensity(float value)
        {
            if (!HasEffect) return;
            Effect.colorIntensity = value;
        }

        // 컬러 필터 색.
        public virtual void SetColor(Color value)
        {
            if (!HasEffect) return;
            Effect.color = value;
        }

        // 컬러 필터 알파(0~1).
        public virtual void SetColorAlpha(float value)
        {
            if (!HasEffect) return;
            Effect.colorAlpha = value;
        }

        // 샘플링(블러/피셀레이트 등) 강도(0~1).
        public virtual void SetSamplingIntensity(float value)
        {
            if (!HasEffect) return;
            Effect.samplingIntensity = value;
        }

        // 트랜지션(디졸브/셔터 등) 진행도(0~1).
        public virtual void SetTransitionRate(float value)
        {
            if (!HasEffect) return;
            Effect.transitionRate = value;
        }

        // 그래디언트 오프셋(-1~1). 흐르는 그래디언트 연출에 사용.
        public virtual void SetGradationOffset(float value)
        {
            if (!HasEffect) return;
            Effect.gradationOffset = value;
        }

        // 그래디언트 회전(0~360도).
        public virtual void SetGradationRotation(float degrees)
        {
            if (!HasEffect) return;
            Effect.gradationRotation = degrees;
        }

        // 엣지 샤이니 진행도(0~1).
        public virtual void SetEdgeShinyRate(float value)
        {
            if (!HasEffect) return;
            Effect.edgeShinyRate = value;
        }
        #endregion

        #region Internal Utils
        private UIEffectTweenerPreset FindTweenerPreset(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            for (var i = 0; i < m_TweenerPresets.Count; i++)
            {
                var preset = m_TweenerPresets[i];
                if (preset != null && preset.ResolvedId == id) return preset;
            }

            return null;
        }

        private PresetEntry FindPreset(string id)
        {
            for (var i = 0; i < m_Presets.Count; i++)
            {
                var entry = m_Presets[i];
                if (entry != null && entry.ResolvedId == id) return entry;
            }

            return null;
        }

        // 중앙 레지스트리에서 이름으로 프리셋을 찾아 적용한다. 등록돼 있으면 true.
        // (호출 전 Effect null 가드는 호출자 책임)
        private bool TryLoadRegisteredPreset(string presetName, bool append)
        {
            switch (UIEffectProjectSettings.LoadPreset(presetName))
            {
                case UIEffectPreset v2:
                    Effect.LoadPreset(v2, append);
                    return true;
                case UIEffect v1: // v1(레거시) 프리셋 호환
                    Effect.LoadPreset(v1, append);
                    return true;
                default:
                    return false;
            }
        }

        private bool HasEffect
        {
            get
            {
                if (Effect != null) return true;
                Debug.LogWarning($"[{nameof(UIEffectController)}] UIEffect 가 없어 동작을 건너뜁니다.", this);
                return false;
            }
        }

        private bool HasTweener
        {
            get
            {
                if (Tweener != null) return true;
                Debug.LogWarning($"[{nameof(UIEffectController)}] UIEffectTweener 가 없어 트윈 동작을 건너뜁니다.", this);
                return false;
            }
        }
        #endregion
    }
}
