# 서브모듈 첫 생성 체크리스트

공개 레포 안에 유료/대용량 리소스용 **private 서브모듈**을 처음 붙일 때 이 순서대로 진행한다.
예시 경로는 학습 레포 기준(`2025_Mini_UnityShaderLearningProject`, 서브모듈 `Assets/_ExAssets`)이며, 실제 경로/이름으로 바꿔서 사용.
※ 기본 브랜치가 `master`라면 명령의 `main`을 `master`로.

---

## 큰 그림

공개 레포에는 결국 두 가지만 들어간다 — **`.gitmodules`**(서브모듈 경로·URL 명세)와 **포인터**(private 레포의 특정 커밋 SHA).
실제 리소스 내용물은 private 레포에만 있고, 공개 레포는 "여기에 저 private 레포의 이 커밋을 붙여라"라는 지시만 갖는다.

---

## 0. 사전 준비

- [ ] 공개 레포가 로컬에 클론돼 있다.
- [ ] private 레포를 GitHub에 생성해뒀다 (비어 있어도, README만 있어도 무방).

---

## 1. 레포 루트 확인 (가장 헷갈리는 지점)

서브모듈 명령은 **레포 루트(`.git`이 있는 폴더)** 에서 실행해야 한다.
Unity 프로젝트가 하위 폴더(예: `Branch/`)에 들어가 있으면 레포 루트와 Assets 폴더가 어긋나니, 의심되면 확인:

```bash
git rev-parse --show-toplevel    # 출력 경로가 레포 루트
```

- 중간 폴더 없이 `프로젝트폴더 > Assets` 구조면 → 레포 루트 = Assets가 든 폴더.
- `프로젝트폴더 > Branch > Assets` 구조면 → 레포 루트는 `Branch`보다 한 단계 위(`프로젝트폴더`).

---

## 2. 서브모듈 추가 (Git Bash, 레포 루트에서)

GitHub Desktop은 서브모듈 추가를 못 하므로 Git Bash로 한다.

```bash
git submodule add https://github.com/계정/private-레포.git Assets/_ExAssets
```

- 이 명령이 private 레포를 그 폴더로 클론하고, `.gitmodules`와 포인터를 자동 stage한다.
- **추가 직후의 서브모듈은 `main` 브랜치 위(정상 상태)** 다. detached HEAD 문제는 *나중에 새로 클론한 뒤*에만 생긴다.

---

## 3. 리소스 넣기

`Assets/_ExAssets` 폴더 안으로 유료/대용량 리소스를 복사해 넣는다.
(private 레포에 이미 리소스가 들어 있었다면 이 단계는 건너뛴다.)

> ⚠️ **100 MB 넘는 파일 주의.** GitHub은 개별 파일 100 MB 초과를 거부한다(push 통째 거부).
> - 필요 없는 대용량 파일이면 애초에 넣지 않거나 제외한다.
> - 꼭 보관해야 하면 Git LFS로 추적한다: `git lfs install` → `git lfs track "*.tga"` 등.

---

## 4. 서브모듈 커밋 + push (Git Bash, `Assets/_ExAssets` 안에서)

```bash
cd Assets/_ExAssets
git status                 # 들어온 파일 확인
git add .
git commit -m "Add external assets"
git push origin main       # ← 서브모듈을 먼저 push
cd ../..                   # 레포 루트로 복귀
```

**서브모듈 push가 반드시 먼저.** 공개 레포가 가리킬 커밋이 private origin에 먼저 존재해야 재클론 시 안 깨진다.

---

## 5. 공개 레포 포인터 커밋 + push (GitHub Desktop 가능)

레포 루트에서 `.gitmodules`와 `Assets/_ExAssets`(포인터)가 변경으로 잡힌다.
GitHub Desktop에서 그대로 커밋·push하거나, CLI로:

```bash
git add .gitmodules Assets/_ExAssets
git commit -m "Add _ExAssets submodule"
git push
```

---

## 6. 검증

```bash
git submodule status       # SHA가 정상 표시되는지
```

가능하면 다른 위치에 새로 클론해서 서브모듈까지 정상적으로 딸려오는지 확인:

```bash
git clone --recurse-submodules https://github.com/계정/공개-레포.git
```

(LFS를 썼다면, 클론하는 PC에도 `git lfs install`이 돼 있어야 실제 파일로 받아진다.)

---

## Unity 관련 체크

- `Assets/_ExAssets` 폴더에 대한 `.meta`는 Unity가 **공개 레포 쪽에** 생성한다. 정상이니 공개 레포에 같이 커밋.
- 리소스 내부 파일들의 `.meta`는 private 서브모듈 안에 들어간다.
- 공개 레포 `.gitignore`가 `Assets/_ExAssets` 경로를 무시하지 않는지 확인.

---

## 한 줄 요약
> private 레포 생성 → (레포 루트에서) `submodule add` → 리소스 넣기 → **서브모듈 커밋·push** → 공개 레포 포인터 커밋·push → 클론 검증
