using System.Collections.Generic;
using UnityEngine;

namespace UIEffectControl
{
    // 트위너 프리셋을 이름으로 찾기 위한 중앙 목록.
    // UIEffectManager 에 이 라이브러리 에셋 하나만 물려두면 씬/프리팹과 무관하게 공유된다.
    // (UIEffect 프리셋의 Project Settings ▸ Runtime Presets 에 대응하는 역할)
    [CreateAssetMenu(fileName = "UIEffectTweenerPresetLibrary", menuName = "UI Effect/Tweener Preset Library")]
    public class UIEffectTweenerPresetLibrary : ScriptableObject
    {
        [Tooltip("이름으로 조회할 트위너 프리셋 목록.")]
        [SerializeField] private List<UIEffectTweenerPreset> m_Presets = new List<UIEffectTweenerPreset>();

        // 이름 → 프리셋 조회 캐시. 목록이 바뀌면 무효화된다.
        private Dictionary<string, UIEffectTweenerPreset> m_Lookup;

        public IReadOnlyList<UIEffectTweenerPreset> Presets => m_Presets;

        // 이름으로 프리셋을 찾는다. 없으면 false(경고는 호출자 몫).
        public bool TryGet(string presetName, out UIEffectTweenerPreset preset)
        {
            preset = null;
            if (string.IsNullOrEmpty(presetName)) return false;

            if (m_Lookup == null) RebuildLookup();
            return m_Lookup.TryGetValue(presetName, out preset) && preset != null;
        }

        public bool Contains(string presetName)
        {
            return TryGet(presetName, out _);
        }

        // 목록을 다시 훑어 조회 캐시를 만든다. 목록을 코드로 바꿨다면 직접 호출한다.
        public void RebuildLookup()
        {
            if (m_Lookup == null) m_Lookup = new Dictionary<string, UIEffectTweenerPreset>();
            else m_Lookup.Clear();

            for (var i = 0; i < m_Presets.Count; i++)
            {
                var preset = m_Presets[i];
                if (preset == null) continue;

                var id = preset.ResolvedId;
                if (string.IsNullOrEmpty(id)) continue;

                if (m_Lookup.ContainsKey(id))
                {
                    Debug.LogWarning($"[{nameof(UIEffectTweenerPresetLibrary)}] 프리셋 이름 '{id}' 가 중복됩니다. 먼저 등록된 것을 사용합니다.", this);
                    continue;
                }

                m_Lookup[id] = preset;
            }
        }

        // 에셋을 다시 로드하거나 인스펙터에서 목록을 고치면 캐시를 버린다.
        private void OnEnable()
        {
            m_Lookup = null;
        }

    #if UNITY_EDITOR
        private void OnValidate()
        {
            m_Lookup = null;
        }
    #endif
    }
}
