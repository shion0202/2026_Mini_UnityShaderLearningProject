# 서브모듈 커밋/푸시 체크리스트

학습용 레포(`2025_Mini_UnityShaderLearningProject`)의 서브모듈(`Assets/_ExAssets`) 변경을 반영할 때마다 이 순서대로 진행한다.
※ 기본 브랜치가 `main`이 아니라 `master`라면 명령의 `main`을 `master`로 바꿀 것.

---

## 절대 원칙 두 가지

1. **편집 전에 서브모듈을 `main` 브랜치 위로 올린다.**
   detached HEAD 상태에서 커밋하면 그 커밋이 어떤 브랜치에도 안 붙어 떠돌게 된다.
2. **서브모듈 push가 먼저, 공개 레포 push가 나중.**
   공개 레포가 가리키는 커밋이 private origin에 먼저 올라가 있어야 재클론 시 안 깨진다.

---

## 매 작업 사이클

### 0. 작업 시작 전 — 서브모듈이 main 위인지 확인 (Git Bash)
```bash
cd Assets/_ExAssets
git checkout main      # detached HEAD 방지 (이게 0순위)
git pull               # 다른 곳에서 올린 변경 먼저 받기 (좋은 습관)
```

### 1. 리소스 수정
`Assets/_ExAssets` 안의 파일을 Unity/탐색기에서 자유롭게 추가·수정·삭제.

### 2. 서브모듈 커밋 + push (Git Bash, 폴더는 `Assets/_ExAssets`)
```bash
git status             # 변경 내용 확인
git add .
git commit -m "변경 내용 설명"
git push origin main   # ← 서브모듈을 먼저 push
```

### 3. 공개 레포 포인터 커밋 + push
```bash
cd ../..               # 레포 루트로 이동
```
GitHub Desktop에서 `Assets/_ExAssets`가 변경된 "파일"(포인터)로 잡힌다 → 커밋 + push.
CLI로 하려면:
```bash
git add Assets/_ExAssets
git commit -m "Update _ExAssets submodule"
git push
```

---

## 검증 (가끔, 또는 중요한 변경 후)
```bash
git submodule status   # SHA 앞에 + / - 기호 없이 일치하면 정합 상태
```

---

## detached HEAD에 빠졌을 때 복구
커밋했는데 "브랜치명"이 커밋 해시(예: `bf27c79`)처럼 보이면 detached 상태다.
```bash
cd Assets/_ExAssets
git switch -c temp      # 지금 커밋을 임시 브랜치로 보존
git switch main
git merge temp          # main으로 합치기 (보통 fast-forward)
git push origin main
git branch -d temp
```
이후 레포 루트에서 포인터를 다시 커밋 + push.

---

## 공개 레포를 pull/클론한 직후 (다른 PC 등)
포인터가 바뀌었으면 서브모듈 작업 트리를 동기화한다.
```bash
git submodule update --init --recursive
```
이 직후 서브모듈은 **detached 상태**다 → 수정하려면 반드시 `git checkout main`부터 (위 0번).

---

## 한 줄 요약
> `checkout main` → 수정 → `add/commit` → **서브모듈 push** → 공개 레포 포인터 커밋 → **공개 레포 push**
