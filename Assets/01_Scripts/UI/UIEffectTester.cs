using UnityEngine;

// UIEffectManager 동작 확인용 임시 스크립트.
// 버튼 오브젝트(또는 아무 오브젝트)에 붙이고, Button 의 OnClick 에 이 컴포넌트의 메서드를 등록해 쓴다.
//
// 사용 순서
//  1) 씬에 UIEffectManager 를 두고 UI Registry 에 uiId + 컨트롤러를 등록한다.
//  2) 이 스크립트를 버튼에 붙이고 Target Ui Id / Preset Name 을 채운다.
//  3) Button ▸ OnClick ▸ + ▸ 이 오브젝트 ▸ UIEffectTester ▸ ApplyPreset() 선택.
public class UIEffectTester : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("이펙트를 적용할 UI 이름. UIEffectManager 의 UI Registry 에 등록된 uiId 여야 한다.")]
    [SerializeField] private string m_TargetUiId = "TestUI";

    [Tooltip("적용할 프리셋 이름. 컨트롤러 로컬 프리셋 id 또는 Project Settings ▸ UI Effect ▸ Runtime Presets 의 이름.")]
    [SerializeField] private string m_PresetName = "Dissolve";

    [Tooltip("체크 시 현재 이펙트 위에 겹쳐 적용한다. 해제 시 전체 교체.")]
    [SerializeField] private bool m_Append;

    [Tooltip("적용할 트위너 프리셋 이름. 컨트롤러 로컬 목록 또는 매니저의 트위너 프리셋 라이브러리에 있는 이름.")]
    [SerializeField] private string m_TweenerPresetName = "FadeIn";

    // 인스펙터에 적어둔 uiId + 프리셋을 적용한다. 버튼 OnClick 기본 진입점.
    public void ApplyPreset()
    {
        if (!TryGetManager(out var manager)) return;

        var success = manager.ApplyPreset(m_TargetUiId, m_PresetName, m_Append);
        Debug.Log($"[{nameof(UIEffectTester)}] ApplyPreset('{m_TargetUiId}', '{m_PresetName}', append={m_Append}) → {success}", this);
    }

    // 버튼마다 다른 프리셋을 걸고 싶을 때 사용한다.
    // OnClick 에서 이 메서드를 고르면 문자열 입력칸이 생기므로, 거기에 프리셋 이름을 적으면 된다.
    public void ApplyPresetByName(string presetName)
    {
        if (!TryGetManager(out var manager)) return;

        var success = manager.ApplyPreset(m_TargetUiId, presetName, m_Append);
        Debug.Log($"[{nameof(UIEffectTester)}] ApplyPresetByName('{m_TargetUiId}', '{presetName}', append={m_Append}) → {success}", this);
    }

    // 인스펙터에 적어둔 트위너 프리셋을 적용한다. 프리셋의 재생 정책에 따라 바로 재생될 수도 있다.
    public void ApplyTweenerPreset()
    {
        if (!TryGetManager(out var manager)) return;

        var success = manager.ApplyTweenerPreset(m_TargetUiId, m_TweenerPresetName);
        Debug.Log($"[{nameof(UIEffectTester)}] ApplyTweenerPreset('{m_TargetUiId}', '{m_TweenerPresetName}') → {success}", this);
    }

    // 버튼마다 다른 트위너 프리셋을 걸고 싶을 때 사용한다. OnClick 에 문자열 입력칸이 생긴다.
    public void ApplyTweenerPresetByName(string presetName)
    {
        if (!TryGetManager(out var manager)) return;

        var success = manager.ApplyTweenerPreset(m_TargetUiId, presetName);
        Debug.Log($"[{nameof(UIEffectTester)}] ApplyTweenerPresetByName('{m_TargetUiId}', '{presetName}') → {success}", this);
    }

    // 등록된 모든 UI 에 같은 프리셋을 적용한다. 그룹/전체 적용 확인용.
    public void ApplyPresetToAll()
    {
        if (!TryGetManager(out var manager)) return;

        var success = manager.ApplyPresetToAll(m_PresetName, m_Append);
        Debug.Log($"[{nameof(UIEffectTester)}] ApplyPresetToAll('{m_PresetName}', append={m_Append}) → {success}", this);
    }

    // 대상 UI 의 이펙트만 기본값으로 되돌린다.
    public void ClearEffect()
    {
        if (!TryGetManager(out var manager)) return;

        var success = manager.ClearEffect(m_TargetUiId);
        Debug.Log($"[{nameof(UIEffectTester)}] ClearEffect('{m_TargetUiId}') → {success}", this);
    }

    // 등록된 모든 UI 의 이펙트를 기본값으로 되돌린다.
    public void ClearAll()
    {
        if (!TryGetManager(out var manager)) return;

        manager.ClearAll();
        Debug.Log($"[{nameof(UIEffectTester)}] ClearAll()", this);
    }

    // 적용 없이 프리셋 이름이 유효한지만 확인한다. 오타 점검용.
    public void LogPresetAvailability()
    {
        if (!TryGetManager(out var manager)) return;

        Debug.Log($"[{nameof(UIEffectTester)}] '{m_PresetName}' 중앙 등록={manager.HasPreset(m_PresetName)}, " +
                  $"'{m_TargetUiId}' 사용 가능={manager.HasPreset(m_TargetUiId, m_PresetName)}, " +
                  $"트위너 프리셋 '{m_TweenerPresetName}' 사용 가능={manager.HasTweenerPreset(m_TargetUiId, m_TweenerPresetName)}, " +
                  $"등록된 uiId 수={manager.RegisteredIdCount}", this);
    }

    // 매니저는 없으면 자동 생성되지만, 종료 중에는 null 이 올 수 있어 가드한다.
    private bool TryGetManager(out UIEffectManager manager)
    {
        manager = UIEffectManager.Instance;
        if (manager != null) return true;

        Debug.LogWarning($"[{nameof(UIEffectTester)}] UIEffectManager 를 찾을 수 없습니다.", this);
        return false;
    }
}
