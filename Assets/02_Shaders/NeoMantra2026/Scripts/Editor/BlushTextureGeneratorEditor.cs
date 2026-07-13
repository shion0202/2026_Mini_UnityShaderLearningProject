#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace NeoMantra2026.Scripts
{
    [CustomEditor(typeof(BlushTextureGenerator))]
    public class BlushTextureGeneratorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var script = (BlushTextureGenerator)target;

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
                else if (prop.propertyPath == "generatedTexture")
                {
                    // 텍스처를 새로 등록하면 textureName을 그 파일 이름으로 동기화
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(prop, true);
                    if (EditorGUI.EndChangeCheck() && prop.objectReferenceValue != null)
                        serializedObject.FindProperty("textureName").stringValue = prop.objectReferenceValue.name;
                }
                else
                {
                    EditorGUILayout.PropertyField(prop, true);
                }
            }
            serializedObject.ApplyModifiedProperties();

            var genTexProp = serializedObject.FindProperty("generatedTexture");
            bool editing = genTexProp.objectReferenceValue != null;
            string texName = serializedObject.FindProperty("textureName").stringValue;
            bool nameEmpty = string.IsNullOrWhiteSpace(texName);

            EditorGUILayout.Space();

            if (editing)
            {
                string assetPath = AssetDatabase.GetAssetPath(genTexProp.objectReferenceValue);
                EditorGUILayout.HelpBox(
                    $"{assetPath} 파일을 갱신합니다.\n(저장 경로는 신규 생성에만 사용됩니다.)",
                    MessageType.Info);
            }
            else if (nameEmpty)
            {
                EditorGUILayout.HelpBox("텍스처 파일 이름을 입력하세요.", MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(!editing && nameEmpty))
            {
                if (GUILayout.Button(editing ? "블러쉬 텍스처 수정" : "블러쉬 텍스처 생성"))
                    script.BakeBlush();
            }
        }
    }
}
#endif
