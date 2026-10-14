using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KarachiTycoon.EditorTools
{
    // Editor and command-line helpers. From a terminal:
    //   Unity -batchmode -quit -projectPath . -executeMethod KarachiTycoon.EditorTools.BuildTools.BuildMac
    [InitializeOnLoad]
    public static class BuildTools
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string PanelPath = "Assets/Resources/UI/KtPanel.asset";

        // Runs once in any editor that opens the project, so nobody has to remember the menu item.
        static BuildTools()
        {
            EditorApplication.delayCall += () => { if (NeedsSetUp()) { SetUp(); Debug.Log("[BuildTools] Project set up: scene, UI panel, input handling, shaders."); } };
        }

        static SerializedObject PlayerSettingsObject() =>
            new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);

        static bool NeedsSetUp() =>
            !File.Exists(ScenePath) || !File.Exists(PanelPath) || PlayerSettingsObject().FindProperty("activeInputHandler")?.intValue != 2;

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

            PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            AssetDatabase.SaveAssets();
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
