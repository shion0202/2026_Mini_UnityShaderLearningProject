using System;
using System.Collections.Generic;
using UnityEngine;

namespace UIEffectControl
{
    // 유니티 인스펙터에서 서식 있게 읽히는 문서용 에셋. 렌더링은 ReadmeEditor 가 담당한다.
    // 내용은 Tools ▸ UI Effect ▸ Generate Guide 메뉴(UIEffectGuideBuilder)로 코드에서 굽는다.
    [CreateAssetMenu(fileName = "UIEffectGuide", menuName = "UI Effect/Readme")]
    public class Readme : ScriptableObject
    {
        [Tooltip("문서 제목.")]
        public string title;

        // 문서를 이루는 한 덩어리. 필요한 필드만 채우면 그 부분만 렌더된다.
        [Serializable]
        public class Section
        {
            [Tooltip("소제목(선택). 비우면 제목 줄 없이 본문만 나온다.")]
            public string heading;

            [Tooltip("본문(선택). <b>굵게</b> 같은 리치 텍스트 태그를 쓸 수 있다.")]
            [TextArea(1, 20)] public string body;

            [Tooltip("코드 예시(선택). 리치 텍스트로 해석하지 않아 <T> 같은 제네릭도 그대로 보인다.")]
            [TextArea(1, 20)] public string code;

            [Tooltip("강조 노트(선택). HelpBox 로 표시된다.")]
            [TextArea(1, 6)] public string note;
        }

        [Tooltip("위에서 아래로 순서대로 렌더되는 섹션 목록.")]
        public List<Section> sections = new List<Section>();
    }
}
