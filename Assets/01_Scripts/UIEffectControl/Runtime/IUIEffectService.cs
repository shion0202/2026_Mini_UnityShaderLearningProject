using UnityEngine;

namespace UIEffectControl
{
    // UI 이펙트 적용을 요청하는 쪽(예: 팀의 UI Manager)이 바라보는 계약.
    //
    // 흐름: 외부 → UI Manager → (이 인터페이스) → 각 UI 의 UIEffectController.
    // UI 목록/생성/삭제/수명은 전적으로 UI Manager 가 소유한다. 이 서비스는 UI 목록을
    // 들고 있지 않으며, "이미 찾아낸 UI 오브젝트" 를 받아 그 하위의 컨트롤러를 구동만 한다.
    //
    // uiRoot 로는 이펙트를 걸 UI 의 루트 GameObject 를 넘긴다.
    // 서비스는 그 오브젝트와 자식에서 UIEffectController 를 모두 찾아 함께 적용한다.
    // (UI 하나에 이펙트 대상이 여러 개인 경우까지 이 방식으로 커버된다.)
    public interface IUIEffectService
    {
        // uiRoot 하위의 이펙트에 프리셋을 적용한다. append=true 면 겹쳐 적용, false 면 전체 교체.
        // 하나 이상 적용되면 true.
        bool ApplyPreset(GameObject uiRoot, string presetName, bool append = false);

        // uiRoot 하위의 트위너에 트위너 프리셋을 적용한다. 프리셋의 재생 정책에 따라 즉시 재생될 수 있다.
        // 조회 순서: 컨트롤러 로컬 목록 → 공용 트위너 라이브러리. 하나 이상 적용되면 true.
        bool ApplyTweenerPreset(GameObject uiRoot, string presetName);

        // 끄기 3종:
        //  - Clear       : 이펙트 + 트윈 둘 다 끄기(기본 경로).
        //  - ClearEffect : 이펙트만 끄기(도는 트윈은 유지).
        //  - StopTween   : 트윈만 정지(아래 재생 제어 그룹에 있음).
        // 대상 컨트롤러가 하나라도 있으면 true.

        // 이펙트를 기본값으로 되돌리고 트윈도 정지한다.
        bool Clear(GameObject uiRoot);

        // 이펙트 값만 기본값으로 되돌린다(도는 트윈은 그대로).
        bool ClearEffect(GameObject uiRoot);

        // ── 트윈 재생 제어 ─────────────────────────────────────────────
        // 이미 적용된 이펙트의 진행도(rate 0→1)를 온디맨드로 구동한다.
        // 프리셋의 재생 정책(적용=재생, 일회성)으로 표현 못 하는 호버 토글·팝업 개폐 등에 쓴다.
        // 대상 uiRoot 하위에서 UIEffectTweener 를 가진 컨트롤러를 하나 이상 구동하면 true.

        // 정방향(0→1) 재생. resetTime=true 면 처음부터.
        bool PlayForward(GameObject uiRoot, bool resetTime = true);

        // 역방향(1→0) 재생. resetTime=true 면 끝에서부터.
        bool PlayReverse(GameObject uiRoot, bool resetTime = true);

        // 현재 진행 방향의 반대로 재생(정방향↔역방향). 호버 인/아웃 토글에 사용(시간 유지).
        bool TogglePlay(GameObject uiRoot);

        // 트윈을 정지하고 시간을 리셋한다.
        bool StopTween(GameObject uiRoot);

        // 트윈 일시정지/재개.
        bool PauseTween(GameObject uiRoot, bool pause);

        // uiRoot 하위의 컨트롤러가 이 프리셋을 쓸 수 있는지 확인한다(로컬 → 중앙 레지스트리). 적용하지 않는다.
        bool HasPreset(GameObject uiRoot, string presetName);

        // uiRoot 하위의 컨트롤러가 이 트위너 프리셋을 쓸 수 있는지 확인한다(로컬 → 공용 라이브러리). 적용하지 않는다.
        bool HasTweenerPreset(GameObject uiRoot, string presetName);
    }
}
