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
    /// 它刻意不复用 TapTap 的发布处理：后者会移除 StreamingAssets 中的视频，
    /// 而标准浏览器版必须保留它们以保证全部功能可用。
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

                WriteReport(outputDirectory, scenes, report);
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

        private static void WriteReport(string outputDirectory, string[] scenes, BuildReport report)
        {
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
                totalBytes = report.summary.totalSize,
                buildSeconds = report.summary.totalTime.TotalSeconds
            };
            File.WriteAllText(
                Path.Combine(outputDirectory, "web-release-report.json"),
                JsonUtility.ToJson(releaseReport, true));
        }
    }
}
