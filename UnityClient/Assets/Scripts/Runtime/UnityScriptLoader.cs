using System.Collections.Generic;
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

        public List<string> LoadSceneNames()
        {
            var sceneNames = new List<string>();
            foreach (TextAsset sceneAsset in Resources.LoadAll<TextAsset>("Content/scenes"))
            {
                string sceneName = sceneAsset.name;
                if (!sceneNames.Contains(sceneName))
                    sceneNames.Add(sceneName);
            }
            sceneNames.Sort();
            return sceneNames;
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
