using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 뷰어 카메라 리그(피벗 + CinemachineCamera + ViewerCameraController)를 씬에 자동 구성하는 메뉴.
/// 캐릭터 루트를 선택한 상태로 실행하면 포커스 타깃 지정과 초기 프레이밍까지 자동으로 해준다.
/// </summary>
public static class ViewerCameraRigBuilder
{
    [MenuItem("Tools/NeoMantra/뷰어 카메라 리그 생성")]
    public static void CreateRig()
    {
        // Main Camera에 CinemachineBrain이 없으면 추가
        Camera mainCam = Camera.main;
        if (mainCam == null)
            mainCam = Object.FindFirstObjectByType<Camera>();

        if (mainCam == null)
        {
            Debug.LogError("[ViewerCameraRigBuilder] 씬에 카메라가 없습니다. Main Camera를 먼저 만들어 주세요.");
            return;
        }

        if (!mainCam.TryGetComponent(out CinemachineBrain _))
            Undo.AddComponent<CinemachineBrain>(mainCam.gameObject);

        // 선택된 오브젝트가 있으면 포커스 타깃으로 사용 (렌더러 바운즈 중심을 피벗으로)
        Transform focus = Selection.activeTransform;
        Vector3 pivotPos = new Vector3(0f, 1f, 0f);
        float radius = 3.5f;

        if (focus != null)
        {
            var renderers = focus.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);

                pivotPos = bounds.center;
                radius = Mathf.Max(1f, bounds.extents.magnitude * 2.5f);
            }
            else
            {
                pivotPos = focus.position + Vector3.up;
            }
        }

        // 리그 계층 구성: ViewerCameraRig ─ CameraPivot / CM_ViewerCamera
        var root = new GameObject("ViewerCameraRig");
        Undo.RegisterCreatedObjectUndo(root, "Create Viewer Camera Rig");

        var pivot = new GameObject("CameraPivot").transform;
        pivot.SetParent(root.transform);
        pivot.position = pivotPos;

        var camGo = new GameObject("CM_ViewerCamera");
        camGo.transform.SetParent(root.transform);

        var vcam = camGo.AddComponent<CinemachineCamera>();
        var orbital = camGo.AddComponent<CinemachineOrbitalFollow>();
        camGo.AddComponent<CinemachineHardLookAt>();

        vcam.Follow = pivot;
        vcam.LookAt = pivot;
        vcam.Lens.FieldOfView = 35f; // 캐릭터 확인용으로 왜곡이 적은 화각

        orbital.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
        orbital.Radius = radius;
        orbital.HorizontalAxis.Value = focus != null ? focus.eulerAngles.y + 180f : 180f; // 정면에서 시작
        orbital.HorizontalAxis.Wrap = true;
        orbital.HorizontalAxis.Range = new Vector2(-180f, 180f);
        orbital.VerticalAxis.Value = 5f;
        orbital.VerticalAxis.Wrap = false;
        orbital.VerticalAxis.Range = new Vector2(-89f, 89f);

        // 캐릭터 회전과 무관하게 월드 기준으로 궤도 유지, 스무딩은 컨트롤러가 담당하므로 댐핑 0
        orbital.TrackerSettings.BindingMode = BindingMode.WorldSpace;
        orbital.TrackerSettings.PositionDamping = Vector3.zero;

        var controller = root.AddComponent<ViewerCameraController>();
        controller.SetupReferences(vcam, orbital, pivot, focus);

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        Debug.Log($"[ViewerCameraRigBuilder] 뷰어 카메라 리그 생성 완료. 포커스 타깃: {(focus != null ? focus.name : "없음 — 인스펙터에서 지정 필요")}");
    }
}
