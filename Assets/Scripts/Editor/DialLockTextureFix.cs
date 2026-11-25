#if UNITY_EDITOR
using UnityEditor;

// *_MetallicSmoothness.png 는 색 정보가 아니라 데이터라서 sRGB를 끈다 (자동)
public class DialLockTextureFix : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        // 다이얼 자물쇠 텍스처에만 적용 (다른 모델의 같은 이름 텍스처는 건드리지 않음 — 예: Key2는 MetallicSmoothness를 색으로 씀)
        if (!assetPath.Contains("/DialLocks/")) return;
        if (assetPath.EndsWith("_MetallicSmoothness.png") || assetPath.EndsWith("_Roughness.png") || assetPath.EndsWith("_Metallic.png"))
            ((TextureImporter)assetImporter).sRGBTexture = false;
    }
}
#endif
