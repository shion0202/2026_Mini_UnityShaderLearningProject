using System;
using System.Collections.Generic;
using Coffee.UIEffects;
using UnityEngine;

/// <summary>
/// UI 이펙트 요청을 한 곳에서 받는 싱글톤 매니저.
///
/// 외부 시스템은 UI 컴포넌트를 직접 들고 있지 않아도
/// UIEffectManager.Instance.ApplyPreset("HpBar", "Dissolve") 처럼
/// "이 이름의 UI 에게 이 이름의 프리셋을 걸어달라" 고 요청할 수 있다.
///
/// 실제 적용은 각 UI 의 UIEffectController 가 수행하고,
/// 매니저는 uiId → 컨트롤러 조회와 실패 로그만 담당한다.
///
/// UI 목록은 현재 인스펙터 등록(m_Entries)이 임시 소스다.
/// 팀의 UI 관리 방식이 정해지면 FetchFromExternalUIManager 와
/// TryResolveExternal 두 훅만 채우면 그대로 대체된다.
/// </summary>

// 컨트롤러(-100)보다 먼저 초기화해 조회 테이블을 준비한다.
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public class UIEffectManager : MonoBehaviour
{
    // 인스펙터에 등록하는 UI 항목. 하나의 uiId 가 여러 컨트롤러를 묶는 그룹 단위다.
    [Serializable]
    public class UIEntry
    {
        [Tooltip("스크립트에서 이 UI를 지목할 이름. 항목마다 고유해야 한다.")]
        public string uiId;

        [Tooltip("이 uiId 로 요청이 오면 함께 이펙트를 적용할 컨트롤러 목록. 하나만 넣어도 되고, 여러 개 넣으면 한 번에 적용된다.")]
        public List<UIEffectController> controllers = new List<UIEffectController>();
    }

    [Header("UI Registry (임시: 인스펙터 등록)")]
    [Tooltip("이 매니저가 관리할 UI 목록. 외부 UI 매니저가 생기면 초기화 시 여기에 병합하도록 바꾼다.")]
    [SerializeField] private List<UIEntry> m_Entries = new List<UIEntry>();

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

    // uiId → 컨트롤러 목록. 같은 id 가 여러 개면 그룹으로 함께 적용된다.
    private readonly Dictionary<string, List<UIEffectController>> m_Index =
        new Dictionary<string, List<UIEffectController>>();

    /// <summary>
    /// 싱글톤 인스턴스. 씬에 없으면 자동 생성한다(이 경우 인스펙터 등록 목록은 비어 있다).
    /// 애플리케이션 종료 중에는 새로 만들지 않고 null 을 반환한다.
    /// </summary>
    public static UIEffectManager Instance
    {
        get
        {
            if (s_Instance != null) return s_Instance;
            if (s_IsQuitting) return null;

            // 비활성 오브젝트에 붙어 있어도 찾을 수 있게 Include 로 검색한다.
            s_Instance = FindFirstObjectByType<UIEffectManager>(FindObjectsInactive.Include);
            if (s_Instance != null) return s_Instance;

            var go = new GameObject($"[{nameof(UIEffectManager)}]");
            s_Instance = go.AddComponent<UIEffectManager>();
            return s_Instance;
        }
    }

    /// 씬에 매니저가 존재하는지(또는 이미 생성되었는지). 자동 생성을 유발하지 않는다.
    public static bool Exists => s_Instance != null;

    /// 현재 등록된 uiId 개수.
    public int RegisteredIdCount => m_Index.Count;

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

        RebuildIndex();
        FetchFromExternalUIManager();
    }

    private void OnDestroy()
    {
        if (s_Instance == this) s_Instance = null;
    }

    private void OnApplicationQuit()
    {
        // 종료 중 Instance 접근이 새 오브젝트를 되살리지 않도록 막는다.
        s_IsQuitting = true;
    }

    // ─────────────────────────────────────────────────────────────
    // 프리셋 요청 API
    // ─────────────────────────────────────────────────────────────

    /// uiId 로 등록된 UI(들)에 프리셋을 적용한다.
    /// 
    /// uiId: 등록된 UI 이름. 같은 이름이 여러 개면 모두 적용된다.
    /// presetName: 프리셋 id(컨트롤러 로컬) 또는 중앙 레지스트리 이름.
    /// append: true 면 현재 이펙트 위에 겹쳐 적용, false 면 전체 교체.
    /// 하나 이상 적용되면 true.
    public bool ApplyPreset(string uiId, string presetName, bool append = false)
    {
        if (!TryResolve(uiId, out var controllers)) return false;

        var applied = 0;
        for (var i = 0; i < controllers.Count; i++)
        {
            var controller = controllers[i];
            if (controller == null) continue;
            if (controller.TryLoadPreset(presetName, append)) applied++;
        }

        if (applied == 0)
        {
            Debug.LogWarning($"[{nameof(UIEffectManager)}] '{uiId}' 에 '{presetName}' 프리셋을 적용하지 못했습니다. " +
                             "프리셋 이름 또는 등록 여부를 확인하세요. (Project Settings ▸ UI Effect ▸ Runtime Presets)", this);
            return false;
        }

        if (m_VerboseLog)
        {
            Debug.Log($"[{nameof(UIEffectManager)}] '{uiId}' {applied}개에 '{presetName}' 적용(append={append}).", this);
        }

        return true;
    }

    /// 등록된 모든 UI 에 같은 프리셋을 적용한다.
    /// 하나 이상 적용되면 true.
    public bool ApplyPresetToAll(string presetName, bool append = false)
    {
        var applied = 0;
        foreach (var pair in m_Index)
        {
            var controllers = pair.Value;
            for (var i = 0; i < controllers.Count; i++)
            {
                var controller = controllers[i];
                if (controller == null) continue;
                if (controller.TryLoadPreset(presetName, append)) applied++;
            }
        }

        if (applied == 0)
        {
            Debug.LogWarning($"[{nameof(UIEffectManager)}] 어떤 UI 에도 '{presetName}' 프리셋을 적용하지 못했습니다.", this);
            return false;
        }

        if (m_VerboseLog)
        {
            Debug.Log($"[{nameof(UIEffectManager)}] 전체 {applied}개에 '{presetName}' 적용(append={append}).", this);
        }

        return true;
    }

    // uiId 로 등록된 UI(들)의 이펙트를 기본값으로 되돌린다.
    public bool ClearEffect(string uiId)
    {
        if (!TryResolve(uiId, out var controllers)) return false;

        for (var i = 0; i < controllers.Count; i++)
        {
            if (controllers[i] != null) controllers[i].ClearEffect();
        }

        return true;
    }

    // 등록된 모든 UI 의 이펙트를 기본값으로 되돌린다.
    public void ClearAll()
    {
        foreach (var pair in m_Index)
        {
            var controllers = pair.Value;
            for (var i = 0; i < controllers.Count; i++)
            {
                if (controllers[i] != null) controllers[i].ClearEffect();
            }
        }
    }

    /// 프리셋 이름이 중앙 레지스트리에 등록돼 있는지 확인한다.
    /// (컨트롤러 로컬 전용 프리셋까지 확인하려면 uiId 를 넘기는 오버로드를 쓴다)
    public bool HasPreset(string presetName)
    {
        return UIEffectProjectSettings.LoadPreset(presetName) != null;
    }

    // 해당 UI 가 그 프리셋을 쓸 수 있는지 확인한다(컨트롤러 로컬 목록 → 중앙 레지스트리).
    public bool HasPreset(string uiId, string presetName)
    {
        if (!TryGetControllers(uiId, out var controllers)) return false;

        for (var i = 0; i < controllers.Count; i++)
        {
            if (controllers[i] != null && controllers[i].HasPreset(presetName)) return true;
        }

        return false;
    }

    // ─────────────────────────────────────────────────────────────
    // 트위너 프리셋 요청 API
    // ─────────────────────────────────────────────────────────────

    // uiId 로 등록된 UI(들)의 트위너에 프리셋을 적용한다.
    // 조회 순서는 이펙트 프리셋과 같다: 컨트롤러 로컬 목록 → 공용 라이브러리.
    // 프리셋의 재생 정책(Play On Apply)에 따라 적용 직후 재생까지 처리된다.
    public bool ApplyTweenerPreset(string uiId, string presetName)
    {
        if (!TryResolve(uiId, out var controllers)) return false;

        var applied = 0;
        for (var i = 0; i < controllers.Count; i++)
        {
            var controller = controllers[i];
            if (controller == null) continue;

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
            Debug.LogWarning($"[{nameof(UIEffectManager)}] '{uiId}' 에 트위너 프리셋 '{presetName}' 을 적용하지 못했습니다. " +
                             "프리셋 이름, 라이브러리 등록 여부, UIEffectTweener 컴포넌트 유무를 확인하세요.", this);
            return false;
        }

        if (m_VerboseLog)
        {
            Debug.Log($"[{nameof(UIEffectManager)}] '{uiId}' {applied}개에 트위너 프리셋 '{presetName}' 적용.", this);
        }

        return true;
    }

    // 공용 라이브러리에 해당 트위너 프리셋이 등록돼 있는지 확인한다.
    public bool HasTweenerPreset(string presetName)
    {
        return m_TweenerLibrary != null && m_TweenerLibrary.Contains(presetName);
    }

    // 해당 UI 가 그 트위너 프리셋을 쓸 수 있는지 확인한다(컨트롤러 로컬 목록 → 공용 라이브러리).
    public bool HasTweenerPreset(string uiId, string presetName)
    {
        if (HasTweenerPreset(presetName)) return true;
        if (!TryGetControllers(uiId, out var controllers)) return false;

        for (var i = 0; i < controllers.Count; i++)
        {
            if (controllers[i] != null && controllers[i].HasTweenerPreset(presetName)) return true;
        }

        return false;
    }

    // ─────────────────────────────────────────────────────────────
    // UI 등록/조회
    // ─────────────────────────────────────────────────────────────

    // uiId 로 등록된 컨트롤러 목록을 얻는다. 없으면 false(경고 없음).
    public bool TryGetControllers(string uiId, out IReadOnlyList<UIEffectController> controllers)
    {
        if (!string.IsNullOrEmpty(uiId) && m_Index.TryGetValue(uiId, out var list) && list.Count > 0)
        {
            controllers = list;
            return true;
        }

        controllers = Array.Empty<UIEffectController>();
        return false;
    }

    // 런타임에 생성된 UI 를 등록한다. 같은 id 로 여러 개 등록하면 그룹이 된다.
    public void Register(string uiId, UIEffectController controller)
    {
        if (string.IsNullOrEmpty(uiId) || controller == null)
        {
            Debug.LogWarning($"[{nameof(UIEffectManager)}] uiId 나 컨트롤러가 비어 있어 등록을 건너뜁니다.", this);
            return;
        }

        if (!m_Index.TryGetValue(uiId, out var list))
        {
            list = new List<UIEffectController>();
            m_Index[uiId] = list;
        }

        if (!list.Contains(controller)) list.Add(controller);
    }

    // 등록을 해제한다. 해당 id 의 마지막 항목이면 id 자체도 제거한다.
    public void Unregister(string uiId, UIEffectController controller)
    {
        if (string.IsNullOrEmpty(uiId) || !m_Index.TryGetValue(uiId, out var list)) return;

        list.Remove(controller);
        if (list.Count == 0) m_Index.Remove(uiId);
    }

    // 해당 uiId 의 등록을 통째로 해제한다.
    public void UnregisterAll(string uiId)
    {
        if (string.IsNullOrEmpty(uiId)) return;
        m_Index.Remove(uiId);
    }

    // 인스펙터 등록 목록으로 조회 테이블을 다시 만든다. 런타임 Register 내용은 사라진다.
    public void RebuildIndex()
    {
        m_Index.Clear();

        for (var i = 0; i < m_Entries.Count; i++)
        {
            var entry = m_Entries[i];
            if (entry == null) continue;

            if (string.IsNullOrEmpty(entry.uiId))
            {
                Debug.LogWarning($"[{nameof(UIEffectManager)}] 등록 목록 {i}번 항목의 uiId 가 비어 있어 건너뜁니다.", this);
                continue;
            }

            // 그룹은 엔트리 안의 리스트로 표현하므로, uiId 중복은 의도된 그룹이 아니라 실수로 본다.
            // 동작이 끊기지 않게 병합은 해주되 경고로 알린다.
            if (m_Index.ContainsKey(entry.uiId))
            {
                Debug.LogWarning($"[{nameof(UIEffectManager)}] uiId '{entry.uiId}' 가 중복 등록되어 있습니다. " +
                                 "한 항목의 Controllers 목록으로 합치세요. 일단 병합해 사용합니다.", this);
            }

            var controllers = entry.controllers;
            if (controllers == null || controllers.Count == 0)
            {
                Debug.LogWarning($"[{nameof(UIEffectManager)}] '{entry.uiId}' 항목에 컨트롤러가 하나도 없어 건너뜁니다.", this);
                continue;
            }

            for (var j = 0; j < controllers.Count; j++)
            {
                if (controllers[j] == null)
                {
                    Debug.LogWarning($"[{nameof(UIEffectManager)}] '{entry.uiId}' 항목의 {j}번 컨트롤러가 비어 있어 건너뜁니다.", this);
                    continue;
                }

                Register(entry.uiId, controllers[j]);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 외부 UI 소스 연동 훅 (UI 관리 방식 확정 후 구현)
    // ─────────────────────────────────────────────────────────────

    /// 초기화 시 외부 UI 매니저가 들고 있는 UI 목록을 받아와 조회 테이블에 병합한다.
    /// 받아온 항목마다 <see cref="Register"/> 를 호출하면 인스펙터 등록분과 함께 동작한다.
    private void FetchFromExternalUIManager()
    {
        // TODO: 팀의 UI 매니저가 정해지면 여기서 UI 목록을 받아 Register 로 병합한다.
        //  예) foreach (var ui in TeamUIManager.Instance.GetAllUI())
        //          Register(ui.Id, ui.GetComponent<UIEffectController>());
        //  주의: 외부 매니저가 이 매니저보다 늦게 초기화되면 실행 순서 조정이나
        //        외부 매니저 준비 콜백에서 재호출이 필요하다.
    }

    /// 조회 테이블에 없는 uiId 를 외부에서 찾아보는 폴백 훅.
    /// 찾으면 컨트롤러를 반환하고, TryResolve 가 결과를 테이블에 캐시한다.
    private bool TryResolveExternal(string uiId, out UIEffectController controller)
    {
        // TODO: 외부 UI 매니저에서 uiId 로 UI 를 검색해 UIEffectController 를 돌려준다.
        //  예) var ui = TeamUIManager.Instance.Find(uiId);
        //      if (ui != null && ui.TryGetComponent(out controller)) return true;
        controller = null;
        return false;
    }

    // ─────────────────────────────────────────────────────────────
    // 내부 유틸
    // ─────────────────────────────────────────────────────────────

    /// 등록 테이블 → 외부 폴백 순으로 uiId 를 해석한다. 실패하면 경고한다.
    private bool TryResolve(string uiId, out List<UIEffectController> controllers)
    {
        if (!string.IsNullOrEmpty(uiId) && m_Index.TryGetValue(uiId, out controllers) && controllers.Count > 0)
        {
            return true;
        }

        // 등록되지 않은 id 는 외부 소스에 한 번 물어보고, 찾으면 다음 요청을 위해 캐시한다.
        if (TryResolveExternal(uiId, out var external) && external != null)
        {
            Register(uiId, external);
            controllers = m_Index[uiId];
            return true;
        }

        Debug.LogWarning($"[{nameof(UIEffectManager)}] '{uiId}' 로 등록된 UI 가 없습니다. 매니저의 UI Registry 를 확인하세요.", this);
        controllers = null;
        return false;
    }
}
