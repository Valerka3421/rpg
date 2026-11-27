using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Aethelgard.EditorTools
{
    /// <summary>Headless-подготовка проекта: сцена Boot с камерой и GameApp,
    /// добавленная в Build Settings. Запуск:
    /// Unity -batchmode -executeMethod Aethelgard.EditorTools.Bootstrapper.Setup</summary>
    public static class Bootstrapper
    {
        public static void Setup()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();

            new GameObject("GameApp").AddComponent<Aethelgard.GameApp>();

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Boot.unity");

            var buildScenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Boot.unity", true) };
            EditorBuildSettings.scenes = buildScenes;

            Debug.Log("Bootstrapper.Setup done");
        }

        public static void BuildWin64()
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Boot.unity" },
                target = BuildTarget.StandaloneWindows64,
                locationPathName = "Builds/Win64/Aethelgard.exe",
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"Build result: {report.summary.result}, size={report.summary.totalSize}, errors={report.summary.totalErrors}");
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.Exception("Build failed: " + report.summary.result);
        }
    }
}
