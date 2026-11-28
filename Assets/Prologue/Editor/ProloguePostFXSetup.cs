#if PROLOGUE_PPV2
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

// 프롤로그 포스트 프로세싱(Built-in 렌더러용 Post Processing Stack v2) 설정
// 메뉴: Tools > Prologue > Setup Post FX
public static class ProloguePostFXSetup
{
    const string ProfilePath = "Assets/Prologue/PostFX/Prologue_PostFX.asset";
    const string LayerName = "PostProcessing";

    [MenuItem("Tools/Prologue/Setup Post FX")]
    public static string Setup()
    {
        // 1) 레이어
        EnsureLayer(LayerName);
        int layer = LayerMask.NameToLayer(LayerName);

        // 2) 프로필
        if (!AssetDatabase.IsValidFolder("Assets/Prologue/PostFX")) AssetDatabase.CreateFolder("Assets/Prologue", "PostFX");
        var profile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(ProfilePath);
        if (profile == null) { profile = ScriptableObject.CreateInstance<PostProcessProfile>(); AssetDatabase.CreateAsset(profile, ProfilePath); }

        var ao = Get<AmbientOcclusion>(profile);
        ao.mode.Override(AmbientOcclusionMode.MultiScaleVolumetricObscurance);
        ao.intensity.Override(1.1f); ao.thicknessModifier.Override(1.5f);

        var bloom = Get<Bloom>(profile);
        bloom.intensity.Override(2.2f); bloom.threshold.Override(0.95f); bloom.softKnee.Override(0.55f);
        bloom.diffusion.Override(7.5f); bloom.color.Override(new Color(1f, 0.95f, 0.88f));

        var cg = Get<ColorGrading>(profile);
        cg.gradingMode.Override(GradingMode.HighDefinitionRange);
        cg.tonemapper.Override(Tonemapper.ACES);
        cg.postExposure.Override(0.7f);
        cg.temperature.Override(-18f); cg.tint.Override(4f);
        cg.saturation.Override(-22f); cg.contrast.Override(18f);
        cg.colorFilter.Override(new Color(0.92f, 0.96f, 1f));
        cg.lift.Override(new Vector4(0.97f, 1f, 1.06f, -0.03f));
        cg.gain.Override(new Vector4(1.02f, 1f, 0.97f, 0f));

        var vig = Get<Vignette>(profile);
        vig.intensity.Override(0.42f); vig.smoothness.Override(0.45f); vig.color.Override(Color.black);

        var grain = Get<Grain>(profile);
        grain.intensity.Override(0.35f); grain.size.Override(1.1f); grain.colored.Override(false); grain.lumContrib.Override(0.7f);

        var ca = Get<ChromaticAberration>(profile);
        ca.intensity.Override(0.12f);

        var mb = Get<MotionBlur>(profile);
        mb.shutterAngle.Override(200f); mb.sampleCount.Override(10);

        EditorUtility.SetDirty(profile);

        // 3) 전역 볼륨
        var volGo = GameObject.Find("PostFX_GlobalVolume");
        if (volGo == null) volGo = new GameObject("PostFX_GlobalVolume");
        var root = GameObject.Find("== PROLOGUE ==");
        if (root != null) volGo.transform.SetParent(root.transform, false);
        volGo.layer = layer;
        var vol = volGo.GetComponent<PostProcessVolume>() ?? volGo.AddComponent<PostProcessVolume>();
        if (vol == null) vol = volGo.AddComponent<PostProcessVolume>();
        vol.isGlobal = true; vol.priority = 1; vol.sharedProfile = profile;

        // 4) 카메라
        var cam = GameObject.Find("Main Camera").GetComponent<Camera>();
        cam.allowHDR = true;
        var pl = cam.GetComponent<PostProcessLayer>();
        if (pl == null) pl = cam.gameObject.AddComponent<PostProcessLayer>();
        var res = AssetDatabase.LoadAssetAtPath<PostProcessResources>("Packages/com.unity.postprocessing/PostProcessing/PostProcessResources.asset");
        if (res != null) pl.Init(res);
        pl.volumeTrigger = cam.transform;
        pl.volumeLayer = 1 << layer;
        pl.antialiasingMode = PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;

        // 5) 리니어 색공간 (조명이 더 사실적으로 계산됨)
        PlayerSettings.colorSpace = ColorSpace.Linear;

        AssetDatabase.SaveAssets();
        return "PostFX ready. resources=" + (res != null) + " layer=" + layer;
    }

    static T Get<T>(PostProcessProfile p) where T : PostProcessEffectSettings
    {
        T s; if (!p.TryGetSettings(out s)) { s = p.AddSettings<T>(); AssetDatabase.AddObjectToAsset(s, p); }
        s.enabled.Override(true); return s;
    }

    static void EnsureLayer(string name)
    {
        if (LayerMask.NameToLayer(name) != -1) return;
        var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tm.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
        {
            var sp = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(sp.stringValue)) { sp.stringValue = name; tm.ApplyModifiedProperties(); return; }
        }
    }
}
#endif
