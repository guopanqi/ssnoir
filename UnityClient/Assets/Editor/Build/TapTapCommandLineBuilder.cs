using System;
using System.IO;
using System.IO.Compression;
using TapTapMiniGame;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace SSNoir.Editor
{
    public static class TapTapCommandLineBuilder
    {
        [Serializable]
        private sealed class UnityBuildReport
        {
            public string unityVersion = string.Empty;
            public string outputDirectory = string.Empty;
            public string webGLTextureSubtarget = string.Empty;
            public bool unitySplashScreenEnabled;
            public string[] excludedAssets = Array.Empty<string>();
            public string[] plannedCutsceneVideos = Array.Empty<string>();
            public string[] prunedCutsceneVideos = Array.Empty<string>();
            public long embeddedCutsceneBytes;
            public long gameZipBytes;
            public long wasmSplitZipBytes;
            public bool dataHostedRemotely;
            public string dataCdnUrl = string.Empty;
            public string videoMode = string.Empty;
            public string remoteAssetBaseUrl = string.Empty;
            public double releaseAssetPreparationSeconds;
            public double tapTapSdkBuildSeconds;
            public double archiveValidationSeconds;
        }

        public static void Build()
        {
            try
            {
                string outputDirectory = RequireEnvironmentPath("SSNOIR_TAPTAP_OUTPUT", _ => true);
                string dataCdnUrl = ReadOptionalDataCdnUrl();

                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                    throw new InvalidOperationException("TapTap Release 必须使用 WebGL BuildTarget。");

                Directory.CreateDirectory(outputDirectory);
                var preparationTimer = Stopwatch.StartNew();
                BuildAssetPreparer.Result prepared =
                    BuildAssetPreparer.PrepareFromEnvironment("TapTapRelease");
                ConfigureTapTapRelease(outputDirectory, dataCdnUrl);
                preparationTimer.Stop();

                var sdkBuildTimer = Stopwatch.StartNew();
                bool started = TapTapBuildWindowHelper.BuildTapTapMiniGame(false);
                sdkBuildTimer.Stop();
                if (!started)
                    throw new InvalidOperationException("TapTap SDK 拒绝启动构建，详情见 Unity 日志。");

                var validationTimer = Stopwatch.StartNew();
                string gameZip = Path.Combine(outputDirectory, "game.zip");
                string wasmSplitZip = Path.Combine(outputDirectory, "game_wasm_split.zip");
                RequireArtifact(gameZip);
                RequireArtifact(wasmSplitZip);
                ValidateCutsceneVideosExcluded(gameZip, wasmSplitZip);
                validationTimer.Stop();
                WriteUnityReport(
                    outputDirectory,
                    prepared,
                    gameZip,
                    wasmSplitZip,
                    dataCdnUrl,
                    preparationTimer.Elapsed.TotalSeconds,
                    sdkBuildTimer.Elapsed.TotalSeconds,
                    validationTimer.Elapsed.TotalSeconds);
                Debug.Log($"[TapTapRelease] 构建完成: {outputDirectory}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw new BuildFailedException($"TapTap Release 构建失败: {exception.Message}");
            }
        }

        private static void ConfigureTapTapRelease(string outputDirectory, string dataCdnUrl)
        {
            var config = TapTapUtil.GetEditorConf(true);
            if (config == null)
                throw new InvalidOperationException("无法加载 TapTap MiniGameConfig.asset。");
            if (string.IsNullOrWhiteSpace(config.ProjectConf.Appid))
                throw new InvalidOperationException("TapTap AppID 为空。");

            // Unity stores this choice in the local EditorUserBuildSettings under Library,
            // not in versioned ProjectSettings. The release build runs from a staging project
            // with its own persistent Library, so pin the mobile WebGL format explicitly.
            EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.ETC2;

            // On paid Unity licenses this removes the only built-in DXT5 texture from the
            // package. Unity Personal forces the splash back on; that license-owned texture is
            // reported below as the one known exception and cannot be changed by an importer.
            PlayerSettings.SplashScreen.show = false;
            ValidateEtc2ReleaseSettings();

            config.ProjectConf.DST = outputDirectory;

            bool dataHostedRemotely = !string.IsNullOrEmpty(dataCdnUrl);
            config.ProjectConf.assetLoadType = dataHostedRemotely ? 0 : 1;
            config.ProjectConf.CDN = dataHostedRemotely ? dataCdnUrl : string.Empty;
            Debug.Log(dataHostedRemotely
                ? $"[TapTapRelease] 首包 Data 使用 CDN: {dataCdnUrl}"
                : "[TapTapRelease] 首包 Data 使用小游戏分包。");

            // 首包资源走 Brotli：实际下载的是未压缩的那一份，压完大约减半。代价是首次启动多
            // 约 200ms 解压，换掉的是好几秒下载——初始化耗时那条指标上这笔账怎么算都划得来。
            config.ProjectConf.compressDataPackage = true;

            config.CompileOptions.DevelopBuild = false;
            config.CompileOptions.ScriptDebugging = false;
            config.CompileOptions.AutoProfile = false;
            config.CompileOptions.ScriptOnly = false;
            config.CompileOptions.Il2CppOptimizeSize = true;
            config.CompileOptions.profilingFuncs = false;
            config.CompileOptions.ProfilingMemory = false;
            config.CompileOptions.enableProfileStats = false;
            config.CompileOptions.showMonitorSuggestModal = false;

            // TapTap 的本地 StreamingAssets 路径不能交给小游戏原生解码器。公共资源准备器已经
            // 根据构建模式把视频改为远程 HTTPS，或显式禁用；包内始终删除 StreamingAssets。
            config.CompileOptions.DeleteStreamingAssets = true;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        private static void ValidateEtc2ReleaseSettings()
        {
            if (EditorUserBuildSettings.webGLBuildSubtarget != WebGLTextureSubtarget.ETC2)
            {
                throw new InvalidOperationException(
                    $"WebGL 纹理子目标必须是 ETC2，当前为 " +
                    $"{EditorUserBuildSettings.webGLBuildSubtarget}。");
            }
            if (PlayerSettings.SplashScreen.show)
                Debug.LogWarning(
                    "[TapTapRelease] 当前 Unity 许可证强制显示 Splash Screen；" +
                    "内置 Unity Logo 会作为唯一已知 DXT5/BC3 例外进入包体。" +
                    "工程纹理仍必须全部遵守 ETC2 规则。");

            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
                    continue;

                TextureImporterPlatformSettings settings =
                    importer.GetPlatformTextureSettings("WebGL");
                if (!settings.overridden)
                    continue;

                switch (settings.format)
                {
                    case TextureImporterFormat.DXT1:
                    case TextureImporterFormat.DXT5:
                    case TextureImporterFormat.DXT1Crunched:
                    case TextureImporterFormat.DXT5Crunched:
                        throw new InvalidOperationException(
                            $"WebGL 纹理禁止使用 DXT/BC 格式: {assetPath} ({settings.format})。");
                }
            }
        }

        private static string RequireEnvironmentPath(string variableName, Func<string, bool> predicate)
        {
            string value = Environment.GetEnvironmentVariable(variableName);
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"缺少环境变量: {variableName}");
            string path = Path.GetFullPath(value);
            if (!predicate(path))
                throw new FileNotFoundException($"环境变量 {variableName} 指向的路径不存在: {path}");
            return path;
        }

        private static string ReadOptionalDataCdnUrl()
        {
            string value = Environment.GetEnvironmentVariable("SSNOIR_TAPTAP_CDN_URL");
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            value = value.Trim().TrimEnd('/');
            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || string.IsNullOrWhiteSpace(uri.Host))
            {
                throw new InvalidOperationException(
                    $"SSNOIR_TAPTAP_CDN_URL 必须是绝对 HTTPS 地址: {value}");
            }
            return value;
        }

        private static void RequireArtifact(string path)
        {
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
                throw new InvalidOperationException($"TapTap 构建产物不存在或为空: {path}");
        }

        private static void ValidateCutsceneVideosExcluded(params string[] archivePaths)
        {
            foreach (string archivePath in archivePaths)
            {
                using var fileStream = File.OpenRead(archivePath);
                using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName.StartsWith(
                            "StreamingAssets/Cutscenes/",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"过审包不应包含本地过场视频: {entry.FullName}");
                    }
                }

                Debug.Log($"[TapTapRelease] 已确认 {Path.GetFileName(archivePath)} 不包含本地过场视频");
            }
        }

        private static void WriteUnityReport(
            string outputDirectory,
            BuildAssetPreparer.Result prepared,
            string gameZip,
            string wasmSplitZip,
            string dataCdnUrl,
            double releaseAssetPreparationSeconds,
            double tapTapSdkBuildSeconds,
            double archiveValidationSeconds)
        {
            var excluded = new string[prepared.Plan.exclude.Length];
            for (int index = 0; index < prepared.Plan.exclude.Length; index++)
                excluded[index] = prepared.Plan.exclude[index].path;

            var report = new UnityBuildReport
            {
                unityVersion = Application.unityVersion,
                outputDirectory = outputDirectory,
                webGLTextureSubtarget = EditorUserBuildSettings.webGLBuildSubtarget.ToString(),
                unitySplashScreenEnabled = PlayerSettings.SplashScreen.show,
                excludedAssets = excluded,
                plannedCutsceneVideos = prepared.Plan.cutsceneVideos.keep,
                prunedCutsceneVideos = prepared.PrunedCutsceneVideos,
                embeddedCutsceneBytes = prepared.EmbeddedCutsceneBytes,
                gameZipBytes = new FileInfo(gameZip).Length,
                wasmSplitZipBytes = new FileInfo(wasmSplitZip).Length,
                dataHostedRemotely = !string.IsNullOrEmpty(dataCdnUrl),
                dataCdnUrl = dataCdnUrl,
                videoMode = prepared.VideoMode,
                remoteAssetBaseUrl = prepared.RemoteAssetBaseUrl,
                releaseAssetPreparationSeconds = releaseAssetPreparationSeconds,
                tapTapSdkBuildSeconds = tapTapSdkBuildSeconds,
                archiveValidationSeconds = archiveValidationSeconds,
            };
            File.WriteAllText(
                Path.Combine(outputDirectory, "unity-build-report.json"),
                JsonUtility.ToJson(report, true));
        }
    }
}
