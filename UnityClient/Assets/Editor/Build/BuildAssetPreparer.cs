using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SSNoir.Editor
{
    /// <summary>
    /// 三种构建共用的 staging 资源准备。这里只允许修改 staging 工程：字体子集、确定无用的
    /// Resources、未使用视频和视频交付模式都在进入平台 builder 之前一次性处理。
    /// </summary>
    public static class BuildAssetPreparer
    {
        private const string RegularFontName = "SourceHanSerifCN-Regular.ttf";
        private const string SemiboldFontName = "SourceHanSerifCN-SemiBold.ttf";
        private const string ResourceFontDirectory = "Assets/Resources/Fonts";
        private const string RuntimeConfigAssetPath = "Assets/Resources/BuildRuntimeConfig.json";
        private const string RuntimeConfigResourcePath = "BuildRuntimeConfig";
        private const string CutsceneVideoDirectory = "Assets/StreamingAssets/Cutscenes";

        [Serializable]
        public sealed class ResourcePlan
        {
            public int version;
            public CutsceneVideoPlan cutsceneVideos = new CutsceneVideoPlan();
            public ResourceExclusion[] exclude = Array.Empty<ResourceExclusion>();
        }

        [Serializable]
        public sealed class CutsceneVideoPlan
        {
            public string[] keep = Array.Empty<string>();
        }

        [Serializable]
        public sealed class ResourceExclusion
        {
            public string path = string.Empty;
            public string reason = string.Empty;
        }

        [Serializable]
        private sealed class RuntimeConfig
        {
            public bool cutsceneVideosDisabled;
            public string cutsceneVideoBaseUrl = string.Empty;
        }

        public sealed class Result
        {
            public ResourcePlan Plan { get; }
            public string VideoMode { get; }
            public string[] PrunedCutsceneVideos { get; }
            public long EmbeddedCutsceneBytes { get; }
            public string RemoteAssetBaseUrl { get; }

            public Result(
                ResourcePlan plan,
                string videoMode,
                string[] prunedCutsceneVideos,
                long embeddedCutsceneBytes,
                string remoteAssetBaseUrl)
            {
                Plan = plan;
                VideoMode = videoMode;
                PrunedCutsceneVideos = prunedCutsceneVideos;
                EmbeddedCutsceneBytes = embeddedCutsceneBytes;
                RemoteAssetBaseUrl = remoteAssetBaseUrl;
            }
        }

        public static Result PrepareFromEnvironment(string buildLabel)
        {
            string planPath = RequireEnvironmentPath("SSNOIR_BUILD_RESOURCE_PLAN", File.Exists);
            string fontDirectory = RequireEnvironmentPath("SSNOIR_BUILD_FONT_DIR", Directory.Exists);
            string videoMode = RequireVideoMode();
            string remoteAssetBaseUrl = videoMode == "remote"
                ? RequireHttpsUrl("SSNOIR_BUILD_REMOTE_ASSET_URL")
                : string.Empty;
            string remoteAssetOutput = videoMode == "remote"
                ? RequireEnvironmentPath("SSNOIR_BUILD_REMOTE_ASSET_OUTPUT", Directory.Exists)
                : string.Empty;

            ResourcePlan plan = LoadPlan(planPath);
            CopySubsetFont(fontDirectory, RegularFontName, buildLabel);
            CopySubsetFont(fontDirectory, SemiboldFontName, buildLabel);
            DeleteAssetIfPresent($"{ResourceFontDirectory}/MiSans-Regular.ttf", buildLabel);
            DeleteAssetIfPresent($"{ResourceFontDirectory}/MiSans-Semibold.ttf", buildLabel);

            var seenPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (ResourceExclusion exclusion in plan.exclude)
            {
                if (exclusion == null || string.IsNullOrWhiteSpace(exclusion.path))
                    throw new InvalidOperationException("公共资源计划包含空的排除路径。");
                string assetPath = NormalizeAndValidateAssetPath(exclusion.path);
                if (!seenPaths.Add(assetPath))
                    throw new InvalidOperationException($"公共资源计划包含重复路径: {assetPath}");
                DeleteRequiredAsset(assetPath, exclusion.reason, buildLabel);
            }

            (string[] prunedVideos, long embeddedBytes) = PrepareCutsceneVideos(
                plan.cutsceneVideos,
                videoMode,
                remoteAssetOutput,
                buildLabel);
            WriteRuntimeConfig(videoMode, remoteAssetBaseUrl);
            DeleteFinderMetadata(buildLabel);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            RequireImportedFont($"{ResourceFontDirectory}/{RegularFontName}");
            RequireImportedFont($"{ResourceFontDirectory}/{SemiboldFontName}");
            if (AssetDatabase.LoadAssetAtPath<TextAsset>(RuntimeConfigAssetPath) == null)
                throw new InvalidOperationException($"Unity 无法导入运行时构建配置: {RuntimeConfigAssetPath}");

            return new Result(plan, videoMode, prunedVideos, embeddedBytes, remoteAssetBaseUrl);
        }

        // 独立验证公共资源层时使用；正式入口由各平台 builder 直接调用 PrepareFromEnvironment。
        public static void PrepareOnlyFromCommandLine()
        {
            Result result = PrepareFromEnvironment("BuildAssetPreparer");
            Debug.Log(
                $"[BuildAssetPreparer] 完成: videoMode={result.VideoMode}, "
                + $"prunedVideos={result.PrunedCutsceneVideos.Length}, "
                + $"embeddedCutsceneBytes={result.EmbeddedCutsceneBytes}");
        }

        private static ResourcePlan LoadPlan(string planPath)
        {
            var plan = JsonUtility.FromJson<ResourcePlan>(File.ReadAllText(planPath));
            if (plan == null || plan.version != 1)
                throw new InvalidOperationException($"不支持的公共资源计划格式: {planPath}");
            if (plan.exclude == null)
                throw new InvalidOperationException("公共资源计划缺少 exclude 数组。");
            if (plan.cutsceneVideos == null || plan.cutsceneVideos.keep == null)
                throw new InvalidOperationException("公共资源计划缺少 cutsceneVideos.keep 数组。");
            return plan;
        }

        private static (string[] pruned, long embeddedBytes) PrepareCutsceneVideos(
            CutsceneVideoPlan plan,
            string videoMode,
            string remoteAssetOutput,
            string buildLabel)
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

            if (videoMode == "remote")
            {
                string remoteCutsceneDirectory = Path.Combine(remoteAssetOutput, "Cutscenes");
                Directory.CreateDirectory(remoteCutsceneDirectory);
                foreach (string fileName in keep)
                {
                    string sourcePath = Path.Combine(fullDirectory, fileName);
                    string destinationPath = Path.Combine(remoteCutsceneDirectory, fileName);
                    File.Copy(sourcePath, destinationPath, overwrite: true);
                    Debug.Log($"[{buildLabel}] 远程过场资源: {fileName}");
                }
            }

            var pruned = new List<string>();
            long embeddedBytes = 0;
            foreach (string fullPath in Directory.GetFiles(fullDirectory, "*.mp4", SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(fullPath);
                bool embed = videoMode == "local" && keep.Contains(fileName);
                if (embed)
                {
                    embeddedBytes += new FileInfo(fullPath).Length;
                    Debug.Log($"[{buildLabel}] 包内过场视频: {fileName}");
                    continue;
                }

                string assetPath = $"{CutsceneVideoDirectory}/{fileName}";
                if (!AssetDatabase.DeleteAsset(assetPath))
                    throw new IOException($"无法从 staging 删除过场视频: {assetPath}");
                pruned.Add(fileName);
                Debug.Log($"[{buildLabel}] 排除本地过场视频: {fileName} ({videoMode})");
            }

            pruned.Sort(StringComparer.Ordinal);
            return (pruned.ToArray(), embeddedBytes);
        }

        private static void WriteRuntimeConfig(string videoMode, string remoteAssetBaseUrl)
        {
            var config = new RuntimeConfig
            {
                cutsceneVideosDisabled = videoMode == "none",
                cutsceneVideoBaseUrl = remoteAssetBaseUrl,
            };
            File.WriteAllText(RuntimeConfigAssetPath, JsonUtility.ToJson(config, prettyPrint: true));
        }

        private static void CopySubsetFont(string generatedFontDirectory, string fontName, string buildLabel)
        {
            string sourcePath = Path.Combine(generatedFontDirectory, fontName);
            if (!File.Exists(sourcePath) || new FileInfo(sourcePath).Length == 0)
                throw new InvalidOperationException($"字体子集不存在或为空: {sourcePath}");

            string targetAssetPath = $"{ResourceFontDirectory}/{fontName}";
            string targetPath = Path.GetFullPath(targetAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
            File.Copy(sourcePath, targetPath, overwrite: true);
            Debug.Log($"[{buildLabel}] 字体子集: {fontName} ({new FileInfo(sourcePath).Length} bytes)");
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
            if (path == RuntimeConfigAssetPath || path.StartsWith(ResourceFontDirectory + "/", StringComparison.Ordinal))
                throw new InvalidOperationException($"公共资源计划不能排除构建基础资源: {path}");
            return path;
        }

        private static void DeleteRequiredAsset(string assetPath, string reason, string buildLabel)
        {
            string fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            {
                // staging 会复用上一次构建已经裁掉的 Resources；同一资源计划再次执行时，
                // 缺失正是此前成功删除的正常结果，不能因此让发布构建失去幂等性。
                Debug.Log($"[{buildLabel}] 资源已不在 staging，跳过排除: {assetPath}；原因: {reason}");
                return;
            }
            if (!AssetDatabase.DeleteAsset(assetPath))
                throw new IOException($"无法从 staging 删除资源: {assetPath}");
            Debug.Log($"[{buildLabel}] 排除资源: {assetPath}；原因: {reason}");
        }

        private static void DeleteAssetIfPresent(string assetPath, string buildLabel)
        {
            string fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                return;
            if (!AssetDatabase.DeleteAsset(assetPath))
                throw new IOException($"无法从 staging 删除资源: {assetPath}");
            Debug.Log($"[{buildLabel}] 排除固定资源: {assetPath}");
        }

        private static void DeleteFinderMetadata(string buildLabel)
        {
            foreach (string path in Directory.GetFiles(Application.dataPath, ".DS_Store", SearchOption.AllDirectories))
            {
                File.Delete(path);
                Debug.Log($"[{buildLabel}] 清理 Finder 元数据: {path}");
            }
        }

        private static string RequireVideoMode()
        {
            string value = Environment.GetEnvironmentVariable("SSNOIR_BUILD_VIDEO_MODE");
            value = value?.Trim().ToLowerInvariant();
            if (value != "local" && value != "none" && value != "remote")
                throw new InvalidOperationException("SSNOIR_BUILD_VIDEO_MODE 必须是 local、none 或 remote。");
            return value;
        }

        private static string RequireHttpsUrl(string variableName)
        {
            string value = Environment.GetEnvironmentVariable(variableName);
            value = value?.Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(value)
                || !Uri.TryCreate(value, UriKind.Absolute, out Uri uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || string.IsNullOrWhiteSpace(uri.Host))
            {
                throw new InvalidOperationException($"{variableName} 必须是绝对 HTTPS 地址: {value}");
            }
            return value;
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
    }
}
