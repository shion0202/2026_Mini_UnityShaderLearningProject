using System;
using System.Collections.Generic;
using Coffee.UIEffects;
using UnityEngine;

namespace UIEffectControl
{
    // UI 이펙트 적용을 실제로 수행하는 실행기(IUIEffectService 구현).
    //
    // UI 목록은 이 매니저가 소유하지 않는다. 팀의 UI Manager 가 UI 의 생성/삭제/수명과 목록을
    // 모두 쥐고 있고, "A UI 에 B 프리셋" 요청이 오면 자기 목록에서 A 오브젝트를 찾아
    // 이 매니저의 API 에 그 GameObject 를 넘긴다. 매니저는 넘어온 오브젝트 하위에서
    // UIEffectController 를 찾아 프리셋을 걸 뿐이다.
    //
    // 이렇게 UI 목록을 이중으로 들지 않으므로, 씬 전환/런타임 생성으로 인한 참조 staleness 가 없다.
    // 매니저가 들고 있는 상태는 공용 트위너 프리셋 라이브러리(에셋 참조)뿐이다.
    [DisallowMultipleComponent]
    public class UIEffectManager : MonoBehaviour, IUIEffectService
    {
        #region Variables
        [Header("Tweener Presets")]
        [Tooltip("이름으로 조회할 공용 트위너 프리셋 라이브러리. UI 전용 프리셋은 각 컨트롤러에 등록한다.")]
        [SerializeField] private UIEffectTweenerPresetLibrary m_TweenerLibrary;

        [Header("Lifetime")]
        [Tooltip("체크 시 씬이 바뀌어도 매니저를 유지한다. 루트 오브젝트일 때만 적용된다.")]
        [SerializeField] private bool m_DontDestroyOnLoad = true;

        [Header("Debug")]
        [Tooltip("체크 시 프리셋 적용 결과를 콘솔에 남긴다.")]
        [SerializeField] private bool m_VerboseLog;

        private static UIEffectManager s_Instance;
        private static bool s_IsQuitting;

        // GameObject → 컨트롤러 해석에 재사용하는 버퍼. 매 호출 GC 를 피한다(재진입 없음).
        private readonly List<UIEffectController> m_Buffer = new List<UIEffectController>();
        #endregion

        #region Properties
        /// 씬에 배치된 매니저 인스턴스. 없으면 경고 후 null 을 반환한다(자동 생성하지 않는다 —
        /// 트위너 라이브러리 같은 필수 설정이 비어 있는 빈 매니저를 만들어봐야 쓸모가 없기 때문).
        public static UIEffectManager Instance
        {
            get
            {
                if (s_Instance != null) return s_Instance;
                if (s_IsQuitting) return null;

                // 비활성 오브젝트에 붙어 있어도 찾을 수 있게 Include 로 검색한다.
                s_Instance = FindFirstObjectByType<UIEffectManager>(FindObjectsInactive.Include);
                if (s_Instance == null)
                {
                    Debug.LogWarning($"[{nameof(UIEffectManager)}] 씬에 매니저가 없습니다. " +
                                     "매니저를 배치하고 트위너 프리셋 라이브러리를 지정하세요.");
                }

                return s_Instance;
            }
        }

        /// 매니저가 존재하는지(또는 이미 참조되었는지). 조회를 유발하지 않는다.
        public static bool Exists => s_Instance != null;
        #endregion

        #region Unity Methods
        private void Awake()
        {
            // 중복 인스턴스 정리: 먼저 자리 잡은 쪽을 살린다.
            if (s_Instance != null && s_Instance != this)
            {
                Debug.LogWarning($"[{nameof(UIEffectManager)}] 이미 인스턴스가 있어 중복된 매니저를 제거합니다.", this);
                Destroy(gameObject);
                return;
            }

            s_Instance = this;

            // DontDestroyOnLoad 는 루트 오브젝트에만 적용된다.
            if (m_DontDestroyOnLoad)
            {
                if (transform.parent == null)
                {
                    DontDestroyOnLoad(gameObject);
                }
                else
                {
                    Debug.LogWarning($"[{nameof(UIEffectManager)}] 자식 오브젝트라 DontDestroyOnLoad 를 적용하지 못했습니다. 루트로 옮기세요.", this);
                }
            }
        }

        private void OnDestroy()
        {
            if (s_Instance == this) s_Instance = null;
        }

        private void OnApplicationQuit()
        {
            // 종료 중 Instance 접근이 새 참조를 시도하지 않도록 막는다.
            s_IsQuitting = true;
        }

        // Enter Play Mode Options 로 도메인 리로드를 끄면 static 이 플레이 세션 사이에 유지된다.
        // 그러면 s_IsQuitting 이 이전 세션의 true 로 남아 Instance 가 계속 null 을 반환하므로,
        // 플레이 시작마다 static 을 초기화한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Instance = null;
            s_IsQuitting = false;
        }
        #endregion

        #region 프리셋 요청 API (IUIEffectService)
        public bool ApplyPreset(GameObject uiRoot, string presetName, bool append = false)
        {
            if (!TryResolve(uiRoot, warnIfEmpty: true)) return false;

            var applied = 0;
            for (var i = 0; i < m_Buffer.Count; i++)
            {
                if (m_Buffer[i].TryLoadPreset(presetName, append)) applied++;
            }

            if (applied == 0)
            {
                Debug.LogWarning($"[{nameof(UIEffectManager)}] '{uiRoot.name}' 에 '{presetName}' 프리셋을 적용하지 못했습니다. " +
                                 "프리셋 이름 또는 등록 여부를 확인하세요. (Project Settings ▸ UI Effect ▸ Runtime Presets)", uiRoot);
                return false;
            }

            if (m_VerboseLog)
            {
                Debug.Log($"[{nameof(UIEffectManager)}] '{uiRoot.name}' {applied}개에 '{presetName}' 적용(append={append}).", uiRoot);
            }

            return true;
        }

        public bool ApplyTweenerPreset(GameObject uiRoot, string presetName)
        {
            if (!TryResolve(uiRoot, warnIfEmpty: true)) return false;

            var applied = 0;
            for (var i = 0; i < m_Buffer.Count; i++)
            {
                var controller = m_Buffer[i];

                // 1) 이 UI 전용 프리셋이 있으면 우선 사용
                if (controller.TryLoadTweenerPreset(presetName))
                {
                    applied++;
                    continue;
                }

                // 2) 없으면 공용 라이브러리에서 이름으로 조회
                if (m_TweenerLibrary != null && m_TweenerLibrary.TryGet(presetName, out var preset)
                                             && controller.ApplyTweenerPreset(preset))
                {
                    applied++;
                }
            }

            if (applied == 0)
            {
                Debug.LogWarning($"[{nameof(UIEffectManager)}] '{uiRoot.name}' 에 트위너 프리셋 '{presetName}' 을 적용하지 못했습니다. " +
                                 "프리셋 이름, 라이브러리 등록 여부, UIEffectTweener 컴포넌트 유무를 확인하세요.", uiRoot);
                return false;
            }

            if (m_VerboseLog)
            {
                Debug.Log($"[{nameof(UIEffectManager)}] '{uiRoot.name}' {applied}개에 트위너 프리셋 '{presetName}' 적용.", uiRoot);
            }

            return true;
        }

        // 둘 다 끄기: 이펙트를 기본값으로 되돌리고 트윈도 정지한다. "이 UI 를 통째로 끈다" 는 기본 경로.
        // 이펙트만/트윈만 끄려면 ClearEffect / StopTween 을 쓴다.
        public bool Clear(GameObject uiRoot)
        {
            if (!TryResolve(uiRoot, warnIfEmpty: true)) return false;

            for (var i = 0; i < m_Buffer.Count; i++)
            {
                m_Buffer[i].Clear();
            }

            return true;
        }

        // 이펙트만 끄기: 이펙트 값만 기본값으로 되돌린다(도는 트윈은 그대로).
        public bool ClearEffect(GameObject uiRoot)
        {
            if (!TryResolve(uiRoot, warnIfEmpty: true)) return false;

            for (var i = 0; i < m_Buffer.Count; i++)
            {
                m_Buffer[i].ClearEffect();
            }

            return true;
        }

        // ─────────────────────────────────────────────────────────────
        // 트윈 재생 제어 (컨트롤러 재생 메서드로 패스스루)
        // ─────────────────────────────────────────────────────────────

        public bool PlayForward(GameObject uiRoot, bool resetTime = true)
        {
            return DriveTweeners(uiRoot, "정방향 재생", controller => controller.PlayForward(resetTime));
        }

        public bool PlayReverse(GameObject uiRoot, bool resetTime = true)
        {
            return DriveTweeners(uiRoot, "역방향 재생", controller => controller.PlayReverse(resetTime));
        }

        public bool TogglePlay(GameObject uiRoot)
        {
            return DriveTweeners(uiRoot, "토글 재생", controller => controller.TogglePlay());
        }

        public bool StopTween(GameObject uiRoot)
        {
            return DriveTweeners(uiRoot, "정지", controller => controller.StopTween());
        }

        public bool PauseTween(GameObject uiRoot, bool pause)
        {
            return DriveTweeners(uiRoot, pause ? "일시정지" : "재개", controller => controller.PauseTween(pause));
        }

        public bool HasPreset(GameObject uiRoot, string presetName)
        {
            if (!TryResolve(uiRoot, warnIfEmpty: false)) return false;

            for (var i = 0; i < m_Buffer.Count; i++)
            {
                if (m_Buffer[i].HasPreset(presetName)) return true;
            }

            return false;
        }

        public bool HasTweenerPreset(GameObject uiRoot, string presetName)
        {
            // 공용 라이브러리에 있으면 컨트롤러 조회 없이 바로 true.
            if (HasTweenerPreset(presetName)) return true;
            if (!TryResolve(uiRoot, warnIfEmpty: false)) return false;

            for (var i = 0; i < m_Buffer.Count; i++)
            {
                if (m_Buffer[i].HasTweenerPreset(presetName)) return true;
            }

            return false;
        }

        // ─────────────────────────────────────────────────────────────
        // 이름만으로 하는 존재 확인 (UI 오브젝트가 필요 없는 전역 조회)
        // ─────────────────────────────────────────────────────────────

        /// 프리셋 이름이 중앙 레지스트리에 등록돼 있는지 확인한다.
        public bool HasPreset(string presetName)
        {
            return UIEffectProjectSettings.LoadPreset(presetName) != null;
        }

        /// 트위너 프리셋 이름이 공용 라이브러리에 등록돼 있는지 확인한다.
        public bool HasTweenerPreset(string presetName)
        {
            return m_TweenerLibrary != null && m_TweenerLibrary.Contains(presetName);
        }
        #endregion

        #region Internal Utils
        // uiRoot 하위에서 트위너를 가진 컨트롤러에만 재생 동작을 수행한다.
        // 트위너 없는 컨트롤러는 건너뛰어(컨트롤러 쪽 경고를 피함) 하나도 못 구동하면 false.
        private bool DriveTweeners(GameObject uiRoot, string action, Action<UIEffectController> op)
        {
            if (!TryResolve(uiRoot, warnIfEmpty: true)) return false;

            var driven = 0;
            for (var i = 0; i < m_Buffer.Count; i++)
            {
                var controller = m_Buffer[i];
                if (controller.Tweener == null) continue;

                op(controller);
                driven++;
            }

            if (driven == 0)
            {
                Debug.LogWarning($"[{nameof(UIEffectManager)}] '{uiRoot.name}' 아래에 {nameof(UIEffectTweener)} 를 가진 컨트롤러가 없어 {action} 을 건너뜁니다.", uiRoot);
                return false;
            }

            if (m_VerboseLog)
            {
                Debug.Log($"[{nameof(UIEffectManager)}] '{uiRoot.name}' {driven}개에 {action}.", uiRoot);
            }

            return true;
        }

        // uiRoot 와 그 자식에서 UIEffectController 를 모두 찾아 m_Buffer 에 담는다.
        // 하나도 없으면 false. warnIfEmpty=false 면 조회성 호출이라 경고하지 않는다.
        private bool TryResolve(GameObject uiRoot, bool warnIfEmpty)
        {
            m_Buffer.Clear();

            if (uiRoot == null)
            {
                if (warnIfEmpty)
                {
                    Debug.LogWarning($"[{nameof(UIEffectManager)}] uiRoot 가 비어 있어 요청을 건너뜁니다.", this);
                }

                return false;
            }

            // 비활성 UI(등장 대기 상태 등)에도 걸 수 있게 비활성 포함으로 찾는다.
            // 논-alloc List 오버로드는 Component 쪽에 있으므로 transform 으로 호출한다.
            uiRoot.transform.GetComponentsInChildren(true, m_Buffer);

            if (m_Buffer.Count == 0)
            {
                if (warnIfEmpty)
                {
                    Debug.LogWarning($"[{nameof(UIEffectManager)}] '{uiRoot.name}' 아래에 {nameof(UIEffectController)} 가 없어 요청을 건너뜁니다.", uiRoot);
                }

                return false;
            }

            return true;
        }
        #endregion
    }
}
