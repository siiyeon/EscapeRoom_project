using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 학교 모델(SchoolBuilding.fbx)을 가져올 때, Blender 작업용으로만 쓰던 보조 오브젝트를 빼고 가져온다.
/// - 창문 구멍 뚫기용 상자, 내부 공간 상자, 백업 유리 등 (게임에서 보이면 창문·실내를 막아버림)
/// - Blender 카메라/조명 (게임 조명은 씬에서 따로 관리)
/// 친구가 Blender에서 다시 내보내도 같은 이름이면 자동으로 정리된다.
/// </summary>
public class SchoolImportCleanup : AssetPostprocessor
{
    const string SchoolPath = "Assets/Models/Environment/SchoolBuilding.fbx";

    static readonly HashSet<string> HelperNames = new HashSet<string>
    {
        "Interior_Cavity",
        "Window_Openings",
        "Window_Backings_Removed",
        "School_Glass_WindowBackup",
    };

    void OnPreprocessModel()
    {
        if (assetPath != SchoolPath) return;
        var importer = (ModelImporter)assetImporter;
        importer.importCameras = false;
        importer.importLights = false;
    }

    void OnPostprocessModel(GameObject root)
    {
        if (assetPath != SchoolPath) return;
        var remove = new List<GameObject>();
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (HelperNames.Contains(t.name)) remove.Add(t.gameObject);
        foreach (GameObject go in remove) Object.DestroyImmediate(go);
        if (remove.Count > 0) Debug.Log($"[SchoolImportCleanup] 학교 모델에서 Blender 보조 오브젝트 {remove.Count}개를 뺐습니다.");
    }
}
