using UnityEngine;
using SSNoir.Core;

namespace SSNoir
{
    public class UnityScriptLoader : IScriptLoader
    {
        public string LoadScriptText(string path)
        {
            string resourcePath = ResourcePath(path);
            TextAsset resource = Resources.Load<TextAsset>(resourcePath);
            if (resource != null)
                return resource.text;

            throw new System.IO.FileNotFoundException(
                $"Scheme 脚本不存在或未正确导入: Resources/{resourcePath} ({path})");
        }

        private static string ResourcePath(string scriptPath)
        {
            string normalized = scriptPath.Replace('\\', '/').TrimStart('/');
            if (normalized.EndsWith(".scm", System.StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(0, normalized.Length - ".scm".Length);
            return "Content/" + normalized;
        }
    }
}
