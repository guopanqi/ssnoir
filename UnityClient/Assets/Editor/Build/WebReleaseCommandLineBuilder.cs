using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SSNoir.Editor
{
    /// <summary>
    /// 构建可部署到普通静态站点的浏览器 WebGL 正式包。
    /// 平台参数属于 Web Release；字体、未使用资源和视频交付模式由公共资源准备器处理。
    /// </summary>
    public static class WebReleaseCommandLineBuilder
    {
        [Serializable]
        private sealed class ReleaseBuildReport
        {
            public string unityVersion = string.Empty;
            public string outputDirectory = string.Empty;
            public string[] scenes = Array.Empty<string>();
            public string compressionFormat = string.Empty;
            public bool developmentBuild;
            public bool dataCaching;
            public bool includesStreamingAssets;
            public string videoMode = string.Empty;
            public string[] excludedAssets = Array.Empty<string>();
            public long embeddedCutsceneBytes;
            public ulong totalBytes;
            public double buildSeconds;
        }

        public static void Build()
        {
            try
            {
                string outputDirectory = RequireOutputDirectory();
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                    throw new InvalidOperationException("Web 正式构建必须使用 WebGL BuildTarget。");

                string[] scenes = EditorBuildSettings.scenes
                    .Where(scene => scene.enabled)
                    .Select(scene => scene.path)
                    .ToArray();
                if (scenes.Length == 0)
                    throw new InvalidOperationException("Build Settings 中没有启用的场景。");

                Directory.CreateDirectory(outputDirectory);
                BuildAssetPreparer.Result prepared =
                    BuildAssetPreparer.PrepareFromEnvironment("WebRelease");
                ConfigureReleaseBuild();

                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputDirectory,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new BuildFailedException(
                        $"WebGL 正式构建失败: {report.summary.result}，错误 {report.summary.totalErrors} 个。");
                }

                WriteReport(outputDirectory, scenes, report, prepared);
                Debug.Log($"[WebRelease] 构建完成: {outputDirectory}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw new BuildFailedException($"Web 正式构建失败: {exception.Message}");
            }
        }

        private static string RequireOutputDirectory()
        {
            string path = Environment.GetEnvironmentVariable("SSNOIR_WEB_RELEASE_OUTPUT");
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("缺少环境变量 SSNOIR_WEB_RELEASE_OUTPUT。");
            return Path.GetFullPath(path);
        }

        private static void ConfigureReleaseBuild()
        {
            // 发布服务器需要按 Content-Encoding: br 提供 .br 文件；换来更小的下载体积。
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
            EditorUserBuildSettings.development = false;
        }

        private static void WriteReport(
            string outputDirectory,
            string[] scenes,
            BuildReport report,
            BuildAssetPreparer.Result prepared)
        {
            string[] excludedAssets = prepared.Plan.exclude
                .Select(exclusion => exclusion.path)
                .ToArray();
            var releaseReport = new ReleaseBuildReport
            {
                unityVersion = Application.unityVersion,
                outputDirectory = outputDirectory,
                scenes = scenes,
                compressionFormat = PlayerSettings.WebGL.compressionFormat.ToString(),
                developmentBuild = EditorUserBuildSettings.development,
                dataCaching = PlayerSettings.WebGL.dataCaching,
                includesStreamingAssets = Directory.Exists(
                    Path.Combine(Application.streamingAssetsPath, "Cutscenes")),
                videoMode = prepared.VideoMode,
                excludedAssets = excludedAssets,
                embeddedCutsceneBytes = prepared.EmbeddedCutsceneBytes,
                totalBytes = report.summary.totalSize,
                buildSeconds = report.summary.totalTime.TotalSeconds
            };
            File.WriteAllText(
                Path.Combine(outputDirectory, "web-release-report.json"),
                JsonUtility.ToJson(releaseReport, true));
        }
    }
}
