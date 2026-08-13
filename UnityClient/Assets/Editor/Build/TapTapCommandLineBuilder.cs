using System;
using System.Collections.Generic;
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
        private const string RegularFontName = "SourceHanSerifCN-Regular.ttf";
        private const string SemiboldFontName = "SourceHanSerifCN-SemiBold.ttf";
        private const string ResourceFontDirectory = "Assets/Resources/Fonts";
        private const string StreamingFontDirectory = "Assets/StreamingAssets/Content/assets/fonts";
        private const string CutsceneVideoDirectory = "Assets/StreamingAssets/Cutscenes";

        [Serializable]
        private sealed class ReleasePlan
        {
            public int version;
            public CutsceneVideoPlan cutsceneVideos = new CutsceneVideoPlan();
            public ReleaseExclusion[] exclude = Array.Empty<ReleaseExclusion>();
        }

        [Serializable]
        private sealed class CutsceneVideoPlan
        {
            public string[] keep = Array.Empty<string>();
        }

        [Serializable]
        private sealed class ReleaseExclusion
        {
            public string path = string.Empty;
            public string reason = string.Empty;
        }

        [Serializable]
        private sealed class UnityBuildReport
        {
            public string unityVersion = string.Empty;
            public string outputDirectory = string.Empty;
            public string webGLTextureSubtarget = string.Empty;
            public bool unitySplashScreenEnabled;
            public string[] excludedAssets = Array.Empty<string>();
            public string[] keptCutsceneVideos = Array.Empty<string>();
            public string[] prunedCutsceneVideos = Array.Empty<string>();
            public long embeddedCutsceneBytes;
            public long gameZipBytes;
            public long wasmSplitZipBytes;
            public bool dataHostedRemotely;
            public string dataCdnUrl = string.Empty;
            public double releaseAssetPreparationSeconds;
            public double tapTapSdkBuildSeconds;
            public double archiveValidationSeconds;
        }

        public static void Build()
        {
            try
            {
                string planPath = RequireEnvironmentPath("SSNOIR_TAPTAP_RELEASE_PLAN", File.Exists);
                string fontDirectory = RequireEnvironmentPath("SSNOIR_TAPTAP_FONT_DIR", Directory.Exists);
                string outputDirectory = RequireEnvironmentPath("SSNOIR_TAPTAP_OUTPUT", _ => true);
                string dataCdnUrl = ReadOptionalDataCdnUrl();

                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                    throw new InvalidOperationException("TapTap 精简构建必须使用 WebGL BuildTarget。");

                Directory.CreateDirectory(outputDirectory);
                var preparationTimer = Stopwatch.StartNew();
                ReleasePlan plan = LoadPlan(planPath);
                string[] prunedCutsceneVideos = ApplyReleaseAssets(plan, fontDirectory);
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
                    plan,
                    prunedCutsceneVideos,
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
                throw new BuildFailedException($"TapTap 精简构建失败: {exception.Message}");
            }
        }

        private static ReleasePlan LoadPlan(string planPath)
        {
            var plan = JsonUtility.FromJson<ReleasePlan>(File.ReadAllText(planPath));
            if (plan == null || plan.version != 1)
                throw new InvalidOperationException($"不支持的发布计划格式: {planPath}");
            if (plan.exclude == null)
                throw new InvalidOperationException("发布计划缺少 exclude 数组。");
            if (plan.cutsceneVideos == null || plan.cutsceneVideos.keep == null)
                throw new InvalidOperationException("发布计划缺少 cutsceneVideos.keep 数组。");
            return plan;
        }

        private static string[] ApplyReleaseAssets(ReleasePlan plan, string generatedFontDirectory)
        {
            CopySubsetFont(generatedFontDirectory, RegularFontName);
            CopySubsetFont(generatedFontDirectory, SemiboldFontName);

            DeleteAssetIfPresent($"{ResourceFontDirectory}/MiSans-Regular.ttf");
            DeleteAssetIfPresent($"{ResourceFontDirectory}/MiSans-Semibold.ttf");
            DeleteAssetIfPresent(StreamingFontDirectory);
            string[] prunedCutsceneVideos = ApplyCutsceneVideoPlan(plan.cutsceneVideos);

            var seenPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (ReleaseExclusion exclusion in plan.exclude)
            {
                if (exclusion == null || string.IsNullOrWhiteSpace(exclusion.path))
                    throw new InvalidOperationException("发布计划包含空的排除路径。");
                string assetPath = NormalizeAndValidateAssetPath(exclusion.path);
                if (!seenPaths.Add(assetPath))
                    throw new InvalidOperationException($"发布计划包含重复路径: {assetPath}");
                DeleteRequiredAsset(assetPath, exclusion.reason);
            }

            DeleteFinderMetadata();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            RequireImportedFont($"{ResourceFontDirectory}/{RegularFontName}");
            RequireImportedFont($"{ResourceFontDirectory}/{SemiboldFontName}");
            return prunedCutsceneVideos;
        }

        private static string[] ApplyCutsceneVideoPlan(CutsceneVideoPlan plan)
        {
            string fullDirectory = Path.GetFullPath(CutsceneVideoDirectory);
            if (!Directory.Exists(fullDirectory))
                throw new DirectoryNotFoundException($"过场视频目录不存在: {fullDirectory}");

            var keep = new HashSet<string>(StringComparer.Ordinal);
            foreach (string fileName in plan.keep)
            {
                if (string.IsNullOrWhiteSpace(fileName)
                    || Path.GetFileName(fileName) != fileName
                    || !fileName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"过场视频保留项必须是单个 mp4 文件名: {fileName}");
                }
                if (!keep.Add(fileName))
                    throw new InvalidOperationException($"过场视频保留清单包含重复项: {fileName}");

                string requiredPath = Path.Combine(fullDirectory, fileName);
                if (!File.Exists(requiredPath))
                    throw new FileNotFoundException($"场景需要的过场视频不存在: {requiredPath}");
            }

            var pruned = new List<string>();
            foreach (string fullPath in Directory.GetFiles(fullDirectory, "*.mp4", SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(fullPath);
                if (keep.Contains(fileName))
                {
                    Debug.Log($"[TapTapRelease] 保留过场视频: {fileName}");
                    continue;
                }

                string assetPath = $"{CutsceneVideoDirectory}/{fileName}";
                if (!AssetDatabase.DeleteAsset(assetPath))
                    throw new IOException($"无法从 staging 删除未使用的过场视频: {assetPath}");
                pruned.Add(fileName);
                Debug.Log($"[TapTapRelease] 排除未使用过场视频: {fileName}");
            }

            pruned.Sort(StringComparer.Ordinal);
            return pruned.ToArray();
        }

        private static void CopySubsetFont(string generatedFontDirectory, string fontName)
        {
            string sourcePath = Path.Combine(generatedFontDirectory, fontName);
            if (!File.Exists(sourcePath) || new FileInfo(sourcePath).Length == 0)
                throw new InvalidOperationException($"字体子集不存在或为空: {sourcePath}");

            string targetAssetPath = $"{ResourceFontDirectory}/{fontName}";
            string targetPath = Path.GetFullPath(targetAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
            File.Copy(sourcePath, targetPath, true);
            Debug.Log($"[TapTapRelease] 字体子集: {fontName} ({new FileInfo(sourcePath).Length} bytes)");
        }

        private static void RequireImportedFont(string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<Font>(assetPath) == null)
                throw new InvalidOperationException($"Unity 无法导入字体子集: {assetPath}");
        }

        private static string NormalizeAndValidateAssetPath(string value)
        {
            string path = value.Replace('\\', '/').Trim().TrimEnd('/');
            bool allowedRoot = path.StartsWith("Assets/Resources/", StringComparison.Ordinal)
                || path.StartsWith("Assets/StreamingAssets/", StringComparison.Ordinal);
            if (!allowedRoot || path.Contains("/../") || path.EndsWith("/..", StringComparison.Ordinal))
                throw new InvalidOperationException($"排除路径不在允许范围内: {value}");
            return path;
        }

        private static void DeleteRequiredAsset(string assetPath, string reason)
        {
            string fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                throw new FileNotFoundException($"发布计划中的资源不存在，请让 Agent 重新审查计划: {assetPath}");
            if (!AssetDatabase.DeleteAsset(assetPath))
                throw new IOException($"无法从 staging 删除资源: {assetPath}");
            Debug.Log($"[TapTapRelease] 排除资源: {assetPath}；原因: {reason}");
        }

        private static void DeleteAssetIfPresent(string assetPath)
        {
            string fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                return;
            if (!AssetDatabase.DeleteAsset(assetPath))
                throw new IOException($"无法从 staging 删除资源: {assetPath}");
            Debug.Log($"[TapTapRelease] 排除固定资源: {assetPath}");
        }

        private static void DeleteFinderMetadata()
        {
            foreach (string path in Directory.GetFiles(Application.dataPath, ".DS_Store", SearchOption.AllDirectories))
            {
                File.Delete(path);
                Debug.Log($"[TapTapRelease] 清理 Finder 元数据: {path}");
            }
        }

        private static void ConfigureTapTapRelease(string outputDirectory, string dataCdnUrl)
        {
            // 必须在动 MiniGameConfig 之前：改脚本宏会触发一次重编译，中途的域重载会把
            // config 上还没写盘的字段（含数据包压缩开关）冲回磁盘上的旧值。
            DeclareStreamingAssetsRemoved();

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

            // The review build intentionally excludes local cutscene videos. TapTap's native
            // decoder cannot read Unity WebGL's packaged StreamingAssets paths; release builds
            // will switch to HTTPS URLs when remote hosting is configured.
            config.CompileOptions.DeleteStreamingAssets = true;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 把「这个包没有 StreamingAssets」告诉运行时代码。删文件是构建期的事，但运行时必须
        /// 知道——否则 CutscenePlayer 仍会为不存在的片子创建 VideoPlayer，而小游戏容器的
        /// _JS_Video_Create 会抛 JS 异常穿出 PlayerLoop，把整个引擎卡死在那一帧。
        /// </summary>
        private static void DeclareStreamingAssetsRemoved()
        {
            const string symbol = "SSNOIR_NO_STREAMING_ASSETS";
            string existing = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL);
            var symbols = new List<string>();
            foreach (string entry in existing.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = entry.Trim();
                if (trimmed.Length == 0)
                    continue;
                if (trimmed == symbol)
                    return;
                symbols.Add(trimmed);
            }

            symbols.Add(symbol);
            PlayerSettings.SetScriptingDefineSymbols(
                NamedBuildTarget.WebGL, string.Join(";", symbols));
            Debug.Log($"[TapTapRelease] 已为本次构建加入脚本宏 {symbol}。");
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
            ReleasePlan plan,
            string[] prunedCutsceneVideos,
            string gameZip,
            string wasmSplitZip,
            string dataCdnUrl,
            double releaseAssetPreparationSeconds,
            double tapTapSdkBuildSeconds,
            double archiveValidationSeconds)
        {
            var excluded = new string[plan.exclude.Length];
            for (int index = 0; index < plan.exclude.Length; index++)
                excluded[index] = plan.exclude[index].path;

            var report = new UnityBuildReport
            {
                unityVersion = Application.unityVersion,
                outputDirectory = outputDirectory,
                webGLTextureSubtarget = EditorUserBuildSettings.webGLBuildSubtarget.ToString(),
                unitySplashScreenEnabled = PlayerSettings.SplashScreen.show,
                excludedAssets = excluded,
                keptCutsceneVideos = plan.cutsceneVideos.keep,
                prunedCutsceneVideos = prunedCutsceneVideos,
                embeddedCutsceneBytes = 0,
                gameZipBytes = new FileInfo(gameZip).Length,
                wasmSplitZipBytes = new FileInfo(wasmSplitZip).Length,
                dataHostedRemotely = !string.IsNullOrEmpty(dataCdnUrl),
                dataCdnUrl = dataCdnUrl,
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
