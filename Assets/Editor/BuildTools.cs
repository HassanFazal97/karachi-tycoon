using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KarachiTycoon.EditorTools
{
    // Editor and command-line helpers. From a terminal:
    //   Unity -batchmode -quit -projectPath . -executeMethod KarachiTycoon.EditorTools.BuildTools.BuildMac
    [InitializeOnLoad]
    public static class BuildTools
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string PanelPath = "Assets/Resources/UI/KtPanel.asset";
        const string UrpPath = "Assets/Settings/KT_URP.asset", RendererPath = "Assets/Settings/KT_URP_Renderer.asset";
        const string LitPath = "Assets/Resources/Materials/KT_Lit.mat", GlowPath = "Assets/Resources/Materials/KT_Glow.mat", ParticlePath = "Assets/Resources/Materials/KT_Particle.mat";

        // Runs once in any editor that opens the project, so nobody has to remember the menu item.
        static BuildTools()
        {
            EditorApplication.delayCall += () => { if (NeedsSetUp()) { SetUp(); Debug.Log("[BuildTools] Project set up: scene, UI panel, input handling, shaders."); } };
        }

        static SerializedObject PlayerSettingsObject() =>
            new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);

        static bool NeedsSetUp() =>
            !File.Exists(ScenePath) || !File.Exists(PanelPath) || PlayerSettingsObject().FindProperty("activeInputHandler")?.intValue != 2
            || PlayerSettings.defaultInterfaceOrientation != UIOrientation.Portrait
            || GraphicsSettings.defaultRenderPipeline == null || !File.Exists(LitPath);

        // The game builds itself in code, so the scene only needs to exist (players need one to boot).
        [MenuItem("Karachi Tycoon/Set Up Project")]
        public static void SetUp()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            // Materials are made in code, so builds would strip the lit shader without this.
            var shader = Shader.Find("Standard");
            var gs = new SerializedObject(AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset"));
            var list = gs.FindProperty("m_AlwaysIncludedShaders");
            bool present = false;
            for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) present = true;
            if (!present && shader != null)
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                gs.ApplyModifiedProperties();
            }

            // UI Toolkit panel settings as an asset (a runtime-made one has no text shaping data).
            if (!File.Exists(PanelPath))
            {
                var panel = ScriptableObject.CreateInstance<UnityEngine.UIElements.PanelSettings>();
                panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.ThemeStyleSheet>("Assets/Resources/UI/KtTheme.tss");
                panel.scaleMode = UnityEngine.UIElements.PanelScaleMode.ScaleWithScreenSize;
                AssetDatabase.CreateAsset(panel, PanelPath);
            }

            // Input: the game reads keys through the Input System package, and "Both" keeps
            // UI Toolkit's pointer input working whichever backend it picks.
            var player = PlayerSettingsObject();
            var input = player.FindProperty("activeInputHandler");
            if (input != null && input.intValue != 2) { input.intValue = 2; player.ApplyModifiedProperties(); }

            SetUpRendering();

            // Phones play in portrait, one-handed. Steam builds are landscape windows and ignore this.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            AssetDatabase.SaveAssets();
        }

        // URP with HDR for bloom on the practical lights, tuned to run on mid-range Android phones:
        // one shadow cascade, soft shadows, no MSAA (FXAA on the camera instead), per-pixel extra lights.
        static void SetUpRendering()
        {
            Directory.CreateDirectory("Assets/Settings");
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpPath);
            if (urp == null)
            {
                // URP's own "Create > URP Asset (with Universal Renderer)" path, which also sets up post-processing data.
                var create = typeof(UniversalRenderPipelineAsset).GetMethod("CreateRendererAsset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var ps = create.GetParameters();
                var rendererType = System.Enum.Parse(ps[1].ParameterType, "UniversalRenderer");
                var renderer = (ScriptableRendererData)create.Invoke(null, new object[] { RendererPath, rendererType, false, "Renderer" });
                urp = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(urp, UrpPath);
            }
            urp.supportsHDR = true;
            urp.msaaSampleCount = 1;
            urp.renderScale = 1f;
            urp.shadowDistance = 90;
            urp.shadowCascadeCount = 1;
            urp.mainLightShadowmapResolution = 2048;
            urp.maxAdditionalLightsCount = 8;
            var so = new SerializedObject(urp);
            so.FindProperty("m_SoftShadowsSupported").boolValue = true;
            so.FindProperty("m_AdditionalLightsRenderingMode").intValue = 1; // per pixel
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);
            GraphicsSettings.defaultRenderPipeline = urp;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }

            // Template materials in Resources: they put the URP shaders (and only the variants used) into builds.
            Directory.CreateDirectory("Assets/Resources/Materials");
            if (!File.Exists(LitPath)) AssetDatabase.CreateAsset(new Material(Shader.Find("Universal Render Pipeline/Lit")), LitPath);
            if (!File.Exists(GlowPath))
            {
                var g = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                g.EnableKeyword("_EMISSION");
                g.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                g.SetColor("_EmissionColor", Color.white);
                AssetDatabase.CreateAsset(g, GlowPath);
            }
            if (!File.Exists(ParticlePath))
            {
                var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                // Transparent, alpha blended.
                m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0);
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); m.SetFloat("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                AssetDatabase.CreateAsset(m, ParticlePath);
            }
        }

        [MenuItem("Karachi Tycoon/Build macOS")]
        public static void BuildMac()
        {
            SetUp();
            // Test builds keep their own PlayerPrefs, so automated runs never touch a real save.
            if (System.Environment.GetCommandLineArgs().Contains("-kt-test")) PlayerSettings.productName = "Karachi Tycoon Test";
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/mac/KarachiTycoon.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None,
            });
            Debug.Log($"[BuildTools] {report.summary.result} · {report.summary.totalErrors} errors · {report.summary.totalSize / 1048576f:F1} MB");
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
