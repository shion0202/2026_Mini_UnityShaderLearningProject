using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

namespace NeoMantra2026.Scripts
{
    // 블러쉬(볼 홍조) 플레이스홀더 마스크 생성기.
    // 얼굴 UV 위 볼 위치에 소프트 타원 스팟을 찍은 R채널 마스크 PNG를 굽는다.
    // 정식 텍스처는 아티스트가 UV 템플릿 위에 그리는 것이 정석 — 이건 배선 검증/임시용.
    [AddComponentMenu("NeoMantra2026/Blush Texture Generator")]
    public class BlushTextureGenerator : MonoBehaviour
    {
        [Serializable]
        public class BlushSpot
        {
            [Tooltip("스팟 중심 UV(0~1). 얼굴 UV에서 볼 위치.")] public Vector2 center = new Vector2(0.3f, 0.4f);
            [Tooltip("타원 반지름(UV 단위). x=가로, y=세로.")] public Vector2 radius = new Vector2(0.08f, 0.05f);
            [Range(0f, 1f), Tooltip("가장자리 부드러움. 0=칼경계, 1=중심부터 전부 그라데이션.")] public float softness = 0.6f;
            [Range(-90f, 90f), Tooltip("타원 기울기(도).")] public float rotation = 0f;
            [Range(0f, 1f), Tooltip("스팟 최대 농도.")] public float intensity = 1f;
        }

        [Header("Blush Spots")]
        [SerializeField, Tooltip("찍을 스팟 목록. 미러 옵션을 쓰면 한쪽 볼만 정의해도 됨.")]
        private List<BlushSpot> spots = new List<BlushSpot> { new BlushSpot() };
        [SerializeField, Tooltip("각 스팟을 U=0.5 기준 좌우 대칭 복제(볼 두 쪽). 얼굴 UV가 좌우 대칭일 때 사용.")]
        private bool mirrorU = true;

        [Header("Texture")]
        [SerializeField, Tooltip("텍스처 파일 이름.")] private string textureName = "Blush_Placeholder";
        [SerializeField, Min(32), Tooltip("텍스처 크기(정사각).")] private int size = 512;

        [Header("Save")]
        [SerializeField, Tooltip("텍스처 저장 경로.")] private string savePath = "Assets/NeoMantra2026/Textures/Blush";

        [Header("Result")]
        [SerializeField, Tooltip("데이터를 반영할 텍스처 파일. 비어있을 경우 생성합니다.")] private Texture2D generatedTexture;

#if UNITY_EDITOR
        public void BakeBlush()
        {
            if (spots == null || spots.Count == 0) { Debug.LogError("[Blush] 스팟이 없습니다."); return; }
            Undo.RecordObject(this, "블러쉬 텍스처 생성");
            size = Mathf.Max(32, size);

            string fullPath;
            bool editing = generatedTexture != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(generatedTexture));
            if (editing)
            {
                // 수정 모드: 등록된 파일의 원래 폴더 유지. 이름이 바뀌었으면 에셋 리네임(충돌 시 번호 부여).
                string currentPath = AssetDatabase.GetAssetPath(generatedTexture);
                string assetDir = Path.GetDirectoryName(currentPath).Replace("\\", "/");
                string desired = string.IsNullOrEmpty(textureName) ? generatedTexture.name : textureName;
                if (desired != generatedTexture.name)
                {
                    string uniqueName = MakeUniqueFileName(assetDir, desired, currentPath);
                    string error = AssetDatabase.RenameAsset(currentPath, uniqueName);
                    if (!string.IsNullOrEmpty(error)) { Debug.LogError($"[Blush] 이름 변경 실패: {error}"); return; }
                    currentPath = assetDir + "/" + uniqueName + ".png";
                    textureName = uniqueName;
                }
                fullPath = currentPath;
            }
            else
            {
                string uniqueName = MakeUniqueFileName(savePath, textureName, null);
                textureName = uniqueName;
                fullPath = Path.Combine(savePath, uniqueName + ".png").Replace("\\", "/");
            }

            // 스팟 렌더 — 픽셀마다 전 스팟의 최대값(Max 결합: 겹쳐도 1 초과 없음)
            var temp = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float val = 0f;
                    foreach (var s in spots)
                    {
                        val = Mathf.Max(val, EvaluateSpot(s, u, v, false));
                        if (mirrorU) val = Mathf.Max(val, EvaluateSpot(s, u, v, true));
                    }
                    pixels[y * size + x] = new Color(val, val, val, val);
                }
            }
            temp.SetPixels(pixels);
            temp.Apply();

            byte[] png = temp.EncodeToPNG();
            DestroyImmediate(temp);

            string dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllBytes(fullPath, png);

            AssetDatabase.ImportAsset(fullPath);
            ApplyImporterSettings(fullPath);

            generatedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(fullPath);
            EditorUtility.SetDirty(this);
            EditorGUIUtility.PingObject(generatedTexture);
            Debug.Log($"[Blush] 저장 완료: {fullPath}");
        }

        // 타원 소프트 폴오프: 회전 좌표계에서 정규화 거리 → smoothstep 역방향
        private static float EvaluateSpot(BlushSpot s, float u, float v, bool mirrored)
        {
            float cx = mirrored ? 1f - s.center.x : s.center.x;
            float du = u - cx;
            float dv = v - s.center.y;

            float rad = s.rotation * Mathf.Deg2Rad * (mirrored ? -1f : 1f); // 미러 시 기울기도 대칭
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            float lu = du * cos - dv * sin;
            float lv = du * sin + dv * cos;

            float rx = Mathf.Max(s.radius.x, 1e-4f);
            float ry = Mathf.Max(s.radius.y, 1e-4f);
            float d = Mathf.Sqrt((lu * lu) / (rx * rx) + (lv * lv) / (ry * ry)); // 0=중심, 1=타원 경계

            // softness=0이면 경계까지 1, softness=1이면 중심(0)부터 폴오프 시작
            float inner = 1f - Mathf.Max(s.softness, 1e-4f);
            float t = Mathf.Clamp01((d - inner) / Mathf.Max(1f - inner, 1e-4f));
            float fall = 1f - t * t * (3f - 2f * t); // smoothstep 반전
            return fall * s.intensity;
        }

        private static string MakeUniqueFileName(string dir, string baseName, string excludePath)
        {
            string candidate = baseName;
            int i = 0;
            while (true)
            {
                string p = Path.Combine(dir, candidate + ".png").Replace("\\", "/");
                if (!File.Exists(p) || (excludePath != null && p == excludePath)) return candidate;
                i++;
                candidate = baseName + i;
            }
        }

        public void PickSavePath()
        {
            string start = Directory.Exists(savePath) ? savePath : Application.dataPath;
            string abs = EditorUtility.OpenFolderPanel("블러쉬 저장 폴더 선택", start, "");
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
                Debug.LogError("[Blush] 프로젝트의 Assets 폴더 내부를 선택하세요.");
            }
        }

        private void ApplyImporterSettings(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false; // R채널 데이터 마스크
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.isReadable = false;

            var platform = importer.GetDefaultPlatformTextureSettings();
            platform.maxTextureSize = Mathf.Clamp(Mathf.NextPowerOfTwo(size), 32, 8192);
            platform.resizeAlgorithm = TextureResizeAlgorithm.Bilinear;
            platform.format = TextureImporterFormat.Automatic;
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(platform);

            importer.SaveAndReimport();
        }
#endif
    }
}
