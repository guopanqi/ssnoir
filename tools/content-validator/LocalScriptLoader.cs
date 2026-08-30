using System;
using System.IO;
using SSNoir.Core;

namespace SSNoir
{
    public class LocalScriptLoader : IScriptLoader
    {
        public string LoadScriptText(string path)
        {
            string contentRoot = ProjectPaths.ContentRoot;
            string fullPath = Path.GetFullPath(Path.Combine(contentRoot, path));
            string relativePath = Path.GetRelativePath(contentRoot, fullPath);
            if (relativePath == ".." || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                throw new InvalidDataException($"脚本路径越出了 Content 目录：{path}");
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"找不到 Scheme 脚本：{path}", fullPath);

            return File.ReadAllText(fullPath);
        }
    }

    internal static class ProjectPaths
    {
        private static readonly Lazy<string> RepoRootValue = new(FindRepoRoot);

        public static string RepoRoot => RepoRootValue.Value;
        public static string ContentRoot => Path.Combine(
            RepoRoot, "UnityClient", "Assets", "Resources", "Content");

        private static string FindRepoRoot()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                for (DirectoryInfo? directory = new(start); directory != null; directory = directory.Parent)
                {
                    string content = Path.Combine(
                        directory.FullName, "UnityClient", "Assets", "Resources", "Content");
                    if (Directory.Exists(content) && Directory.Exists(Path.Combine(directory.FullName, "Engine")))
                        return directory.FullName;
                }
            }

            throw new DirectoryNotFoundException(
                "找不到 SSNoir 工程根目录（应同时包含 Engine 与 UnityClient/Assets/Resources/Content）。");
        }
    }
}
