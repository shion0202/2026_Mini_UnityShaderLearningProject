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
        [Header("Gradient")]
        [SerializeField, Tooltip("생성할 그래디언트 텍스처 색.")] private Gradient gradient = new Gradient();

        [Header("Texture")]
        [SerializeField, Tooltip("텍스처 파일 이름.")] private string textureName = "GeneratedGradient";
        [SerializeField, Min(2), Tooltip("텍스처 파일 가로 크기.")] private int width = 256;
        [SerializeField, Min(1), Tooltip("텍스처 파일 세로 크기.")] private int height = 1;

        [Header("Import Options")]
        [SerializeField, Tooltip("색 램프면 ON. 데이터(마스크/커브) 램프면 OFF.")] private bool sRGB = true;
        [SerializeField, Tooltip("UV 범위(0~1) 밖 샘플 처리 방식.")] private TextureWrapMode wrapMode = TextureWrapMode.Clamp;
        [SerializeField, Tooltip("픽셀 보간 방식.")] private FilterMode filterMode = FilterMode.Bilinear;
        [SerializeField, Tooltip("런타임에 스크립트로 픽셀을 읽을 때만 필요. 셰이더 샘플만이면 OFF(메모리 절약).")] private bool readable = false;

        [Header("Save")]
        [SerializeField, Tooltip("텍스처 저장 경로.")] private string savePath = "Assets/NeoMantra2026/Textures/Gradient";

        [Header("Result")]
        [SerializeField, Tooltip("데이터를 반영할 텍스처 파일. 비어있을 경우 생성합니다.")] private Texture2D generatedTexture;

#if UNITY_EDITOR
        public void BakeGradient()
        {
            if (gradient == null) { Debug.LogError("[Gradient] Gradient가 비어 있습니다."); return; }
            width = Mathf.Max(2, width);
            height = Mathf.Max(1, height);

            string fileName = (generatedTexture != null ? generatedTexture.name : textureName) + ".png";
            string fullPath = Path.Combine(savePath, fileName).Replace("\\", "/");

            // 픽셀 배치 생성 — SetPixels 1회 (SetPixel 이중 루프보다 빠름)
            var temp = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int x = 0; x < width; x++)
            {
                Color c = gradient.Evaluate(x / (float)(width - 1));
                for (int y = 0; y < height; y++)
                    pixels[y * width + x] = c;
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
            Debug.Log($"[Gradient] 저장 완료: {fullPath}");
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
            importer.SaveAndReimport();
        }
#endif
    }
}
