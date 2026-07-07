using System;
using System.IO;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 런타임 캐릭터 비주얼 확인용 뷰어 카메라 컨트롤러 (3D 툴 스타일).
///
/// 조작:
///  - 회전      : 우클릭 드래그 (Alt+좌클릭도 가능)
///  - 이동(팬)  : 휠클릭 드래그
///  - 줌        : 스크롤 휠 (기본: 커서 위치 기준 줌)
///  - FOV       : Ctrl + 스크롤 휠
///  - Shift     : 회전/팬 가속
///  - F         : 포커스 타깃 프레이밍 (화면에 꽉 차게)
///  - Home      : 카메라 리셋 (시작 상태로)
///  - T         : 턴테이블 자동 회전 토글
///  - 1~6       : 프리셋 뷰 (정면/후면/좌/우/상단/쿼터뷰)
///  - F12       : 스크린샷 (프로젝트 폴더 옆 Screenshots/)
///
/// CinemachineOrbitalFollow(Sphere)의 축 값을 이 스크립트가 직접 구동하므로
/// CinemachineInputAxisController는 붙이지 말 것.
/// </summary>
public class ViewerCameraController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("뷰어용 시네머신 카메라.")]
    [SerializeField] private CinemachineCamera viewerCamera;

    [Tooltip("뷰어 카메라의 Orbital Follow (Sphere 모드).")]
    [SerializeField] private CinemachineOrbitalFollow orbitalFollow;

    [Tooltip("카메라가 바라보는 피벗. 팬 조작 시 이 트랜스폼이 이동한다.")]
    [SerializeField] private Transform pivot;

    [Tooltip("포커스(F)·프리셋 뷰 기준이 되는 캐릭터 루트. 비워두면 F 키가 동작하지 않는다.")]
    [SerializeField] private Transform focusTarget;

    [Header("Orbit")]
    [Tooltip("마우스 1픽셀당 회전 각도(도).")]
    [SerializeField, Range(0.01f, 1f)] private float orbitSpeed = 0.2f;

    [Tooltip("체크 시 세로 회전 방향 반전.")]
    [SerializeField] private bool invertOrbitY = false;

    [Tooltip("세로 회전(피치) 허용 범위(도).")]
    [SerializeField] private Vector2 pitchRange = new Vector2(-89f, 89f);

    [Header("Pan")]
    [Tooltip("팬 속도 배율. 1이면 커서가 잡은 지점이 화면에서 1:1로 따라온다.")]
    [SerializeField, Range(0.1f, 3f)] private float panMultiplier = 1f;

    [Header("Zoom")]
    [Tooltip("스크롤 한 칸당 거리 배율. 1.15면 한 칸에 15%씩 가까워/멀어진다.")]
    [SerializeField, Range(1.01f, 1.5f)] private float zoomStepRatio = 1.15f;

    [Tooltip("카메라-피벗 거리 허용 범위.")]
    [SerializeField] private Vector2 distanceRange = new Vector2(0.2f, 30f);

    [Tooltip("체크 시 커서가 가리키는 지점을 향해 줌 (블렌더 스타일). 해제 시 피벗 중심 줌.")]
    [SerializeField] private bool zoomToCursor = true;

    [Header("FOV")]
    [Tooltip("Ctrl+스크롤 한 칸당 FOV 변화량(도).")]
    [SerializeField, Range(0.5f, 10f)] private float fovStep = 2f;

    [Tooltip("FOV 허용 범위(도).")]
    [SerializeField] private Vector2 fovRange = new Vector2(10f, 90f);

    [Header("Feel")]
    [Tooltip("입력 스무딩 시간(초). 0이면 즉시 반응. 0.05~0.12 권장.")]
    [SerializeField, Range(0f, 0.5f)] private float smoothTime = 0.08f;

    [Tooltip("Shift를 누른 동안의 회전/팬 속도 배율.")]
    [SerializeField, Range(1f, 10f)] private float fastMultiplier = 3f;

    [Header("Turntable")]
    [Tooltip("턴테이블 자동 회전 속도(도/초). 음수면 반대 방향.")]
    [SerializeField, Range(-180f, 180f)] private float turntableSpeed = 30f;

    [Header("Framing")]
    [Tooltip("포커스(F) 시 바운즈 대비 여유 배율. 1이면 꽉 참.")]
    [SerializeField, Range(1f, 2f)] private float framingPadding = 1.15f;

    [Header("Screenshot")]
    [Tooltip("스크린샷 해상도 배율 (1~4).")]
    [SerializeField, Range(1, 4)] private int screenshotSuperSize = 2;

    [Header("Keys")]
    [SerializeField] private Key focusKey = Key.F;
    [SerializeField] private Key resetKey = Key.Home;
    [SerializeField] private Key turntableKey = Key.T;
    [SerializeField] private Key screenshotKey = Key.F12;

    // ── 목표값(입력이 직접 갱신) / 현재값(스무딩되어 시네머신에 반영) ──
    private float targetYaw, currentYaw;
    private float targetPitch, currentPitch;
    private float targetDistance, currentDistance;
    private float targetFov, currentFov;
    private Vector3 targetPivotPos, currentPivotPos;

    // 리셋(Home)용 시작 상태
    private float initialYaw, initialPitch, initialDistance, initialFov;
    private Vector3 initialPivotPos;

    private bool turntableActive;
    private bool orbitDragging, panDragging;
    // 드래그 시작점이 UI 위였다면 버튼을 뗄 때까지 해당 드래그를 무시한다.
    private bool orbitBlockedByUI, panBlockedByUI;

    private Camera outputCamera;

    /// <summary>에디터 리그 빌더에서 참조를 주입할 때 사용.</summary>
    public void SetupReferences(CinemachineCamera cam, CinemachineOrbitalFollow orbital, Transform pivotTransform, Transform focus)
    {
        viewerCamera = cam;
        orbitalFollow = orbital;
        pivot = pivotTransform;
        focusTarget = focus;
    }

    private void Start()
    {
        if (viewerCamera == null || orbitalFollow == null || pivot == null)
        {
            Debug.LogError("[ViewerCameraController] 참조가 비어 있습니다. Tools > NeoMantra > 뷰어 카메라 리그 생성 메뉴로 리그를 만들어 주세요.", this);
            enabled = false;
            return;
        }

        outputCamera = Camera.main;
        if (outputCamera == null)
            outputCamera = FindFirstObjectByType<Camera>();

        // 씬에 배치된 초기 상태를 그대로 시작/리셋 기준으로 삼는다.
        targetYaw = currentYaw = initialYaw = orbitalFollow.HorizontalAxis.Value;
        targetPitch = currentPitch = initialPitch = orbitalFollow.VerticalAxis.Value;
        targetDistance = currentDistance = initialDistance = orbitalFollow.Radius;
        targetFov = currentFov = initialFov = viewerCamera.Lens.FieldOfView;
        targetPivotPos = currentPivotPos = initialPivotPos = pivot.position;
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;
        var keyboard = Keyboard.current;

        bool shift = keyboard != null && keyboard.shiftKey.isPressed;
        bool ctrl = keyboard != null && keyboard.ctrlKey.isPressed;
        bool alt = keyboard != null && keyboard.altKey.isPressed;
        float speedMul = shift ? fastMultiplier : 1f;

        HandleDragStates(mouse, alt);
        HandleOrbit(mouse, speedMul);
        HandlePan(mouse, speedMul);
        HandleScroll(mouse, ctrl);
        if (keyboard != null) HandleKeys(keyboard);
        HandleTurntable();

        ApplySmoothedState();
    }

    // ────────────────────────────── 입력 처리 ──────────────────────────────

    private void HandleDragStates(Mouse mouse, bool alt)
    {
        bool orbitPressed = mouse.rightButton.isPressed || (alt && mouse.leftButton.isPressed);
        bool panPressed = mouse.middleButton.isPressed;

        // 드래그 시작 프레임에 UI 위 여부 판정
        if (orbitPressed && !orbitDragging)
            orbitBlockedByUI = IsPointerOverUI();
        if (panPressed && !panDragging)
            panBlockedByUI = IsPointerOverUI();

        orbitDragging = orbitPressed;
        panDragging = panPressed;
    }

    private void HandleOrbit(Mouse mouse, float speedMul)
    {
        if (!orbitDragging || orbitBlockedByUI) return;

        Vector2 delta = mouse.delta.ReadValue();
        float pitchSign = invertOrbitY ? -1f : 1f;

        targetYaw += delta.x * orbitSpeed * speedMul;
        targetPitch = Mathf.Clamp(
            targetPitch + delta.y * orbitSpeed * speedMul * pitchSign,
            pitchRange.x, pitchRange.y);

        // 턴테이블 등으로 각도가 무한히 커지지 않게 주기적으로 정규화
        if (Mathf.Abs(targetYaw) > 540f)
        {
            float shift = 360f * Mathf.Sign(targetYaw);
            targetYaw -= shift;
            currentYaw -= shift;
        }
    }

    private void HandlePan(Mouse mouse, float speedMul)
    {
        if (!panDragging || panBlockedByUI) return;

        Vector2 delta = mouse.delta.ReadValue();

        // 현재 거리·FOV 기준으로 1픽셀당 월드 이동량을 계산 → 커서가 잡은 지점이 그대로 따라온다.
        float worldPerPixel = 2f * currentDistance * Mathf.Tan(currentFov * 0.5f * Mathf.Deg2Rad) / Screen.height;
        Quaternion camRot = Quaternion.Euler(currentPitch, currentYaw, 0f);

        targetPivotPos += (camRot * Vector3.right * -delta.x + camRot * Vector3.up * -delta.y)
                          * (worldPerPixel * panMultiplier * speedMul);
    }

    private void HandleScroll(Mouse mouse, bool ctrl)
    {
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(scroll, 0f) || IsPointerOverUI()) return;

        // Windows 마우스는 한 칸에 ±120을 주는 경우가 있어 스텝 단위로 정규화
        if (Mathf.Abs(scroll) > 3f) scroll /= 120f;

        if (ctrl)
        {
            targetFov = Mathf.Clamp(targetFov - scroll * fovStep, fovRange.x, fovRange.y);
            return;
        }

        float oldDistance = targetDistance;
        targetDistance = Mathf.Clamp(
            targetDistance * Mathf.Pow(zoomStepRatio, -scroll),
            distanceRange.x, distanceRange.y);

        // 커서 기준 줌: 커서가 가리키는 지점이 화면에서 고정되도록 피벗을 보정
        if (zoomToCursor && outputCamera != null && oldDistance > Mathf.Epsilon)
        {
            float scale = targetDistance / oldDistance;
            Ray ray = outputCamera.ScreenPointToRay(mouse.position.ReadValue());
            var plane = new Plane(-outputCamera.transform.forward, targetPivotPos);
            if (plane.Raycast(ray, out float enter))
            {
                Vector3 cursorPoint = ray.GetPoint(enter);
                targetPivotPos = cursorPoint + (targetPivotPos - cursorPoint) * scale;
            }
        }
    }

    private void HandleKeys(Keyboard keyboard)
    {
        if (keyboard[resetKey].wasPressedThisFrame) ResetView();
        if (keyboard[focusKey].wasPressedThisFrame) FrameFocusTarget();
        if (keyboard[turntableKey].wasPressedThisFrame) turntableActive = !turntableActive;
        if (keyboard[screenshotKey].wasPressedThisFrame) CaptureScreenshot();

        // 프리셋 뷰: 1 정면 / 2 후면 / 3 좌측 / 4 우측 / 5 상단 / 6 쿼터뷰
        if (keyboard.digit1Key.wasPressedThisFrame) SetPresetView(180f, 0f);
        if (keyboard.digit2Key.wasPressedThisFrame) SetPresetView(0f, 0f);
        if (keyboard.digit3Key.wasPressedThisFrame) SetPresetView(90f, 0f);
        if (keyboard.digit4Key.wasPressedThisFrame) SetPresetView(-90f, 0f);
        if (keyboard.digit5Key.wasPressedThisFrame) SetPresetView(180f, 89f);
        if (keyboard.digit6Key.wasPressedThisFrame) SetPresetView(180f + 30f, 20f);
    }

    private void HandleTurntable()
    {
        // 사용자가 직접 회전 중일 때는 턴테이블 일시 정지
        if (!turntableActive || (orbitDragging && !orbitBlockedByUI)) return;
        targetYaw += turntableSpeed * Time.deltaTime;
    }

    // ────────────────────────────── 카메라 기능 ──────────────────────────────

    /// <summary>시작 상태로 복원 (버튼 OnClick 연결 가능).</summary>
    public void ResetView()
    {
        turntableActive = false;
        targetYaw = currentYaw + Mathf.DeltaAngle(currentYaw, initialYaw); // 최단 경로로 회전
        targetPitch = initialPitch;
        targetDistance = initialDistance;
        targetFov = initialFov;
        targetPivotPos = initialPivotPos;
    }

    /// <summary>포커스 타깃의 렌더러 바운즈가 화면에 꽉 차도록 프레이밍 (버튼 OnClick 연결 가능).</summary>
    public void FrameFocusTarget()
    {
        if (focusTarget == null)
        {
            Debug.LogWarning("[ViewerCameraController] focusTarget이 비어 있어 프레이밍할 수 없습니다.", this);
            return;
        }

        var renderers = focusTarget.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float aspect = outputCamera != null ? outputCamera.aspect : 16f / 9f;
        float halfFovV = currentFov * 0.5f * Mathf.Deg2Rad;
        float halfFovH = Mathf.Atan(Mathf.Tan(halfFovV) * aspect);
        float halfFov = Mathf.Min(halfFovV, halfFovH);
        float boundsRadius = bounds.extents.magnitude;

        targetPivotPos = bounds.center;
        targetDistance = Mathf.Clamp(
            boundsRadius / Mathf.Sin(halfFov) * framingPadding,
            distanceRange.x, distanceRange.y);
    }

    /// <summary>캐릭터 기준 각도로 프리셋 뷰 이동. yawOffset 180 = 정면.</summary>
    public void SetPresetView(float yawOffset, float pitch)
    {
        float baseYaw = focusTarget != null ? focusTarget.eulerAngles.y : 0f;
        float desiredYaw = baseYaw + yawOffset;

        targetYaw = currentYaw + Mathf.DeltaAngle(currentYaw, desiredYaw); // 최단 경로로 회전
        targetPitch = Mathf.Clamp(pitch, pitchRange.x, pitchRange.y);
    }

    /// <summary>스크린샷 저장 (버튼 OnClick 연결 가능).</summary>
    public void CaptureScreenshot()
    {
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        ScreenCapture.CaptureScreenshot(path, screenshotSuperSize);
        Debug.Log($"[ViewerCameraController] 스크린샷 저장: {path}");
    }

    // ────────────────────────────── 적용 ──────────────────────────────

    private void ApplySmoothedState()
    {
        // 프레임레이트 독립 지수 스무딩
        float k = smoothTime <= 0f ? 1f : 1f - Mathf.Exp(-Time.deltaTime / smoothTime);

        currentYaw = Mathf.Lerp(currentYaw, targetYaw, k);
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, k);
        currentDistance = Mathf.Lerp(currentDistance, targetDistance, k);
        currentFov = Mathf.Lerp(currentFov, targetFov, k);
        currentPivotPos = Vector3.Lerp(currentPivotPos, targetPivotPos, k);

        orbitalFollow.HorizontalAxis.Value = Mathf.Repeat(currentYaw + 180f, 360f) - 180f;
        orbitalFollow.VerticalAxis.Value = currentPitch;
        orbitalFollow.Radius = currentDistance;
        viewerCamera.Lens.FieldOfView = currentFov;
        pivot.position = currentPivotPos;
    }

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
