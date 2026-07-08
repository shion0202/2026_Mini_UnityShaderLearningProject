using UnityEngine;

namespace NeoMantra2026.Scripts
{
    [ExecuteAlways]
    [AddComponentMenu("NeoMantra2026/SDF Helper")]
    public class SDFHelper : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, Tooltip("얼굴 방향 기준 Transform (머리 본 자식으로 두고 월드 기즈모로 정렬한 빈 오브젝트 권장). 비우면 자신.")] private Transform faceRoot;
        [SerializeField, Tooltip("얼굴 위치(_FaceCenter) 기준 Transform. 방향과 다른 소스를 쓰고 싶을 때 지정(예: 머리 본에 둔 빈 오브젝트). 비우면 faceRoot 위치 사용.")] private Transform faceCenter;
        [SerializeField, Tooltip("대상 렌더러. 비우면 자식에서 탐색.")] private Renderer targetRenderer;
        [SerializeField, Tooltip("렌더러에서 적용할 머티리얼 번호.")] private int materialIndex = 0;

        [Header("축 보정")]
        [SerializeField, Tooltip("전후 반전.")] private bool invertForward = false;
        [SerializeField, Tooltip("좌우 반전.")] private bool invertRight = false;

        [Header("Debug")]
        [SerializeField, Tooltip("Forward(파랑)과 Right(빨강) 방향 확인용.")] private bool drawGizmo = true;

        // 셰이더 프로퍼티 Reference 이름
        private string ForwardProperty = "_FaceForward";
        private string RightProperty = "_FaceRight";
        private string CenterProperty = "_FaceCenter"; // 얼굴 중심 월드 위치(SDF 받는 그림자 1점 샘플용)

        private static readonly Vector4 DefaultForward = new Vector4(0f, 0f, 1f, 0f);
        private static readonly Vector4 DefaultRight = new Vector4(1f, 0f, 0f, 0f);
        private static readonly Vector4 DefaultCenter = new Vector4(0f, 0f, 0f, 0f);

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

            // 복원 대상이 바뀌면(스왑/플레이 진입 등) 직전 공유 에셋을 기본값 복원
            if (_lastWritten != restoreTarget)
            {
                RestoreDefault(_lastWritten);
                _lastWritten = restoreTarget;
            }

            if (mat == null) return;
            Transform dirT = faceRoot ? faceRoot : transform;      // 방향 소스
            Transform posT = faceCenter ? faceCenter : dirT;       // 위치 소스(없으면 방향과 공용)
            Vector3 fwd = dirT.forward * (invertForward ? -1f : 1f);
            Vector3 right = dirT.right * (invertRight ? -1f : 1f);
            mat.SetVector(ForwardProperty, fwd);
            mat.SetVector(RightProperty, right);
            mat.SetVector(CenterProperty, posT.position); // 위치(w=0). 빛 쪽 밀기는 셰이더에서 처리
        }

        private Material ResolveMaterial(out bool isSharedAsset)
        {
            isSharedAsset = false;
            if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();
            if (targetRenderer == null) return null;

            if (Application.isPlaying)
            {
                // 플레이 중: 인스턴스 (무오염)
                if (_matInstance == null)
                {
                    var mats = targetRenderer.materials; // 최초 접근 시 인스턴스화
                    if (materialIndex >= 0 && materialIndex < mats.Length)
                        _matInstance = mats[materialIndex];
                }
                isSharedAsset = false;
                return _matInstance;
            }

            // 에디터: 공유 머티리얼로 프리뷰 (복원 필요)
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
            mat.SetVector(CenterProperty, DefaultCenter);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo) return;

            Transform dirT = faceRoot ? faceRoot : transform;
            Transform posT = faceCenter ? faceCenter : dirT;
            // 방향은 dirT 기준(월드 정렬 확인용), 원점은 위치 소스에서 그려 배치 감 잡기 쉽게
            Vector3 o = posT.position;
            Gizmos.color = Color.blue; // forward
            Gizmos.DrawLine(o, o + dirT.forward * (invertForward ? -0.3f : 0.3f));
            Gizmos.color = Color.red;  // right
            Gizmos.DrawLine(o, o + dirT.right * (invertRight ? -0.3f : 0.3f));
            Gizmos.color = Color.yellow; // _FaceCenter 샘플 위치
            Gizmos.DrawWireSphere(o, 0.03f);
        }
    }
}
