using UnityEditor;
using UnityEngine;

namespace UIEffectControl
{
    // Readme 에셋을 인스펙터에 서식 있는 문서로 렌더한다.
    // 프로젝트 창에서 에셋을 클릭하면 별도 뷰어 없이 유니티 안에서 바로 읽힌다.
    [CustomEditor(typeof(Readme))]
    public class ReadmeEditor : Editor
    {
        private GUIStyle m_TitleStyle;
        private GUIStyle m_HeadingStyle;
        private GUIStyle m_BodyStyle;
        private GUIStyle m_BulletStyle;
        private GUIStyle m_CodeStyle;
        private bool m_ShowRaw;

        // 라이트/다크 스킨 모두에서 읽히는 제목·소제목 강조색.
        private static Color AccentColor =>
            EditorGUIUtility.isProSkin ? new Color(0.45f, 0.72f, 1f) : new Color(0.11f, 0.35f, 0.62f);

        private void BuildStyles()
        {
            if (m_BodyStyle != null) return;

            m_TitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 19,
                wordWrap = true,
                margin = new RectOffset(0, 0, 2, 6)
            };
            m_TitleStyle.normal.textColor = AccentColor;

            m_HeadingStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                wordWrap = true
            };
            m_HeadingStyle.normal.textColor = AccentColor;

            // 본문은 리치 텍스트 허용(<b> 등). 굵기/강조를 본문에서 표현한다.
            m_BodyStyle = new GUIStyle(EditorStyles.label)
            {
                richText = true,
                wordWrap = true,
                fontSize = 12
            };

            // 불릿 기호 전용(고정 폭, 행잉 인덴트를 위해 본문과 분리해 그린다).
            m_BulletStyle = new GUIStyle(m_BodyStyle) { alignment = TextAnchor.UpperLeft };

            // 코드는 리치 텍스트를 끈다. 안 그러면 GetComponent<T>() 의 <T> 가 태그로 먹혀 사라진다.
            // 긴 줄이 인스펙터 폭에서 잘리지 않게 wordWrap 은 켜고, 박스 배경 + 패딩으로 본문과 구분한다.
            m_CodeStyle = new GUIStyle(EditorStyles.textArea)
            {
                richText = false,
                wordWrap = true,
                fontSize = 12,
                padding = new RectOffset(8, 8, 6, 6)
            };
        }

        public override void OnInspectorGUI()
        {
            BuildStyles();

            var readme = (Readme)target;

            // 좌우 여백을 줘서 텍스트가 인스펙터 가장자리에 붙지 않게 한다.
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8f);
            EditorGUILayout.BeginVertical();

            if (!string.IsNullOrEmpty(readme.title))
            {
                EditorGUILayout.Space(6f);
                GUILayout.Label(readme.title, m_TitleStyle);
                DrawRule();
            }

            if (readme.sections != null)
            {
                foreach (var section in readme.sections)
                {
                    if (section != null) DrawSection(section);
                }
            }

            EditorGUILayout.Space(14f);
            DrawRule();
            m_ShowRaw = EditorGUILayout.Foldout(m_ShowRaw, "원본 필드 편집", true);
            if (m_ShowRaw)
            {
                EditorGUILayout.HelpBox("문서 내용은 Tools ▸ UI Effect ▸ Generate Guide 로 코드에서 다시 구울 수 있습니다. 여기서 직접 고친 값은 재생성 시 덮어써집니다.", MessageType.None);
                DrawDefaultInspector();
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(8f);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSection(Readme.Section section)
        {
            EditorGUILayout.Space(12f);

            if (!string.IsNullOrEmpty(section.heading))
            {
                GUILayout.Label(section.heading, m_HeadingStyle);
                DrawRule();
                EditorGUILayout.Space(3f);
            }

            if (!string.IsNullOrEmpty(section.body))
            {
                DrawBody(section.body);
            }

            if (!string.IsNullOrEmpty(section.code))
            {
                EditorGUILayout.Space(4f);
                var height = m_CodeStyle.CalcHeight(new GUIContent(section.code), EditorGUIUtility.currentViewWidth - 56f);
                // 선택 가능(복사용) 라벨로 코드를 표시한다.
                EditorGUILayout.SelectableLabel(section.code, m_CodeStyle, GUILayout.Height(height + 4f));
            }

            if (!string.IsNullOrEmpty(section.note))
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox(section.note, MessageType.Info);
            }
        }

        // 본문을 줄 단위로 그린다. 빈 줄은 문단 간격, "• " 로 시작하면 행잉 인덴트 불릿으로 처리한다.
        private void DrawBody(string body)
        {
            var lines = body.Replace("\r\n", "\n").Split('\n');
            foreach (var line in lines)
            {
                if (line.Trim().Length == 0)
                {
                    EditorGUILayout.Space(5f);
                    continue;
                }

                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("• "))
                {
                    DrawBullet(trimmed.Substring(2));
                }
                else
                {
                    GUILayout.Label(line, m_BodyStyle);
                }
            }
        }

        // 불릿 기호와 본문을 가로로 나눠 그려, 줄이 넘칠 때 다음 줄이 기호가 아니라 본문 시작에 맞게 들여쓰이도록 한다.
        private void DrawBullet(string text)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(4f);
            GUILayout.Label("•", m_BulletStyle, GUILayout.Width(12f));
            GUILayout.Label(text, m_BodyStyle);
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawRule()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.35f));
        }
    }
}
