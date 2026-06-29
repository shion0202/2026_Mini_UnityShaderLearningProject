#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace NeoMantra2026.Scripts
{
    [CustomEditor(typeof(GradientTextureGenerator))]
    public class GradientTextureGeneratorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var script = (GradientTextureGenerator)target;
            EditorGUILayout.Space();
            if (GUILayout.Button("저장 경로 선택"))
                script.PickSavePath();
            if (GUILayout.Button("그래디언트 텍스처 생성"))
                script.BakeGradient();
        }
    }
}
#endif
