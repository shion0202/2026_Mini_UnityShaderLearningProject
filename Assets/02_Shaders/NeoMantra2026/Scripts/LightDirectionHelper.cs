using UnityEngine;

namespace NeoMantra2026.Scrips
{
    [ExecuteAlways]
    [AddComponentMenu("NeoMantra2026/Light Direction Helper")]
    public class LightDirectionHelper : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, Tooltip("빛 방향 기준 오브젝트. 비우면 자신.")] private Transform lightProxy;
        [SerializeField, Tooltip("대상 렌더러. 비우면 자식에서 탐색.")] private Renderer targetRenderer;
        [SerializeField, Tooltip("렌더러에서 적용할 머티리얼 번호.")] private int materialIndex = 0;

        [Header("방향 보정")]
        [SerializeField, Tooltip("빛 방향 반전.")] private bool invertDirection = false;

        [Header("Debug")]
        [SerializeField, Tooltip("Right Direction(노랑) 확인용.")] private bool drawGizmo = true;

        private string directionProperty = "_FakeLightDirection";
        private string toggleProperty = "_FakeLightFollowObject";
        private float onValue = 1f;

        [SerializeField, HideInInspector] private Vector4 savedDefault = new Vector4(0, 0, 1, 0);
        [SerializeField, HideInInspector] private bool hasSavedDefault = false;

        private Material _matInstance;
        private Material _sharedTouched; // 에디터에서 값을 쓴 공유 머티리얼 (복원 추적)
        private bool _wasOverriding = false;

        private void OnEnable() { Apply(); }
        private void LateUpdate() { Apply(); }
        private void OnValidate() { _matInstance = null; Apply(); }
        private void OnDisable()
        {
            if (_sharedTouched != null && hasSavedDefault)
            {
                _sharedTouched.SetVector(directionProperty, savedDefault);
            }
                
            _sharedTouched = null;
            _matInstance = null;
            _wasOverriding = false;
        }

        private void Apply()
        {
            Material mat = ResolveMaterial(out bool isSharedAsset);
            if (mat == null) return;

            bool drive = string.IsNullOrEmpty(toggleProperty) || (mat.HasProperty(toggleProperty) && Mathf.Abs(mat.GetFloat(toggleProperty) - onValue) < 0.5f);

            if (drive)
            {
                // 초기값이 아직 없으면(등록 직후) 현재 값을 캡처
                if (!hasSavedDefault)
                {
                    savedDefault = mat.GetVector(directionProperty);
                    hasSavedDefault = true;
                }
                Transform t = lightProxy ? lightProxy : transform;
                Vector3 dir = t.forward * (invertDirection ? -1f : 1f);
                mat.SetVector(directionProperty, dir);

                _wasOverriding = true;
                _sharedTouched = isSharedAsset ? mat : null;
            }
            else
            {
                if (_wasOverriding)
                {
                    // Off로 전환한 직후 → 초기값 복원
                    mat.SetVector(directionProperty, savedDefault);
                    _wasOverriding = false;
                }
                else
                {
                    // Off 상태: 유저가 만진 현재 값을 초기값으로 실시간 캡처
                    savedDefault = mat.GetVector(directionProperty);
                    hasSavedDefault = true;
                }
                _sharedTouched = null; // off일 땐 원본 값을 오염시키지 않음
            }
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
                    {
                        _matInstance = mats[materialIndex];
                    }
                }
                isSharedAsset = false;
                return _matInstance;
            }

            isSharedAsset = true;
            var smats = targetRenderer.sharedMaterials;
            if (materialIndex >= 0 && materialIndex < smats.Length)
            {
                return smats[materialIndex];
            }
            return null;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo) return;

            Transform t = lightProxy ? lightProxy : transform;
            Vector3 dir = t.forward * (invertDirection ? -1f : 1f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(t.position, t.position + dir * 0.5f);
            Gizmos.DrawWireSphere(t.position + dir * 0.5f, 0.05f);
        }
    }
}
