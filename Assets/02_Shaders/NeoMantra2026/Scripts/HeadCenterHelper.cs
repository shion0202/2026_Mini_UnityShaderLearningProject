using UnityEngine;

namespace NeoMantra2026.Scripts
{
    [ExecuteAlways]
    [AddComponentMenu("NeoMantra2026/Head Center Helper")]
    public class HeadCenterHelper : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, Tooltip("머리 중심 Transform(머리 본 또는 머리 중앙 빈 오브젝트). 비우면 자신.")] private Transform headPivot;
        [SerializeField, Tooltip("대상 렌더러. 비우면 자식에서 탐색.")] private Renderer targetRenderer;
        [SerializeField, Tooltip("렌더러에서 적용할 머티리얼 번호.")] private int materialIndex = 0;

        [Header("Debug")]
        [SerializeField, Tooltip("머리 중심(파랑) 확인용.")] private bool drawGizmo = true;
        [SerializeField, Tooltip("Gizmo 반지름.")] private float gizmoRadius = 0.15f;

        private string headCenterProperty = "_HeadCenter";

        private static readonly Vector4 DefaultCenter = Vector4.zero;

        private Material _matInstance;
        private Material _lastWritten;

        private void OnEnable() { Apply(); }
        private void LateUpdate() { Apply(); }
        private void OnValidate() { _matInstance = null; Apply(); }
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
            Transform t = headPivot ? headPivot : transform;
            mat.SetVector(headCenterProperty, t.position);   // 월드 좌표 공급
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
                    if (materialIndex >= 0 && materialIndex < mats.Length) _matInstance = mats[materialIndex];
                }
                isSharedAsset = false;
                return _matInstance;
            }
            isSharedAsset = true;
            var smats = targetRenderer.sharedMaterials;
            if (materialIndex >= 0 && materialIndex < smats.Length) return smats[materialIndex];
            return null;
        }

        private void RestoreDefault(Material mat)
        {
            if (mat == null) return;
            mat.SetVector(headCenterProperty, DefaultCenter);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo) return;

            Transform t = headPivot ? headPivot : transform;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(t.position, gizmoRadius);
        }
    }
}
