using UnityEditor;
using UnityEngine;

namespace UIEffectControl
{
    // UIEffectController 인스펙터에 트위너 프리셋 제작 도구를 붙인다.
    //
    // 목적: UIEffectTweener 에서 값을 만지며 느낌을 잡은 뒤, 그 값을 손으로 다시 옮겨적지 않고
    //       버튼 한 번으로 UIEffectTweenerPreset 에셋에 굽기 위한 것.
    //
    // 워크플로
    //  1) 트위너 인스펙터에서 duration/curve 등을 조정해 원하는 느낌을 만든다.
    //  2) 여기서 [현재 트위너 값 → 새 프리셋 저장] → 저장 위치를 고르면 에셋이 생성된다.
    //  3) 나중에 값을 다시 손봤다면 [→ 선택 프리셋 덮어쓰기] 로 갱신한다.
    //  4) [선택 프리셋 → 트위너에 적용] 은 저장된 프리셋이 어떤 느낌인지 되돌려 확인할 때 쓴다.
    [CustomEditor(typeof(UIEffectController))]
    [CanEditMultipleObjects]
    public class UIEffectControllerEditor : Editor
    {
        private UIEffectTweenerPreset m_WorkPreset;
        private bool m_AddToLocalList = true;
        private bool m_FoldOut = true;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            m_FoldOut = EditorGUILayout.Foldout(m_FoldOut, "트위너 프리셋 도구", true);
            if (!m_FoldOut) return;

            var controller = (UIEffectController)target;
            var tweener = controller.Tweener;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (targets.Length > 1)
                {
                    EditorGUILayout.HelpBox("여러 오브젝트를 함께 선택한 상태에서는 프리셋 도구를 쓸 수 없습니다.", MessageType.Info);
                    return;
                }

                if (tweener == null)
                {
                    EditorGUILayout.HelpBox("같은 오브젝트에 UIEffectTweener 가 없습니다. 먼저 추가한 뒤 값을 조정하세요.", MessageType.Info);
                    return;
                }

                EditorGUILayout.HelpBox("트위너에서 값을 조정한 뒤 아래 버튼으로 프리셋 에셋에 저장하세요. 값을 옮겨적을 필요는 없습니다.", MessageType.None);

                m_WorkPreset = (UIEffectTweenerPreset)EditorGUILayout.ObjectField(
                    new GUIContent("대상 프리셋", "덮어쓰기 / 적용 버튼이 사용할 프리셋 에셋."),
                    m_WorkPreset, typeof(UIEffectTweenerPreset), false);

                m_AddToLocalList = EditorGUILayout.ToggleLeft(
                    new GUIContent("저장 후 이 UI 의 로컬 목록에 추가", "새로 만든 프리셋을 컨트롤러의 Tweener Presets 목록에 자동 등록합니다."),
                    m_AddToLocalList);

                EditorGUILayout.Space(2f);

                if (GUILayout.Button("현재 트위너 값 → 새 프리셋 저장"))
                {
                    SaveAsNewPreset(controller);
                }

                using (new EditorGUI.DisabledScope(m_WorkPreset == null))
                {
                    if (GUILayout.Button("현재 트위너 값 → 선택 프리셋 덮어쓰기"))
                    {
                        OverwritePreset(controller);
                    }

                    if (GUILayout.Button("선택 프리셋 → 트위너에 적용 (확인용)"))
                    {
                        ApplyPresetToTweener(controller);
                    }
                }
            }
        }

        private void SaveAsNewPreset(UIEffectController controller)
        {
            var defaultName = $"{controller.gameObject.name}_Tween";
            var path = EditorUtility.SaveFilePanelInProject(
                "트위너 프리셋 저장", defaultName, "asset", "현재 트위너 값을 새 프리셋 에셋으로 저장합니다.");
            if (string.IsNullOrEmpty(path)) return;

            var preset = CreateInstance<UIEffectTweenerPreset>();
            preset.CaptureFrom(controller.Tweener);

            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();

            m_WorkPreset = preset;
            if (m_AddToLocalList) AddToLocalList(preset);

            EditorGUIUtility.PingObject(preset);
            Debug.Log($"[{nameof(UIEffectControllerEditor)}] 트위너 프리셋을 저장했습니다: {path}", preset);
        }

        private void OverwritePreset(UIEffectController controller)
        {
            if (!EditorUtility.DisplayDialog("프리셋 덮어쓰기",
                    $"'{m_WorkPreset.name}' 의 트윈 설정을 현재 트위너 값으로 덮어씁니다.\n" +
                    "재생 정책(Play On Apply / Reset Time On Apply)은 유지됩니다.", "덮어쓰기", "취소"))
            {
                return;
            }

            Undo.RecordObject(m_WorkPreset, "Overwrite Tweener Preset");
            m_WorkPreset.CaptureFrom(controller.Tweener);
            EditorUtility.SetDirty(m_WorkPreset);
            AssetDatabase.SaveAssets();

            Debug.Log($"[{nameof(UIEffectControllerEditor)}] '{m_WorkPreset.name}' 을 현재 트위너 값으로 갱신했습니다.", m_WorkPreset);
        }

        private void ApplyPresetToTweener(UIEffectController controller)
        {
            // 컨트롤러의 Play On Enable 도 함께 바뀌므로 둘 다 되돌릴 수 있게 기록한다.
            Undo.RecordObject(controller.Tweener, "Apply Tweener Preset");
            Undo.RecordObject(controller, "Apply Tweener Preset");

            controller.ApplyTweenerPreset(m_WorkPreset);

            EditorUtility.SetDirty(controller.Tweener);
            EditorUtility.SetDirty(controller);
        }

        // 새로 만든 프리셋을 컨트롤러의 Tweener Presets 목록 끝에 등록한다.
        private void AddToLocalList(UIEffectTweenerPreset preset)
        {
            var listProperty = serializedObject.FindProperty("m_TweenerPresets");
            if (listProperty == null) return;

            var index = listProperty.arraySize;
            listProperty.InsertArrayElementAtIndex(index);
            listProperty.GetArrayElementAtIndex(index).objectReferenceValue = preset;
            serializedObject.ApplyModifiedProperties();
        }
    }
}
