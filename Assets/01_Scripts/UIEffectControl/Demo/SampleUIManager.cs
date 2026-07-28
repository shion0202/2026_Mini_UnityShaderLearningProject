using System;
using System.Collections.Generic;
using UnityEngine;

namespace UIEffectControl
{
    // 팀의 실제 UI Manager 를 대신하는 예시/임시 스탠드인.
    //
    // 두 가지 목적을 겸한다.
    //  1) 데모 씬에서 버튼으로 이펙트를 걸어보는 테스트 드라이버.
    //  2) 실제 UI Manager 가 IUIEffectService 를 어떻게 호출해야 하는지의 참조 예시.
    //
    // 핵심 흐름(실제 UI Manager 도 이 형태를 따르면 된다):
    //   외부 요청("A UI 에 B 프리셋") → 자기 UI 목록에서 A 의 GameObject 를 찾음
    //   → IUIEffectService.ApplyPreset(그 GameObject, "B") 위임.
    //
    // 즉 UI 의 생성/삭제/목록은 이 쪽(UI Manager)이 소유하고,
    // 이펙트 실행은 UIEffectManager 에 위임한다. 이펙트 매니저는 UI 목록을 들지 않는다.
    public class SampleUIManager : MonoBehaviour
    {
        // 이 UI Manager 가 소유하는 UI 목록. uiId → 이펙트를 걸 루트 GameObject.
        // 실제 UI Manager 라면 프리팹 생성/삭제 시 이 목록을 직접 관리한다.
        [Serializable]
        public class UIEntry
        {
            [Tooltip("이 UI 를 지목할 이름. 고유해야 한다.")]
            public string uiId;

            [Tooltip("이펙트를 걸 UI 의 루트 GameObject. 이 오브젝트와 자식의 UIEffectController 가 대상이 된다.")]
            public GameObject uiRoot;
        }

        [Header("UI 목록 (이 매니저가 소유)")]
        [SerializeField] private List<UIEntry> m_UIs = new List<UIEntry>();

        [Header("버튼 테스트용 기본값")]
        [Tooltip("버튼에서 이펙트를 걸 대상 UI 이름.")]
        [SerializeField] private string m_TargetUIId = "State";

        [Tooltip("적용할 프리셋 이름(컨트롤러 로컬 id 또는 중앙 레지스트리 이름).")]
        [SerializeField] private string m_PresetName = "Trauma";

        [Tooltip("적용할 트위너 프리셋 이름(컨트롤러 로컬 또는 공용 라이브러리).")]
        [SerializeField] private string m_TweenerPresetName = "Trauma";

        [Tooltip("체크 시 현재 이펙트 위에 겹쳐 적용. 해제 시 전체 교체.")]
        [SerializeField] private bool m_Append;

        // uiId → uiRoot 조회 테이블.
        private readonly Dictionary<string, GameObject> m_Index = new Dictionary<string, GameObject>();

        // 이펙트 실행을 위임할 서비스. 실제 UI Manager 는 원한다면 이 인터페이스를
        // 생성자/초기화에서 주입받아 UIEffectManager 결합을 더 낮출 수 있다.
        private IUIEffectService Service => UIEffectManager.Instance;

        private void Awake()
        {
            RebuildIndex();
        }

        // ─────────────────────────────────────────────────────────────
        // 버튼 OnClick 진입점 (옛 UIEffectTester 와 메서드 이름을 맞춰 재배선을 줄임)
        // ─────────────────────────────────────────────────────────────

        // 인스펙터 기본값(m_TargetUIId + m_PresetName)으로 적용.
        public void ApplyPreset()
        {
            ApplyPresetByName(m_PresetName);
        }

        // 버튼마다 다른 프리셋을 걸 때. OnClick 문자열 칸에 프리셋 이름을 적는다.
        public void ApplyPresetByName(string presetName)
        {
            if (!TryGetUIRoot(m_TargetUIId, out var uiRoot)) return;
            if (Service == null) return;

            var success = Service.ApplyPreset(uiRoot, presetName, m_Append);
            Debug.Log($"[{nameof(SampleUIManager)}] ApplyPreset('{m_TargetUIId}', '{presetName}', append={m_Append}) → {success}", this);
        }

        // 인스펙터 기본값(m_TargetUIId + m_TweenerPresetName)으로 트위너 프리셋 적용.
        public void ApplyTweenerPreset()
        {
            ApplyTweenerPresetByName(m_TweenerPresetName);
        }

        // 버튼마다 다른 트위너 프리셋을 걸 때. OnClick 문자열 칸에 이름을 적는다.
        public void ApplyTweenerPresetByName(string presetName)
        {
            if (!TryGetUIRoot(m_TargetUIId, out var uiRoot)) return;
            if (Service == null) return;

            var success = Service.ApplyTweenerPreset(uiRoot, presetName);
            Debug.Log($"[{nameof(SampleUIManager)}] ApplyTweenerPreset('{m_TargetUIId}', '{presetName}') → {success}", this);
        }

        // 대상 UI 를 통째로 끈다(이펙트 초기화 + 트윈 정지). 기본 경로.
        public void Clear()
        {
            if (!TryGetUIRoot(m_TargetUIId, out var uiRoot) || Service == null) return;

            Service.Clear(uiRoot);
            Debug.Log($"[{nameof(SampleUIManager)}] Clear('{m_TargetUIId}')", this);
        }

        // 대상 UI 의 이펙트만 기본값으로 되돌린다(도는 트윈은 그대로).
        public void ClearEffect()
        {
            if (!TryGetUIRoot(m_TargetUIId, out var uiRoot) || Service == null) return;

            Service.ClearEffect(uiRoot);
            Debug.Log($"[{nameof(SampleUIManager)}] ClearEffect('{m_TargetUIId}')", this);
        }

        // ─────────────────────────────────────────────────────────────
        // 트윈 재생 제어 (매니저 패스스루 호출 예시 — 호버/토글/팝업 등에 사용)
        // ─────────────────────────────────────────────────────────────

        // 대상 UI 의 트윈을 정방향(0→1) 재생.
        public void PlayForward()
        {
            if (!TryGetUIRoot(m_TargetUIId, out var uiRoot) || Service == null) return;
            Service.PlayForward(uiRoot);
        }

        // 대상 UI 의 트윈을 역방향(1→0) 재생.
        public void PlayReverse()
        {
            if (!TryGetUIRoot(m_TargetUIId, out var uiRoot) || Service == null) return;
            Service.PlayReverse(uiRoot);
        }

        // 대상 UI 의 트윈 진행 방향을 반전(호버 인/아웃 토글).
        public void TogglePlay()
        {
            if (!TryGetUIRoot(m_TargetUIId, out var uiRoot) || Service == null) return;
            Service.TogglePlay(uiRoot);
        }

        // 대상 UI 의 트윈을 정지하고 시간을 리셋.
        public void StopTween()
        {
            if (!TryGetUIRoot(m_TargetUIId, out var uiRoot) || Service == null) return;
            Service.StopTween(uiRoot);
        }

        // ─────────────────────────────────────────────────────────────
        // UI 목록 관리 (실제 UI Manager 가 담당할 영역의 예시)
        // ─────────────────────────────────────────────────────────────

        // 런타임에 생성된 UI 를 목록에 등록한다.
        public void Register(string uiId, GameObject uiRoot)
        {
            if (string.IsNullOrEmpty(uiId) || uiRoot == null)
            {
                Debug.LogWarning($"[{nameof(SampleUIManager)}] uiId 나 uiRoot 가 비어 있어 등록을 건너뜁니다.", this);
                return;
            }

            m_Index[uiId] = uiRoot;
        }

        // 등록을 해제한다.
        public void Unregister(string uiId)
        {
            if (!string.IsNullOrEmpty(uiId)) m_Index.Remove(uiId);
        }

        // 인스펙터 목록으로 조회 테이블을 다시 만든다.
        public void RebuildIndex()
        {
            m_Index.Clear();

            for (var i = 0; i < m_UIs.Count; i++)
            {
                var entry = m_UIs[i];
                if (entry == null || string.IsNullOrEmpty(entry.uiId) || entry.uiRoot == null) continue;

                m_Index[entry.uiId] = entry.uiRoot;
            }
        }

        // uiId 로 UI 루트를 찾는다. 없으면 경고.
        private bool TryGetUIRoot(string uiId, out GameObject uiRoot)
        {
            if (!string.IsNullOrEmpty(uiId) && m_Index.TryGetValue(uiId, out uiRoot) && uiRoot != null)
            {
                return true;
            }

            Debug.LogWarning($"[{nameof(SampleUIManager)}] '{uiId}' 로 등록된 UI 가 없습니다. UI 목록을 확인하세요.", this);
            uiRoot = null;
            return false;
        }
    }
}
