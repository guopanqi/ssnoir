using System.IO;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir
{
    public class UnityScriptLoader : IScriptLoader
    {
        public string LoadScriptText(string path)
        {
            string fullPath = Path.Combine(Application.streamingAssetsPath, "Content", path);

            // On Windows, Mac, Linux and in the Unity Editor, StreamingAssets can be read via standard File IO
            if (File.Exists(fullPath))
            {
                return File.ReadAllText(fullPath);
            }

            // Fallback error logging for platforms that don't support direct file reading (like WebGL or Android APKs)
            Debug.LogError($"[UnityScriptLoader] Script file not found at: {fullPath}");
            throw new FileNotFoundException($"Script file not found at: {fullPath}");
        }
    }
}
