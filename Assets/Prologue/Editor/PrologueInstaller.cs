using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// 프롤로그 패키지를 임포트하면 자동으로 한 번 실행된다.
//  1) 필요한 패키지 설치 (Built-in 렌더러: Post Processing)
//  2) 렌더 파이프라인에 맞게 재질/포스트 프로세싱 변환 (URP 자동 변환)
//  3) Prologue 씬을 빌드 순서 0번(게임 첫 씬)으로 등록
//  4) 색 공간 Linear
// 다시 실행하려면 메뉴: Tools > Prologue > Install (첫 씬으로 설정)
namespace PrologueCutscene.EditorTools
{
    [InitializeOnLoad]
    public static class PrologueInstaller
    {
        public const string Root = "Assets/Prologue";
        public const string ScenePath = Root + "/Scenes/Prologue.unity";
        const string Marker = Root + "/Editor/.install_done";
        const string PPv2Package = "com.unity.postprocessing";

        static PrologueInstaller()
        {
            if (File.Exists(Marker)) return;
            if (!File.Exists(ScenePath)) return;
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += () => Install(false); return; }
                Install(false);
            };
        }

        public enum Pipeline { BuiltIn, URP, HDRP, Other }

        public static Pipeline Current()
        {
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp == null) return Pipeline.BuiltIn;
            var n = rp.GetType().FullName;
            if (n.Contains("Universal")) return Pipeline.URP;
            if (n.Contains("HighDefinition")) return Pipeline.HDRP;
            return Pipeline.Other;
        }

        [MenuItem("Tools/Prologue/Install (첫 씬으로 설정)")]
        static void InstallMenu() { Install(true); }

        public static void Install(bool fromMenu)
        {
            var log = new List<string>();
            var pipe = Current();
            log.Add("렌더 파이프라인: " + pipe);

            // 1) Built-in 렌더러면 Post Processing 패키지가 필요하다
            if (pipe == Pipeline.BuiltIn)
            {
#if !PROLOGUE_PPV2
                Debug.Log("[Prologue] Post Processing 패키지를 설치합니다. 설치 후 자동으로 이어서 진행됩니다.");
                UnityEditor.PackageManager.Client.Add(PPv2Package);
                return;   // 패키지 설치 → 재컴파일 → [InitializeOnLoad] 가 다시 Install 을 부른다
#else
                log.Add(ProloguePipelineSetup.SetupBuiltIn());
#endif
            }
            else if (pipe == Pipeline.URP)
            {
#if PROLOGUE_URP
                log.Add(ProloguePipelineSetup.SetupURP());
#endif
            }
            else
            {
                log.Add("주의: " + pipe + " 렌더 파이프라인은 자동 변환을 지원하지 않습니다. 재질이 분홍색이면 Built-in 또는 URP 에서 사용하세요.");
            }

            // 2) 빌드 순서 0번에 Prologue 씬
            log.Add(SetFirstScene());

            // 3) 색 공간
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;
                log.Add("색 공간을 Linear 로 변경");
            }

            AssetDatabase.SaveAssets();
            File.WriteAllText(Marker, "installed " + System.DateTime.Now.ToString("s") + "\n" + string.Join("\n", log));
            AssetDatabase.Refresh();
            Debug.Log("[Prologue] 설치 완료\n- " + string.Join("\n- ", log));
            if (fromMenu) EditorUtility.DisplayDialog("Prologue", "설치 완료\n\n- " + string.Join("\n- ", log), "확인");
        }

        static string SetFirstScene()
        {
            var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            string next = list.Count > 0 ? list[0].path : "(없음)";
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            return "Prologue 씬을 빌드 순서 0번으로 등록 (끝나면 다음 씬: " + next + ")";
        }
    }

    // ---------------- 파이프라인별 설정 ----------------
    public static class ProloguePipelineSetup
    {
        const string MatDir = PrologueInstaller.Root + "/Materials";

        static string[] PrologueMaterials()
        {
            return AssetDatabase.FindAssets("t:Material", new[] { PrologueInstaller.Root })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();
        }

        static int EnsureLayer(string name)
        {
            int idx = LayerMask.NameToLayer(name);
            if (idx != -1) return idx;
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tm.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var sp = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(sp.stringValue)) { sp.stringValue = name; tm.ApplyModifiedProperties(); return i; }
            }
            return -1;
        }

#if PROLOGUE_PPV2
        // Built-in: 씬에 저장된 Post Processing(v2) 구성 요소의 레이어 연결만 다시 맞춘다
        public static string SetupBuiltIn()
        {
            int layer = EnsureLayer("PostProcessing");
            var scene = EditorSceneManager.OpenScene(PrologueInstaller.ScenePath, OpenSceneMode.Additive);
            string r = "Post Processing 레이어 연결";
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var vol in root.GetComponentsInChildren<UnityEngine.Rendering.PostProcessing.PostProcessVolume>(true))
                    if (layer >= 0) vol.gameObject.layer = layer;
                foreach (var pl in root.GetComponentsInChildren<UnityEngine.Rendering.PostProcessing.PostProcessLayer>(true))
                {
                    if (layer >= 0) pl.volumeLayer = 1 << layer;
                    var res = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.PostProcessing.PostProcessResources>("Packages/com.unity.postprocessing/PostProcessing/PostProcessResources.asset");
                    if (res != null) pl.Init(res);
                }
            }
            EditorSceneManager.SaveScene(scene);
            if (EditorSceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
            return r + " (레이어 " + layer + ")";
        }
#endif

#if PROLOGUE_URP
        // URP: Standard 재질 → URP Lit, 비 파티클 → URP 파티클, Post Processing v2 → URP Volume
        public static string SetupURP()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            int converted = 0;
            foreach (var p in PrologueMaterials())
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null || m.shader == null || m.shader.name != "Standard") continue;
                ConvertStandardToURP(m, lit); converted++;
            }

            // 비: 기본 파티클 재질은 URP 에서 분홍색이 되므로 교체
            var rainMatPath = MatDir + "/Rain_URP.mat";
            var rainMat = AssetDatabase.LoadAssetAtPath<Material>(rainMatPath);
            if (rainMat == null)
            {
                rainMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                rainMat.SetFloat("_Surface", 1); rainMat.SetFloat("_Blend", 0);
                rainMat.SetOverrideTag("RenderType", "Transparent");
                rainMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                rainMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); rainMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); rainMat.SetInt("_ZWrite", 0);
                rainMat.renderQueue = (int)RenderQueue.Transparent;
                rainMat.SetColor("_BaseColor", new Color(0.8f, 0.85f, 0.95f, 0.5f));
                AssetDatabase.CreateAsset(rainMat, rainMatPath);
            }

            var scene = EditorSceneManager.OpenScene(PrologueInstaller.ScenePath, OpenSceneMode.Additive);
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var ps in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
                    if (ps.sharedMaterial == null || ps.sharedMaterial.shader.name.StartsWith("Particles/")) ps.sharedMaterial = rainMat;

                // 남아 있는(동작하지 않는) Post Processing v2 구성 요소 제거
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
#if PROLOGUE_PPV2
                foreach (var c in root.GetComponentsInChildren<UnityEngine.Rendering.PostProcessing.PostProcessLayer>(true)) Object.DestroyImmediate(c);
                foreach (var c in root.GetComponentsInChildren<UnityEngine.Rendering.PostProcessing.PostProcessVolume>(true)) Object.DestroyImmediate(c);
#endif
                // URP 는 조명이 거리 제곱으로 줄어들어 Built-in 과 밝기가 다르다 → 같은 느낌이 나도록 보정
                foreach (var l in root.GetComponentsInChildren<Light>(true))
                {
                    if (l.type == LightType.Directional) continue;
                    l.intensity = l.intensity * Mathf.Max(l.range * l.range / 34f, 0.02f);
                }
                foreach (var cam in root.GetComponentsInChildren<Camera>(true))
                {
                    var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
                    data.renderPostProcessing = true;
                    data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                }
            }

            // Volume 생성 (기존 Post Processing 수치와 같게)
            var profilePath = PrologueInstaller.Root + "/PostFX/Prologue_URP_Volume.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, profilePath); }
            BuildURPProfile(profile);
            GameObject volGo = null;
            foreach (var root in scene.GetRootGameObjects()) { var t = FindDeep(root.transform, "PostFX_URPVolume"); if (t) volGo = t.gameObject; }
            if (volGo == null)
            {
                volGo = new GameObject("PostFX_URPVolume");
                var parent = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "== PROLOGUE ==");
                if (parent) volGo.transform.SetParent(parent.transform, false);
            }
            var vol = volGo.GetComponent<Volume>() ?? volGo.AddComponent<Volume>();
            vol.isGlobal = true; vol.priority = 10; vol.sharedProfile = profile;

            EditorSceneManager.SaveScene(scene);
            if (EditorSceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
            return "URP 변환: 재질 " + converted + "개, 비 파티클, 포스트 프로세싱 Volume (주의: 앰비언트 오클루전은 URP 렌더러 설정의 SSAO 를 켜야 합니다)";
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = FindDeep(c, name); if (r) return r; }
            return null;
        }

        static void ConvertStandardToURP(Material m, Shader lit)
        {
            var mainTex = m.GetTexture("_MainTex"); var st = new Vector4(m.mainTextureScale.x, m.mainTextureScale.y, m.mainTextureOffset.x, m.mainTextureOffset.y);
            var color = m.color;
            var bump = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null; float bumpScale = m.HasProperty("_BumpScale") ? m.GetFloat("_BumpScale") : 1f;
            var mg = m.HasProperty("_MetallicGlossMap") ? m.GetTexture("_MetallicGlossMap") : null;
            float metallic = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;
            float smooth = mg != null ? (m.HasProperty("_GlossMapScale") ? m.GetFloat("_GlossMapScale") : 1f) : (m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : 0.5f);
            bool emission = m.IsKeywordEnabled("_EMISSION"); var emitCol = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
            float mode = m.HasProperty("_Mode") ? m.GetFloat("_Mode") : 0f;

            m.shader = lit;
            m.SetTexture("_BaseMap", mainTex); m.SetVector("_BaseMap_ST", st); m.SetColor("_BaseColor", color);
            if (bump) { m.SetTexture("_BumpMap", bump); m.SetFloat("_BumpScale", bumpScale); m.EnableKeyword("_NORMALMAP"); }
            if (mg) { m.SetTexture("_MetallicGlossMap", mg); m.EnableKeyword("_METALLICSPECGLOSSMAP"); }
            m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", smooth);
            if (emission) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emitCol); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
            if (mode >= 2f)
            {
                m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0);
                m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); m.SetInt("_ZWrite", 0);
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(m);
        }

        static T Add<T>(VolumeProfile p) where T : VolumeComponent
        {
            T c; if (!p.TryGet(out c)) { c = p.Add<T>(true); AssetDatabase.AddObjectToAsset(c, p); }
            c.active = true; return c;
        }

        static void BuildURPProfile(VolumeProfile p)
        {
            var tm = Add<UnityEngine.Rendering.Universal.Tonemapping>(p); tm.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.ACES);
            var ca = Add<UnityEngine.Rendering.Universal.ColorAdjustments>(p);
            ca.postExposure.Override(0.6f); ca.contrast.Override(10f); ca.saturation.Override(-28f); ca.colorFilter.Override(new Color(0.97f, 0.98f, 1f));
            var wb = Add<UnityEngine.Rendering.Universal.WhiteBalance>(p); wb.temperature.Override(-8f); wb.tint.Override(4f);
            var lgg = Add<UnityEngine.Rendering.Universal.LiftGammaGain>(p);
            lgg.lift.Override(new Vector4(0.99f, 1f, 1.03f, -0.02f)); lgg.gain.Override(new Vector4(1.02f, 1f, 0.97f, 0f));
            var bl = Add<UnityEngine.Rendering.Universal.Bloom>(p); bl.intensity.Override(0.6f); bl.threshold.Override(0.95f); bl.scatter.Override(0.7f); bl.tint.Override(new Color(1f, 0.95f, 0.88f));
            var vg = Add<UnityEngine.Rendering.Universal.Vignette>(p); vg.intensity.Override(0.42f); vg.smoothness.Override(0.45f); vg.color.Override(Color.black);
            var fg = Add<UnityEngine.Rendering.Universal.FilmGrain>(p); fg.intensity.Override(0.35f); fg.response.Override(0.7f);
            var chr = Add<UnityEngine.Rendering.Universal.ChromaticAberration>(p); chr.intensity.Override(0.05f);
            var mb = Add<UnityEngine.Rendering.Universal.MotionBlur>(p); mb.intensity.Override(0.3f);
            EditorUtility.SetDirty(p);
        }
#endif
    }
}
