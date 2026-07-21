using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

namespace NeoMantra2026.Scripts
{
    [AddComponentMenu("NeoMantra2026/Gradient Texture Generator")]
    public class GradientTextureGenerator : MonoBehaviour
    {
        public enum GradientDirection
        {
            Horizontal, // 가로: 왼쪽(0) → 오른쪽(1)
            Vertical    // 세로: 아래(0) → 위(1)
        }

        [Header("Gradient")]
        [SerializeField, Tooltip("생성할 그래디언트 텍스처 색.")] private Gradient gradient = new Gradient();
        [SerializeField, Tooltip("그래디언트 방향. (가로: 왼쪽→오른쪽, 세로: 아래→위)")] private GradientDirection direction = GradientDirection.Horizontal;

        [Header("Texture")]
        [SerializeField, Tooltip("텍스처 파일 이름.")] private string textureName = "Gradient_Sample";
        [SerializeField, Min(2), Tooltip("텍스처 파일 가로 크기.")] private int width = 256;
        [SerializeField, Min(1), Tooltip("텍스처 파일 세로 크기.")] private int height = 256;

        [Header("Import Options")]
        [SerializeField, Tooltip("색 램프면 ON. 데이터(마스크/커브) 램프면 OFF.")] private bool sRGB = true;
        [SerializeField, Tooltip("UV 범위(0~1) 밖 샘플 처리 방식.")] private TextureWrapMode wrapMode = TextureWrapMode.Clamp;
        [SerializeField, Tooltip("픽셀 보간 방식.")] private FilterMode filterMode = FilterMode.Bilinear;
        [SerializeField, Tooltip("런타임에 스크립트로 픽셀을 읽을 때만 필요. 셰이더 샘플만이면 메모리 절약을 위해 OFF.")] private bool readable = false;

        [Header("Save")]
        [SerializeField, Tooltip("텍스처 저장 경로.")] private string savePath = "Assets/NeoMantra2026/Textures/Gradient";

        [Header("Result")]
        [SerializeField, Tooltip("데이터를 반영할 텍스처 파일. 비어있을 경우 생성.")] private Texture2D generatedTexture;

#if UNITY_EDITOR
        public void BakeGradient()
        {
            if (gradient == null) { Debug.LogError("[Gradient] Gradient가 비어 있습니다."); return; }
            Undo.RecordObject(this, "그래디언트 텍스처 생성");   // textureName/generatedTexture 필드 변경 되돌리기용 (파일 생성 자체는 Undo 불가)
            width = Mathf.Max(2, width);
            height = Mathf.Max(1, height);

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
                    if (!string.IsNullOrEmpty(error)) { Debug.LogError($"[Gradient] 이름 변경 실패: {error}"); return; }
                    currentPath = assetDir + "/" + uniqueName + ".png";
                    textureName = uniqueName;   // 번호가 붙었을 수 있으니 실제 이름을 인스펙터에 반영
                }
                fullPath = currentPath;
            }
            else
            {
                // 신규 생성: 동명 파일이 있으면 test → test1 → test2 … 식으로 번호 부여(덮어쓰기 방지)
                string uniqueName = MakeUniqueFileName(savePath, textureName, null);
                textureName = uniqueName;
                fullPath = Path.Combine(savePath, uniqueName + ".png").Replace("\\", "/");
            }

            // 픽셀 배치 생성 — SetPixels 1회 (SetPixel 이중 루프보다 빠름)
            var temp = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            if (direction == GradientDirection.Horizontal)
            {
                for (int x = 0; x < width; x++)
                {
                    Color c = gradient.Evaluate(width > 1 ? x / (float)(width - 1) : 0f);
                    for (int y = 0; y < height; y++)
                        pixels[y * width + x] = c;
                }
            }
            else
            {
                // Texture2D의 y=0이 아래쪽 행 → 그래디언트 0=아래, 1=위
                for (int y = 0; y < height; y++)
                {
                    Color c = gradient.Evaluate(height > 1 ? y / (float)(height - 1) : 0f);
                    for (int x = 0; x < width; x++)
                        pixels[y * width + x] = c;
                }
            }
            temp.SetPixels(pixels);
            temp.Apply();

            byte[] png = temp.EncodeToPNG();
            DestroyImmediate(temp);   // 임시 텍스처 정리(누수 방지)

            string dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllBytes(fullPath, png);

            AssetDatabase.ImportAsset(fullPath);
            ApplyImporterSettings(fullPath);   // reimport 1회로 통합(원본은 2회)

            generatedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(fullPath);
            EditorUtility.SetDirty(this);
            EditorGUIUtility.PingObject(generatedTexture);   // 프로젝트 창에서 결과 파일 하이라이트
            Debug.Log($"[Gradient] 저장 완료: {fullPath}");
        }

        // dir 안에 baseName.png가 이미 있으면 baseName1, baseName2 … 로 회피 (excludePath 자신은 허용)
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
            string abs = EditorUtility.OpenFolderPanel("그래디언트 저장 폴더 선택", start, "");
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
                Debug.LogError("[Gradient] 프로젝트의 Assets 폴더 내부를 선택하세요.");
            }
        }

        private void ApplyImporterSettings(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = sRGB;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = wrapMode;
            importer.filterMode = filterMode;
            importer.isReadable = readable;

            // 플랫폼(Default) 설정 — 마스크/램프 용도 프리셋.
            // 그라데이션은 압축 밴딩에 민감 → 무압축 + 원본 크기 유지(다운스케일 방지).
            var platform = importer.GetDefaultPlatformTextureSettings();
            platform.maxTextureSize = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.Max(width, height)), 32, 8192);
            platform.resizeAlgorithm = TextureResizeAlgorithm.Bilinear; // 축소 시 링잉 없는 보간(Mitchell은 그라데이션에 과함)
            platform.format = TextureImporterFormat.Automatic;
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(platform);

            importer.SaveAndReimport();
        }
#endif
    }
}
