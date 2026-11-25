#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorPuzzle.EditorTools
{
    /// <summary>
    /// ColorPuzzle 폴더를 Assets 아래에 넣으면 자동으로 머티리얼과 프리팹(ColorPuzzle.prefab)을 만든다.
    /// 수동 실행: 메뉴 Tools > Color Puzzle > Rebuild Prefab
    /// </summary>
    [InitializeOnLoad]
    public static class ColorPuzzleSetup
    {
        const string ModelName = "ColorPuzzle";

        static ColorPuzzleSetup()
        {
            EditorApplication.delayCall += () =>
            {
                string root = FindRoot();
                if (root != null && !File.Exists(Path.Combine(root, "ColorPuzzle.prefab")))
                    Build(root);
            };
        }

        // FBX가 스크립트보다 늦게 임포트되는 경우를 대비
        class Watcher : AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                foreach (var p in imported)
                {
                    if (!p.EndsWith("/ColorPuzzle.fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
                    EditorApplication.delayCall += () =>
                    {
                        string root = FindRoot();
                        if (root != null && !File.Exists(Path.Combine(root, "ColorPuzzle.prefab"))) Build(root);
                    };
                    break;
                }
            }
        }

        [MenuItem("Tools/Color Puzzle/Rebuild Prefab")]
        public static void Rebuild()
        {
            string root = FindRoot();
            if (root == null) { Debug.LogError("[ColorPuzzle] ColorPuzzle.fbx 를 찾지 못했습니다."); return; }
            Build(root);
        }

        static string FindRoot()
        {
            foreach (var guid in AssetDatabase.FindAssets(ModelName + " t:Model"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(p) == ModelName && p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                {
                    // .../ColorPuzzle/Models/ColorPuzzle.fbx -> .../ColorPuzzle
                    string models = Path.GetDirectoryName(p).Replace('\\', '/');
                    return Path.GetDirectoryName(models).Replace('\\', '/');
                }
            }
            return null;
        }

        static void Build(string root)
        {
            string models = root + "/Models";
            string fbx = models + "/ColorPuzzle.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (model == null) { Debug.LogError("[ColorPuzzle] 모델 로드 실패: " + fbx); return; }

            // 텍스처 임포트 설정
            SetTex(models + "/ColorPuzzle_BaseColor.png", true);
            SetTex(models + "/ColorPuzzle_MetallicSmoothness.png", false);

            string matDir = root + "/Materials";
            if (!AssetDatabase.IsValidFolder(matDir)) AssetDatabase.CreateFolder(root, "Materials");
            var baked = MakeBakedMaterial(matDir + "/ColorPuzzle_Baked.mat",
                AssetDatabase.LoadAssetAtPath<Texture2D>(models + "/ColorPuzzle_BaseColor.png"),
                AssetDatabase.LoadAssetAtPath<Texture2D>(models + "/ColorPuzzle_MetallicSmoothness.png"));
            var lampMat = MakeLampMaterial(matDir + "/ColorPuzzle_Lamp.mat");

            var go = (GameObject)Object.Instantiate(model);
            go.name = "ColorPuzzle";
            try
            {
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = r.name.EndsWith("_Lamp") ? lampMat : baked;
                    r.sharedMaterials = mats;
                }

                var puzzle = go.AddComponent<ColorButtonPuzzle>();
                puzzle.body = Find(go, "ColorPuzzle_Body")?.GetComponent<Renderer>();
                puzzle.lamp = Find(go, "ColorPuzzle_Lamp")?.GetComponent<Renderer>();
                puzzle.needle = Find(go, "ColorPuzzle_Needle");
                puzzle.gaugeFace = Find(go, "ColorPuzzle_GaugeFace")?.GetComponent<Renderer>();

                // 표시등 근처 작은 포인트 라이트
                if (puzzle.lamp != null)
                {
                    var lgo = new GameObject("LampLight");
                    lgo.transform.SetParent(puzzle.lamp.transform, false);
                    var l = lgo.AddComponent<Light>();
                    l.type = LightType.Point; l.range = 0.6f; l.intensity = 1.5f; l.enabled = false;
                    l.shadows = LightShadows.None;
                    puzzle.lampLight = l;
                }

                SetupButton(go, "ColorPuzzle_Button_Red", PuzzleButton.Kind.Color, PuzzleColor.Red, 0.008f);
                SetupButton(go, "ColorPuzzle_Button_Blue", PuzzleButton.Kind.Color, PuzzleColor.Blue, 0.008f);
                SetupButton(go, "ColorPuzzle_Button_Yellow", PuzzleButton.Kind.Color, PuzzleColor.Yellow, 0.008f);
                SetupButton(go, "ColorPuzzle_Button_Green", PuzzleButton.Kind.Color, PuzzleColor.Green, 0.008f);
                SetupButton(go, "ColorPuzzle_Button_Enter", PuzzleButton.Kind.Enter, PuzzleColor.Red, 0.004f);

                var bodyT = Find(go, "ColorPuzzle_Body");
                if (bodyT != null && bodyT.GetComponent<Collider>() == null) bodyT.gameObject.AddComponent<BoxCollider>();

                var audio = go.AddComponent<AudioSource>();
                audio.playOnAwake = false; audio.spatialBlend = 1f; audio.maxDistance = 15f;
                puzzle.audioSource = audio;

                string prefabPath = root + "/ColorPuzzle.prefab";
                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                Debug.Log("[ColorPuzzle] 프리팹 생성 완료: " + prefabPath);
            }
            finally { Object.DestroyImmediate(go); }
            AssetDatabase.SaveAssets();
        }

        static void SetupButton(GameObject root, string name, PuzzleButton.Kind kind, PuzzleColor color, float depth)
        {
            var t = Find(root, name);
            if (t == null) { Debug.LogWarning("[ColorPuzzle] 버튼 없음: " + name); return; }
            if (t.GetComponent<Collider>() == null) t.gameObject.AddComponent<BoxCollider>();
            var b = t.GetComponent<PuzzleButton>() ?? t.gameObject.AddComponent<PuzzleButton>();
            b.kind = kind; b.color = color; b.pressDepth = depth;
        }

        static Transform Find(GameObject root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        static void SetTex(string path, bool srgb)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null || ti.sRGBTexture == srgb) return;
            ti.sRGBTexture = srgb;
            ti.SaveAndReimport();
        }

        static Shader LitShader(out int kind)
        {
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp == null) { kind = 0; return Shader.Find("Standard"); }
            string n = rp.GetType().Name;
            if (n.Contains("Universal")) { kind = 1; return Shader.Find("Universal Render Pipeline/Lit"); }
            if (n.Contains("HD")) { kind = 2; return Shader.Find("HDRP/Lit"); }
            kind = 0; return Shader.Find("Standard");
        }

        static Material LoadOrCreate(string path, Shader sh)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            else m.shader = sh;
            return m;
        }

        static Material MakeBakedMaterial(string path, Texture2D baseMap, Texture2D msMap)
        {
            var sh = LitShader(out int kind);
            var m = LoadOrCreate(path, sh);
            if (kind == 1)
            {
                m.SetTexture("_BaseMap", baseMap); m.SetColor("_BaseColor", Color.white);
                m.SetTexture("_MetallicGlossMap", msMap); m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetFloat("_Smoothness", 1f);
            }
            else if (kind == 2)
            {
                m.SetTexture("_BaseColorMap", baseMap);
                m.SetFloat("_Metallic", 0.3f); m.SetFloat("_Smoothness", 0.4f);
            }
            else
            {
                m.SetTexture("_MainTex", baseMap); m.SetColor("_Color", Color.white);
                m.SetTexture("_MetallicGlossMap", msMap); m.EnableKeyword("_METALLICGLOSSMAP");
                m.SetFloat("_GlossMapScale", 1f);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material MakeLampMaterial(string path)
        {
            var sh = LitShader(out int kind);
            var m = LoadOrCreate(path, sh);
            var c = new Color(0.25f, 0.02f, 0.02f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.85f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.85f);
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
#endif
