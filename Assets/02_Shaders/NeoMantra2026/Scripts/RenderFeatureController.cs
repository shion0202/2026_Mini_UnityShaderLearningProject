using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NeoMantra2026.Scripts
{
    // 렌더피처 런타임/에디터 컨트롤러.
    // 렌더피처 그룹 on/off: 인스펙터 체크 박스(OnValidate) 또는 SetFeature(이름, bool)
    // 선택 하이라이트: 오브젝트 루트 내에서 렌더러가 있는 오브젝트에 한하여 기본 및 선택 레이어를 스왑
    // 주의: SetActive는 Renderer Data 에셋 상태를 바꾸며, 에디터에선 플레이 종료 후에도 유지.
    [ExecuteAlways]
    [AddComponentMenu("NeoMantra2026/Render Feature Controller")]
    public class RenderFeatureController : MonoBehaviour
    {
        #region Serializable Classes
        [Serializable]
        public class FeatureGroup
        {
            [Tooltip("식별용 그룹 이름.")] public string groupName;
            [Tooltip("그룹 단위로 관리할 렌더 피처 (Universal Renderer Data의 서브 에셋을 드래그).")]
            public List<ScriptableRendererFeature> features = new List<ScriptableRendererFeature>();
            [Tooltip("체크 시 활성화. 인스펙터 변경 사항이 즉시 반영.")] public bool enabled = true;
        }

        [Serializable]
        public class ObjectEntry
        {
            [Tooltip("오브젝트 루트. 자식 중 렌더러가 있는 오브젝트만 레이어 변경(본, 콜라이더 등은 무변경).")]
            public GameObject root;
            [Tooltip("체크 시 선택 상태(선택 레이어로 변경 = 하이라이트 표시).")] public bool selected;

            // 선택 시점의 원래 레이어를 저장하며, 해제 시 복원. 다중 레이어 구성도 지원.
            [SerializeField, HideInInspector] public List<GameObject> savedObjects = new List<GameObject>();
            [SerializeField, HideInInspector] public List<int> savedLayers = new List<int>();
        }

        [Serializable]
        public class LayerPair
        {
            [LayerField, Tooltip("기본 레이어. 렌더러가 이 레이어일 때만 변경 대상.")] public int normalLayer;
            [LayerField, Tooltip("선택 시 옮길 레이어(하이라이트 피처가 보는 레이어).")] public int selectedLayer;
        }
        #endregion

        #region Variables
        [Header("렌더 피처 그룹")]
        [SerializeField, Tooltip("용도별 그룹 관리. (예: Outline / Silhouette / Highlight / FakeShadow)")]
        private List<FeatureGroup> featureGroups = new List<FeatureGroup>();

        [Header("하이라이트 레이어")]
        [SerializeField, Tooltip("기본 및 선택 레이어 페어 목록 (예: Character→CharacterSelected). 렌더러의 현재 레이어와 일치하는 페어를 찾아 선택 레이어로 변경.")]
        private List<LayerPair> layerPairs = new List<LayerPair>();
        [SerializeField, Tooltip("선택 토글 대상 오브젝트 목록. 해제 시 원래 레이어로 복원.")]
        private List<ObjectEntry> objects = new List<ObjectEntry>();

        [Header("하이라이트 피처 (런타임용)")]
        [SerializeField, Tooltip("Public API가 제어할 RenderObjects 피처.")]
        private List<RenderObjects> highlightFeatures = new List<RenderObjects>();
        #endregion

        #region Public API
        // 그룹 이름으로 렌더 피처 on/off. 인스펙터 체크 박스와 상태 동기화.
        public void SetFeature(string groupName, bool on)
        {
            var group = featureGroups.Find(g => g.groupName == groupName);
            if (group == null)
            {
                Debug.LogWarning($"[RenderFeature] 그룹 없음: {groupName}");
                return;
            }

            group.enabled = on;
            ApplyGroup(group);
        }

        public bool GetFeature(string groupName)
        {
            var group = featureGroups.Find(g => g.groupName == groupName);
            return group != null && group.enabled;
        }

        // 캐릭터 선택 상태 변경(레이어 스왑). 목록에 없는 루트면 항목을 추가해 관리.
        public void SetSelected(GameObject root, bool on)
        {
            if (root == null) return;
            var entry = objects.Find(c => c.root == root);
            if (entry == null)
            {
                entry = new ObjectEntry { root = root };
                objects.Add(entry);
            }
            entry.selected = on;
            ApplyObject(entry);
        }

        public bool IsSelected(GameObject root)
        {
            var entry = objects.Find(c => c.root == root);
            return entry != null && entry.selected;
        }

        // 하이라이트 피처의 Filter Layer Mask 직접 제어 (오브젝트 레이어 무손상)
        public void SetHighlightLayers(LayerMask mask)
        {
            foreach (var ro in highlightFeatures)
                ApplyFeatureMask(ro, mask);
        }

        public void AddHighlightLayer(int layer)
        {
            foreach (var ro in highlightFeatures)
                if (ro != null)
                    ApplyFeatureMask(ro, ro.settings.filterSettings.LayerMask | (1 << layer));
        }

        public void RemoveHighlightLayer(int layer)
        {
            foreach (var ro in highlightFeatures)
                if (ro != null)
                    ApplyFeatureMask(ro, ro.settings.filterSettings.LayerMask & ~(1 << layer));
        }
        #endregion

        #region Private Methods
        private static void ApplyFeatureMask(RenderObjects ro, LayerMask mask)
        {
            if (ro == null) return;
            if (ro.settings.filterSettings.LayerMask == mask) return;
            ro.settings.filterSettings.LayerMask = mask;

            // 패스가 생성 시점 필터를 캐시하므로 재생성해야 반영됨
            ro.Create();
#if UNITY_EDITOR
            EditorUtility.SetDirty(ro);
#endif
        }

        private void OnEnable() { ApplyAll(); }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // OnValidate 중 layer 변경은 SendMessage 제한 경고가 발생하므로 에디터에선 한 프레임 지연 적용
                EditorApplication.delayCall -= DelayedApply;   // 중복 등록 방지
                EditorApplication.delayCall += DelayedApply;
                return;
            }
#endif
            ApplyAll();
        }

#if UNITY_EDITOR
        private void DelayedApply()
        {
            EditorApplication.delayCall -= DelayedApply;

            // 지연 사이에 오브젝트가 삭제된 경우
            if (this == null) return;
            ApplyAll();
        }
#endif

        private void ApplyAll()
        {
            foreach (var g in featureGroups) ApplyGroup(g);
            foreach (var c in objects) ApplyObject(c);
        }

        private void ApplyGroup(FeatureGroup group)
        {
            if (group == null) return;

            foreach (var f in group.features)
            {
                if (f == null) continue;
                if (f.isActive == group.enabled) continue;
                f.SetActive(group.enabled);
#if UNITY_EDITOR
                // 에디터에서 에셋 상태 저장 보장
                EditorUtility.SetDirty(f);
#endif
            }
        }

        private void ApplyObject(ObjectEntry entry)
        {
            if (entry == null || entry.root == null) return;

            // 저장분 유무 = 현재 선택 상태 적용 여부
            bool applied = entry.savedObjects.Count > 0;

            if (entry.selected && !applied)
            {
                // 선택: 현재 레이어와 일치하는 페어를 찾아 원래 레이어 저장 후 선택 레이어로 변경
                foreach (var r in entry.root.GetComponentsInChildren<Renderer>(true))
                {
                    var go = r.gameObject;
                    var pair = layerPairs.Find(p => p.normalLayer == go.layer);
                    if (pair == null) continue;   // 페어 없는 레이어는 현상 유지

                    entry.savedObjects.Add(go);
                    entry.savedLayers.Add(go.layer);
                    go.layer = pair.selectedLayer;
                }
            }
            else if (!entry.selected && applied)
            {
                // 해제: 저장해둔 원래 레이어로 복원
                for (int i = 0; i < entry.savedObjects.Count; i++)
                    if (entry.savedObjects[i] != null)
                        entry.savedObjects[i].layer = entry.savedLayers[i];
                entry.savedObjects.Clear();
                entry.savedLayers.Clear();
            }
        }
        #endregion
    }
}
