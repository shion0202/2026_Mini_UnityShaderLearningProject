#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace NeoMantra2026.Scripts
{
    [CustomEditor(typeof(SmoothNormalBaker))]
    public class SmoothNormalBakerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var script = (SmoothNormalBaker)target;

            serializedObject.Update();
            SerializedProperty prop = serializedObject.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (prop.propertyPath == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(prop, true);
                    continue;
                }

                if (prop.propertyPath == "savePath")
                {
                    // 저장 경로는 읽기 전용(폴더 선택 버튼으로만 변경)
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(prop, true);
                    if (GUILayout.Button("저장 경로 선택"))
                        script.PickSavePath();
                }
                else if (prop.propertyPath == "originalMeshes" || prop.propertyPath == "bakedMeshes")
                {
                    // 자동 기록 목록은 읽기 전용 표시
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(prop, true);
                }
                else
                {
                    EditorGUILayout.PropertyField(prop, true);
                }
            }
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "위치가 같은 정점들의 노멀을 평균해 탄젠트 공간으로 UV3에 저장한 메시 사본을 만들고 렌더러에 스왑합니다.\n" +
                "원본 메시는 수정되지 않으며, '원본으로 되돌리기'로 언제든 복구할 수 있습니다.",
                MessageType.Info);

            if (GUILayout.Button("스무스 노멀 베이크 + 메시 스왑"))
                script.Bake();
            if (GUILayout.Button("원본으로 되돌리기"))
                script.Revert();
        }
    }
}
#endif
