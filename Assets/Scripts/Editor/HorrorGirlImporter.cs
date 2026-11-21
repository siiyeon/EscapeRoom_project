using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 여자 귀신(HorrorGirl.fbx)을 가져올 때 README 안내대로 자동 설정한다.
/// - Rig: Humanoid (Avatar는 모델에서 생성)
/// - Idle / Walk 애니메이션 반복(Loop Time)
/// - HorrorGirl_Normal.png → Normal map
/// - 애니메이터(Assets/Models/Characters/Resources/HorrorGirlAnimator.controller)를 만들어 둔다.
///   GhostPatrol이 실행 중에 이걸 불러와서 멈춰 있으면 Idle, 움직이면 Walk를 재생한다.
/// 처음 가져올 때 한 번만 설정하므로, 이후 Inspector에서 직접 바꾼 값은 덮어쓰지 않는다.
/// </summary>
public class HorrorGirlImporter : AssetPostprocessor
{
    const string ModelPath = "Assets/Models/Characters/HorrorGirl.fbx";
    const string ControllerPath = "Assets/Models/Characters/Resources/HorrorGirlAnimator.controller";
    const string DoneMark = "HorrorGirlImporter:configured";

    bool IsTarget => assetPath == ModelPath;
    bool AlreadyConfigured => assetImporter.userData != null && assetImporter.userData.Contains(DoneMark);

    void OnPreprocessModel()
    {
        if (!IsTarget || AlreadyConfigured) return;
        var importer = (ModelImporter)assetImporter;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importCameras = false;
        importer.importLights = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
    }

    void OnPreprocessAnimation()
    {
        if (!IsTarget || AlreadyConfigured) return;
        var importer = (ModelImporter)assetImporter;
        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0) return;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            clip.loopTime = true;
            clip.loopPose = true;
        }
        importer.clipAnimations = clips;
        importer.userData = (importer.userData ?? "") + DoneMark;
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.EndsWith("HorrorGirl_Normal.png")) return;
        var importer = (TextureImporter)assetImporter;
        if (importer.textureType == TextureImporterType.NormalMap) return;
        importer.textureType = TextureImporterType.NormalMap;
    }

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (!imported.Contains(ModelPath)) return;
        EditorApplication.delayCall += CreateControllerIfMissing;
    }

    static void CreateControllerIfMissing()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null) return;
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToArray();
        AnimationClip idle = clips.FirstOrDefault(c => c.name.Contains("Idle"));
        AnimationClip walk = clips.FirstOrDefault(c => c.name.Contains("Walk"));
        if (idle == null || walk == null)
        {
            Debug.LogWarning($"[HorrorGirlImporter] Idle/Walk 애니메이션을 찾지 못했습니다. ({string.Join(", ", clips.Select(c => c.name))})");
            return;
        }

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState idleState = sm.AddState("Idle");
        idleState.motion = idle;
        AnimatorState walkState = sm.AddState("Walk");
        walkState.motion = walk;
        sm.defaultState = idleState;

        AnimatorStateTransition toWalk = idleState.AddTransition(walkState);
        toWalk.hasExitTime = false;
        toWalk.duration = 0.2f;
        toWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        AnimatorStateTransition toIdle = walkState.AddTransition(idleState);
        toIdle.hasExitTime = false;
        toIdle.duration = 0.3f;
        toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        AssetDatabase.SaveAssets();
        Debug.Log("[HorrorGirlImporter] 여자 귀신 애니메이터를 만들었습니다: " + ControllerPath);
    }
}
