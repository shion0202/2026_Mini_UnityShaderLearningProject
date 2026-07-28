# UI Effect 시스템 사용 가이드

`coffee.ui-effect`(5.11.2) 패키지 위에 올린, 2D UI 셰이더 이펙트를 런타임에서 제어하기 위한 스크립트 묶음입니다.
모든 타입은 `namespace UIEffectControl` 안에 있으므로 연동 코드 상단에 `using UIEffectControl;` 를 추가하세요.

---

## 1. 전체 구조

세 계층으로 나뉩니다.

| 계층 | 타입 | 역할 |
|---|---|---|
| **실행기(서비스)** | `UIEffectManager` : `IUIEffectService` | UI 오브젝트를 받아 그 하위 컨트롤러를 구동. UI 목록은 소유하지 않음 |
| **컨트롤러** | `UIEffectController` | 개별 UI 오브젝트에 붙어 실제 `UIEffect` / `UIEffectTweener` 를 제어하는 파사드 |
| **프리셋 에셋** | `UIEffectPreset`(패키지), `UIEffectTweenerPreset` / `UIEffectTweenerPresetLibrary` | "이런 이펙트/트윈"을 이름으로 재사용 |

기본 호출 흐름은 다음과 같습니다.

```
외부 로직 → UI Manager(팀 소유, UI 목록 관리) → IUIEffectService(GameObject 전달) → UIEffectController
```

**핵심 원칙:** UI 의 생성·삭제·목록은 팀의 UI Manager 가 소유합니다. 이펙트 서비스는 UI 목록을 들지 않고,
"이미 찾아낸 UI 의 GameObject" 를 받아 **그 오브젝트와 자식에 붙은 모든 `UIEffectController`** 를 구동만 합니다.
따라서 UI 하나에 이펙트 대상이 여러 개여도 루트 오브젝트 하나만 넘기면 됩니다.

> `SampleUIManager.cs` 가 이 흐름(uiId → GameObject 소유 후 서비스에 위임)의 참조 구현입니다. 실제 UI Manager 를 만들 때 형태를 참고하세요.

---

## 2. 씬 세팅

1. 씬에 `UIEffectManager` 컴포넌트를 하나 배치하고, **Tweener Preset Library** 에셋을 지정합니다. (없으면 트위너 프리셋을 이름으로 못 찾습니다.)
2. 이펙트를 걸 각 UI 오브젝트에 `UIEffect`(패키지) + `UIEffectController` 를 붙입니다. 트윈이 필요하면 같은 오브젝트에 `UIEffectTweener` 도 추가합니다.
3. **`UIEffect` / `UIEffectController` / `UIEffectTweener` 는 반드시 같은 GameObject 에 둡니다.** (컨트롤러가 트위너를 같은 오브젝트에서만 찾습니다.)

---

## 3. 서비스(매니저)로 제어하기 — 권장 경로

`UIEffectManager.Instance` 로 접근하거나 `IUIEffectService` 참조를 주입받아 사용합니다. 모든 메서드는 **대상 UI 의 루트 `GameObject`** 를 받습니다.

```csharp
using UIEffectControl;

// UI Manager 가 자기 목록에서 찾은 UI 의 GameObject 를 넘긴다.
IUIEffectService fx = UIEffectManager.Instance;   // 씬에 매니저가 없으면 null
if (fx != null)
{
    fx.ApplyPreset(hpBarRoot, "Trauma");   // 프리셋 교체(전체 교체)
    fx.PlayForward(hpBarRoot);             // 트윈 정방향 재생
}
```

반환값 `bool` 은 "대상 컨트롤러를 하나 이상 처리했는지" 입니다. 실패 시 콘솔에 원인 경고가 남습니다.

### 프리셋 적용

| 메서드 | 동작 |
|---|---|
| `ApplyPreset(root, name, append=false)` | 이펙트 프리셋 적용. `append=true` 면 현재 위에 겹침(None 아닌 필터만 덮어씀), `false` 면 전체 교체 |
| `ApplyTweenerPreset(root, name)` | 트위너 설정 적용. 프리셋의 재생 정책에 따라 적용 즉시 재생될 수 있음 |

프리셋 이름은 **컨트롤러 로컬 목록 → 중앙 레지스트리(트위너는 라이브러리)** 순으로 조회됩니다.

### 끄기 3종

| 메서드 | 동작 |
|---|---|
| `Clear(root)` | **이펙트 + 트윈 둘 다** 끄기 (기본 경로) |
| `ClearEffect(root)` | 이펙트 값만 초기화 (도는 트윈은 유지) |
| `StopTween(root)` | 트윈만 정지 |

### 트윈 재생 제어 (온디맨드)

프리셋의 "적용=재생"(일회성)으로 표현 못 하는 **양방향·반복 제어**용입니다. 호버 토글, 팝업 개폐 등에 씁니다.

```csharp
// 버튼 호버 인/아웃 토글 예시
public void OnPointerEnter() => UIEffectManager.Instance?.PlayForward(gameObject);
public void OnPointerExit()  => UIEffectManager.Instance?.PlayReverse(gameObject);
```

`PlayForward(root, resetTime=true)` · `PlayReverse(root, resetTime=true)` · `TogglePlay(root)` · `StopTween(root)` · `PauseTween(root, pause)`

> **주의:** 이 재생 상태는 오브젝트를 껐다 켜면 초기화됩니다. "활성화될 때마다 자동 재생"이 필요하면 재생 패스스루가 아니라 **트위너 프리셋의 Play On Enable** 로 설정하세요.

### 존재 확인

`HasPreset(root, name)` / `HasTweenerPreset(root, name)` 로 적용 없이 사용 가능 여부만 확인할 수 있습니다. (GameObject 없이 전역 등록만 볼 땐 이름만 받는 오버로드 사용.)

---

## 4. 컨트롤러로 직접 제어하기

특정 UI 를 참조로 들고 있을 때는 `UIEffectController` 를 직접 써도 됩니다. 서비스 메서드는 결국 이 컨트롤러들을 호출합니다.

```csharp
var controller = popup.GetComponent<UIEffectController>();
controller.LoadPreset("Awaken");   // 로컬 목록 → 중앙 레지스트리 순 조회
controller.PlayForward();
```

### 프리셋 전환

`LoadPreset(id)` · `LoadPreset(index)` · `AppendPreset(id)` · `LoadPresetAsset(preset, append)` · `LoadRegisteredPreset(name, append)` 등을 제공합니다. 대부분 로컬 목록을 먼저 보고, 없으면 중앙 레지스트리로 폴백합니다.

### 개별 파라미터 세터

`SetColorIntensity`, `SetTransitionRate`, `SetGradationRotation` 등 **자주 쓰는 이펙트 값을 float 하나 또는 Color 하나만 받는 세터로 노출**한 그룹입니다.
인자가 단순해 **버튼/슬라이더의 UnityEvent(예: `OnValueChanged`)에 인스펙터에서 바로 바인딩**하거나, 코드에서 값을 실시간 조정할 때 씁니다.

```csharp
// 슬라이더나 게임 로직에서 이펙트 강도를 실시간으로 움직일 때
controller.SetColorIntensity(0.5f);
controller.SetTransitionRate(progress);   // 예: 디졸브 진행도 0~1

// 여기 없는 나머지 속성(약 60여 개)은 Effect 로 직접 접근
controller.Effect.gradationRotation = 90f;
```

즉 이 세터들은 "이펙트의 특정 값 하나를 지금 이 값으로 세팅"하는 얇은 래퍼이며, 세밀한 제어가 필요하면 `Effect`(`UIEffect`) / `Tweener`(`UIEffectTweener`) 프로퍼티로 원본 컴포넌트에 직접 접근하면 됩니다.

> 컨트롤러의 모든 조작 메서드는 `virtual` 이라, 프로젝트별 동작이 필요하면 상속해서 오버라이드할 수 있습니다.

---

## 5. 프리셋 에셋 만들기

- **이펙트 프리셋(`UIEffectPreset`)**: 패키지 기능입니다. 공용은 `Project Settings ▸ UI Effect ▸ Runtime Presets` 에 등록해 이름으로 부르고, 특정 UI 전용 오버라이드는 컨트롤러 인스펙터의 `Presets` 목록에 등록합니다(같은 이름이면 로컬이 우선).
- **트위너 프리셋(`UIEffectTweenerPreset`)**: 패키지가 트위너 프리셋을 지원하지 않아 별도로 만든 에셋입니다. 공용은 `UIEffectTweenerPresetLibrary` 에 모아 매니저에 물리고, 전용은 컨트롤러의 `Tweener Presets` 목록에 등록합니다.
  - 값은 손으로 옮기지 않습니다. 트위너에서 원하는 느낌을 만든 뒤, **컨트롤러 인스펙터 하단 "트위너 프리셋 도구 ▸ 현재 트위너 값 → 새 프리셋 저장"** 버튼으로 구우면 됩니다.
  - 트위너 프리셋은 트윈 설정 외에 **Play On Apply**(적용 직후 재생 방식)와 **Play On Enable**(활성화 시 재생 방식)을 함께 들고 있어, 적용 시 컨트롤러의 재생 정책까지 한 번에 정합니다.

---

## 6. 알아둘 동작

- **대상 범위**: 서비스에 넘긴 GameObject 의 **자기 + 모든 자식**에 붙은 컨트롤러가 대상입니다. 트위너 없는 컨트롤러는 재생 호출에서 조용히 건너뜁니다.
- **매니저 부재**: `UIEffectManager.Instance` 는 씬에 매니저가 없으면 경고 후 `null` 을 반환합니다(자동 생성하지 않음). 호출 전 null 체크를 권장합니다.
- **기본 정지**: 컨트롤러의 `Play On Enable` 기본값은 `None` 이라, 활성화만으로는 트윈이 돌지 않고 이펙트의 정적 값이 유지됩니다. 켜지자마자 움직여야 하는 경우에만 `Forward`/`Reverse` 로 두세요.
- **씬 유지**: 매니저는 UI 참조를 들지 않으므로 씬 전환에 안전합니다. 씬을 넘겨 유지하려면 `Don't Destroy On Load` 를 켜고 루트 오브젝트로 두세요.
