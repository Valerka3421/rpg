#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Aethelgard.EditorTools
{
    /// <summary>Сборка standalone-версии игры (Aethelgard → Build Game).</summary>
    public static class BuildGame
    {
        public static void BuildWindows()
        {
            // Окно игры 1920×1080 по умолчанию (совпадает с дизайн-разрешением)
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;

            var scenes = new[] { "Assets/Scenes/Empty.unity" };
            var report = BuildPipeline.BuildPlayer(scenes,
                "Build/Aethelgard.exe",
                BuildTarget.StandaloneWindows64,
                BuildOptions.None);
            Debug.Log($"Build done: {report.summary.result}, " +
                $"{report.summary.totalSize / (1024 * 1024)} MB");
        }
    }
}
#endif
