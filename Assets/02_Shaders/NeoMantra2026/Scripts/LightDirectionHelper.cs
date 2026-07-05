using UnityEngine;

namespace NeoMantra2026.Scripts
{
    // 커스텀 라이트의 '오브젝트 기준 방향'용 프레임 공급 헬퍼.
    // referenceProxy(캐릭터 정면 정렬 Transform)의 축을 셰이더에 넣어, _FakeLightEuler 각도를
    // 그 축 기준으로 재해석시킨다(= 캐릭터가 회전하면 빛도 같은 상대각으로 따라 회전).
    // 컴포넌트 비활성 시 항등축 복원 → 월드 기준으로 되돌아감. SDFHelper와 동일 구조.
    [ExecuteAlways]
    [AddComponentMenu("NeoMantra2026/Light Direction Helper")]
    public class LightDirectionHelper : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, Tooltip("캐릭터 정면 프레임 Transform(리그 보정용 정렬 빈 오브젝트 권장). forward/right/up이 커스텀 라이트 각도의 기준 축이 됨. 비우면 자신.")] private Transform referenceProxy;
        [SerializeField, Tooltip("대상 렌더러. 비우면 자식에서 탐색.")] private Renderer targetRenderer;
        [SerializeField, Tooltip("렌더러에서 적용할 머티리얼 번호.")] private int materialIndex = 0;

        [Header("Debug")]
        [SerializeField, Tooltip("기준 축(파랑=forward/빨강=right/초록=up) 확인용.")] private bool drawGizmo = true;

        private const string ForwardProperty = "_FakeLightRefForward";
        private const string RightProperty   = "_FakeLightRefRight";
        private const string UpProperty      = "_FakeLightRefUp";
        private static readonly Vector4 DefaultForward = new Vector4(0f, 0f, 1f, 0f);
        private static readonly Vector4 DefaultRight   = new Vector4(1f, 0f, 0f, 0f);
        private static readonly Vector4 DefaultUp      = new Vector4(0f, 1f, 0f, 0f);

        private Material _matInstance; // 런타임 인스턴스 캐시
        private Material _lastWritten; // 복원 대상(에디터 공유 머티리얼용)

        [ContextMenu("Apply Forced")]
        private void ApplyNow() { _matInstance = null; Apply(); }

        private void OnEnable() { Apply(); }
        private void OnValidate() { _matInstance = null; Apply(); }
        private void LateUpdate() { Apply(); }

        private void OnDisable()
        {
            RestoreDefault(_lastWritten);
            _lastWritten = null;
            _matInstance = null;
        }

        private void Apply()
        {
            Material mat = ResolveMaterial(out bool isSharedAsset);
            Material restoreTarget = isSharedAsset ? mat : null;

            if (_lastWritten != restoreTarget)
            {
                RestoreDefault(_lastWritten);
                _lastWritten = restoreTarget;
            }

            if (mat == null) return;
            Transform t = referenceProxy ? referenceProxy : transform;
            mat.SetVector(ForwardProperty, t.forward);
            mat.SetVector(RightProperty, t.right);
            mat.SetVector(UpProperty, t.up);
        }

        private Material ResolveMaterial(out bool isSharedAsset)
        {
            isSharedAsset = false;
            if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();
            if (targetRenderer == null) return null;

            if (Application.isPlaying)
            {
                if (_matInstance == null)
                {
                    var mats = targetRenderer.materials;
                    if (materialIndex >= 0 && materialIndex < mats.Length)
                        _matInstance = mats[materialIndex];
                }
                isSharedAsset = false;
                return _matInstance;
            }

            isSharedAsset = true;
            var smats = targetRenderer.sharedMaterials;
            if (materialIndex >= 0 && materialIndex < smats.Length)
                return smats[materialIndex];
            return null;
        }

        private void RestoreDefault(Material mat)
        {
            if (mat == null) return;
            mat.SetVector(ForwardProperty, DefaultForward);
            mat.SetVector(RightProperty, DefaultRight);
            mat.SetVector(UpProperty, DefaultUp);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo) return;
            Transform t = referenceProxy ? referenceProxy : transform;
            Gizmos.color = Color.blue;  Gizmos.DrawLine(t.position, t.position + t.forward * 0.3f);
            Gizmos.color = Color.red;   Gizmos.DrawLine(t.position, t.position + t.right * 0.3f);
            Gizmos.color = Color.green; Gizmos.DrawLine(t.position, t.position + t.up * 0.3f);
        }
    }
}
