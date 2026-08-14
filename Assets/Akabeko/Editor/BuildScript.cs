using UnityEditor;
using System.IO;
using UnityEngine;

namespace Akabeko.Editor
{
    public static class BuildScript
    {
        [MenuItem("Build/Build WebGL")]
        public static void PerformWebGLBuild()
        {
            string buildPath = Path.Combine(Directory.GetCurrentDirectory(), "Builds/WebGL");
            
            // ビルドディレクトリがなければ作成
            if (!Directory.Exists(buildPath))
            {
                Directory.CreateDirectory(buildPath);
            }

            // シーンのリスト
            string[] scenes = new string[] { "Assets/Akabeko/Scenes/MainScene.unity" };

            // 静的ホスティングでのBrotli圧縮エラーを防ぐため、解凍フォールバックを有効化
            PlayerSettings.WebGL.decompressionFallback = true;

            // ビルド設定
            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
            buildPlayerOptions.scenes = scenes;
            buildPlayerOptions.locationPathName = buildPath;
            buildPlayerOptions.target = BuildTarget.WebGL;
            buildPlayerOptions.options = BuildOptions.None;

            Debug.Log("[BuildScript] Starting WebGL Build...");
            var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            var summary = report.summary;

            if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"[BuildScript] WebGL Build Succeeded! Size: {summary.totalSize} bytes");
            }
            else if (summary.result == UnityEditor.Build.Reporting.BuildResult.Failed)
            {
                Debug.LogError($"[BuildScript] WebGL Build Failed! Errors: {summary.totalErrors}");
            }
        }
    }
}
