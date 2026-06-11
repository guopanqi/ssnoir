using System.IO;
using System.Collections.Generic;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir
{
    public class UnityScriptLoader : IScriptLoader
    {
        public string LoadScriptText(string path)
        {
            string fullPath = Path.Combine(Application.streamingAssetsPath, "Content", path);

            if (File.Exists(fullPath))
            {
                return File.ReadAllText(fullPath);
            }

            string resourcePath = Path.Combine("Content", path).Replace('\\', '/');
            TextAsset resource = Resources.Load<TextAsset>(resourcePath);
            if (resource != null)
            {
                return resource.text;
            }

            Debug.LogError($"[UnityScriptLoader] Script file not found at: {fullPath} or Resources/{resourcePath}");
            throw new FileNotFoundException($"Script file not found at: {fullPath} or Resources/{resourcePath}");
        }

        public List<string> LoadSceneNames()
        {
            var sceneNames = new List<string>();
            string scenesDir = Path.Combine(Application.streamingAssetsPath, "Content", "scenes");

            if (Directory.Exists(scenesDir))
            {
                foreach (string file in Directory.GetFiles(scenesDir, "*.scm"))
                {
                    sceneNames.Add(Path.GetFileNameWithoutExtension(file));
                }
            }

            if (sceneNames.Count == 0)
            {
                foreach (TextAsset sceneAsset in Resources.LoadAll<TextAsset>("Content/scenes"))
                {
                    string sceneName = Path.GetFileNameWithoutExtension(sceneAsset.name);
                    if (!sceneNames.Contains(sceneName))
                    {
                        sceneNames.Add(sceneName);
                    }
                }
            }

            sceneNames.Sort();
            return sceneNames;
        }
    }
}
