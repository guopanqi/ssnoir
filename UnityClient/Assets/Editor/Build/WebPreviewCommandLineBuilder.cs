using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SSNoir.Editor
{
    public static class WebPreviewCommandLineBuilder
    {
        [Serializable]
        private sealed class PreviewBuildReport
        {
            public string unityVersion = string.Empty;
            public string outputDirectory = string.Empty;
            public string[] scenes = Array.Empty<string>();
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
                    throw new InvalidOperationException("手机 Web 预览必须使用 WebGL BuildTarget。");

                string[] scenes = EditorBuildSettings.scenes
                    .Where(scene => scene.enabled)
                    .Select(scene => scene.path)
                    .ToArray();
                if (scenes.Length == 0)
                    throw new InvalidOperationException("Build Settings 中没有启用的场景。");

                Directory.CreateDirectory(outputDirectory);
                BuildAssetPreparer.Result prepared =
                    BuildAssetPreparer.PrepareFromEnvironment("WebPreview");
                ConfigurePreviewBuild();

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
                        $"WebGL 构建失败: {report.summary.result}，错误 {report.summary.totalErrors} 个。");
                }

                WriteReport(outputDirectory, scenes, report, prepared);
                Debug.Log($"[WebPreview] 构建完成: {outputDirectory}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw new BuildFailedException($"手机 Web 预览构建失败: {exception.Message}");
            }
        }

        private static string RequireOutputDirectory()
        {
            string path = Environment.GetEnvironmentVariable("SSNOIR_WEB_PREVIEW_OUTPUT");
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("缺少环境变量 SSNOIR_WEB_PREVIEW_OUTPUT。");
            return Path.GetFullPath(path);
        }

        private static void ConfigurePreviewBuild()
        {
            // 关闭传输压缩后，普通静态服务器无需配置 Content-Encoding，手机局域网预览更稳定。
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.dataCaching = false;
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
            var previewReport = new PreviewBuildReport
            {
                unityVersion = Application.unityVersion,
                outputDirectory = outputDirectory,
                scenes = scenes,
                videoMode = prepared.VideoMode,
                excludedAssets = excludedAssets,
                embeddedCutsceneBytes = prepared.EmbeddedCutsceneBytes,
                totalBytes = report.summary.totalSize,
                buildSeconds = report.summary.totalTime.TotalSeconds
            };
            File.WriteAllText(
                Path.Combine(outputDirectory, "web-preview-report.json"),
                JsonUtility.ToJson(previewReport, true));
        }
    }
}
