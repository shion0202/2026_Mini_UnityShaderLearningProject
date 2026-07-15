using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

namespace NeoMantra2026.Scripts
{
    // 스무스 노멀 베이커: 위치가 같은 정점들의 노멀을 평균 → 탄젠트 공간으로 변환해 UV3에 저장.
    // 아웃라인(인버티드 헐) 압출 방향용 — 하드엣지에서 껍질이 갈라져 아웃라인이 끊기는 문제 해결.
    // 탄젠트 공간 저장이라 스키닝 변형을 TBN이 따라감(오브젝트 공간 저장의 본 미추종 한계 회피).
    // 셰이딩 노멀(하드엣지)은 건드리지 않음 — UV3는 압출 전용 데이터.
    [AddComponentMenu("NeoMantra2026/Smooth Normal Baker")]
    public class SmoothNormalBaker : MonoBehaviour
    {
        [Header("대상")]
        [SerializeField, Tooltip("비우면 자식의 SkinnedMeshRenderer/MeshFilter 전체 자동 수집.")]
        private List<Renderer> targetRenderers = new List<Renderer>();

        [Header("Save")]
        [SerializeField, Tooltip("베이크된 메시 에셋 저장 경로.")] private string savePath = "Assets/NeoMantra2026/Meshes/SmoothNormal";

        [Header("Result (자동 기록 — 되돌리기용)")]
        [SerializeField, Tooltip("스왑 전 원본 메시(되돌리기에 사용). 직접 수정하지 말 것.")]
        private List<Mesh> originalMeshes = new List<Mesh>();
        [SerializeField, Tooltip("원본과 짝을 이루는 베이크 메시.")]
        private List<Mesh> bakedMeshes = new List<Mesh>();

#if UNITY_EDITOR
        public void Bake()
        {
            var renderers = CollectRenderers();
            if (renderers.Count == 0) { Debug.LogError("[SmoothNormal] 대상 렌더러가 없습니다."); return; }

            if (!Directory.Exists(savePath)) Directory.CreateDirectory(savePath);

            Undo.RecordObject(this, "스무스 노멀 베이크");
            int bakedCount = 0;

            foreach (var r in renderers)
            {
                Mesh src = GetMesh(r);
                if (src == null) continue;
                if (bakedMeshes.Contains(src)) continue;   // 이미 베이크 메시가 꽂혀 있으면 스킵(이중 베이크 방지)

                // 같은 원본을 쓰는 렌더러가 여럿이면 베이크는 1회, 스왑만 공유
                int existing = originalMeshes.IndexOf(src);
                Mesh baked;
                if (existing >= 0)
                {
                    baked = bakedMeshes[existing];
                }
                else
                {
                    baked = CreateBakedMesh(src);
                    if (baked == null) continue;
                    originalMeshes.Add(src);
                    bakedMeshes.Add(baked);
                    bakedCount++;
                }
                SetMesh(r, baked);
            }

            EditorUtility.SetDirty(this);
            Debug.Log($"[SmoothNormal] 베이크 완료: 신규 {bakedCount}개, 총 {bakedMeshes.Count}개 메시 스왑됨.");
        }

        public void Revert()
        {
            Undo.RecordObject(this, "스무스 노멀 되돌리기");
            var renderers = CollectRenderers();
            foreach (var r in renderers)
            {
                Mesh cur = GetMesh(r);
                int idx = bakedMeshes.IndexOf(cur);
                if (idx >= 0) SetMesh(r, originalMeshes[idx]);
            }
            EditorUtility.SetDirty(this);
            Debug.Log("[SmoothNormal] 원본 메시로 되돌림 (베이크 에셋은 삭제하지 않음).");
        }

        private Mesh CreateBakedMesh(Mesh src)
        {
            var positions = src.vertices;
            var normals = src.normals;
            var tangents = src.tangents;
            if (normals == null || normals.Length != positions.Length)
            { Debug.LogError($"[SmoothNormal] {src.name}: 노멀 없음 — 스킵."); return null; }
            if (tangents == null || tangents.Length != positions.Length)
            { Debug.LogError($"[SmoothNormal] {src.name}: 탄젠트 없음(임포터에서 Tangents 활성 필요) — 스킵."); return null; }

            // 1) 위치 기준 그룹핑(1e-4 양자화) 후 노멀 합산 → 평균
            var groups = new Dictionary<Vector3Int, Vector3>(positions.Length);
            for (int i = 0; i < positions.Length; i++)
            {
                var key = Quantize(positions[i]);
                groups.TryGetValue(key, out var sum);
                groups[key] = sum + normals[i];
            }

            // 2) 정점별 평균 노멀을 탄젠트 공간으로 변환해 UV3에 저장
            var smoothTS = new List<Vector3>(positions.Length);
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 smooth = groups[Quantize(positions[i])].normalized;
                Vector3 n = normals[i];
                Vector3 t = (Vector3)tangents[i];
                Vector3 b = Vector3.Cross(n, t) * tangents[i].w;
                smoothTS.Add(new Vector3(Vector3.Dot(smooth, t), Vector3.Dot(smooth, b), Vector3.Dot(smooth, n)));
            }

            // 3) 사본 생성 + UV3 기록 + 에셋 저장 (원본 무변경)
            Mesh baked = Instantiate(src);
            baked.name = src.name + "_SmoothNormal";
            baked.SetUVs(3, smoothTS);

            string path = Path.Combine(savePath, baked.name + ".asset").Replace("\\", "/");
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null) AssetDatabase.DeleteAsset(path);   // 재베이크 시 갱신
            AssetDatabase.CreateAsset(baked, path);
            return baked;
        }

        private static Vector3Int Quantize(Vector3 p) =>
            new Vector3Int(Mathf.RoundToInt(p.x * 10000f), Mathf.RoundToInt(p.y * 10000f), Mathf.RoundToInt(p.z * 10000f));

        private List<Renderer> CollectRenderers()
        {
            if (targetRenderers != null && targetRenderers.Count > 0)
            {
                var list = new List<Renderer>();
                foreach (var r in targetRenderers) if (r != null) list.Add(r);
                return list;
            }
            var result = new List<Renderer>();
            result.AddRange(GetComponentsInChildren<SkinnedMeshRenderer>(true));
            foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr != null) result.Add(mr);
            }
            return result;
        }

        private static Mesh GetMesh(Renderer r)
        {
            if (r is SkinnedMeshRenderer smr) return smr.sharedMesh;
            var mf = r.GetComponent<MeshFilter>();
            return mf != null ? mf.sharedMesh : null;
        }

        private void SetMesh(Renderer r, Mesh m)
        {
            if (r is SkinnedMeshRenderer smr) { Undo.RecordObject(smr, "메시 스왑"); smr.sharedMesh = m; EditorUtility.SetDirty(smr); }
            else
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null) { Undo.RecordObject(mf, "메시 스왑"); mf.sharedMesh = m; EditorUtility.SetDirty(mf); }
            }
        }

        public void PickSavePath()
        {
            string start = Directory.Exists(savePath) ? savePath : Application.dataPath;
            string abs = EditorUtility.OpenFolderPanel("베이크 메시 저장 폴더 선택", start, "");
            if (string.IsNullOrEmpty(abs)) return;

            string dataPath = Application.dataPath.Replace("\\", "/");
            abs = abs.Replace("\\", "/");
            if (abs == dataPath || abs.StartsWith(dataPath + "/"))
            {
                savePath = "Assets" + abs.Substring(dataPath.Length);
                EditorUtility.SetDirty(this);
            }
            else
            {
                Debug.LogError("[SmoothNormal] 프로젝트의 Assets 폴더 내부를 선택하세요.");
            }
        }
#endif
    }
}
