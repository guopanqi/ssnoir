using System.IO;
using UnityEditor;
using UnityEngine;

namespace SSNoir.Editor
{
    [InitializeOnLoad]
    public static class ContentSyncEditor
    {
        static ContentSyncEditor()
        {
            // Trigger copy when Editor loads or compiles
            SyncContent();
            
            // Trigger copy when entering Play Mode
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                SyncContent();
            }
        }

        [MenuItem("SSNoir/Rebuild Schemy DLL")]
        public static void RebuildSchemyDll()
        {
            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string csproj = Path.Combine(repoRoot, "schemy-master", "src", "schemy", "schemy.csproj");
            string dllSrc = Path.Combine(repoRoot, "schemy-master", "src", "schemy", "bin", "Release", "netstandard2.0", "schemy.dll");
            string dllDest = Path.Combine(repoRoot, "Engine", "Plugins", "schemy.dll");

            if (!File.Exists(csproj))
            {
                Debug.LogError($"[SchemyBuild] schemy.csproj not found at: {csproj}");
                return;
            }

            Debug.Log("[SchemyBuild] Building schemy (netstandard2.0, Release)...");

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{csproj}\" -c Release --nologo -v q",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using (var proc = System.Diagnostics.Process.Start(psi))
            {
                string stdout = proc.StandardOutput.ReadToEnd();
                string stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                if (proc.ExitCode != 0)
                {
                    Debug.LogError($"[SchemyBuild] Build failed:\n{stderr}\n{stdout}");
                    return;
                }
            }

            if (!File.Exists(dllSrc))
            {
                Debug.LogError($"[SchemyBuild] Built DLL not found at: {dllSrc}");
                return;
            }

            File.Copy(dllSrc, dllDest, overwrite: true);
            Debug.Log($"[SchemyBuild] Copied schemy.dll to Engine/Plugins. Refreshing assets...");
            AssetDatabase.Refresh();
        }

        [MenuItem("SSNoir/Sync Content Now")]
        public static void SyncContent()
        {
            // Source: [ProjectRoot]/Content
            // ProjectRoot is parent of Assets folder (UnityClient)
            // So Source is UnityClient/../Content -> [Root]/Content
            string sourcePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Content"));
            string destPath = Path.GetFullPath(Path.Combine(Application.dataPath, "StreamingAssets", "Content"));
            string resourcesContentPath = Path.GetFullPath(Path.Combine(Application.dataPath, "Resources", "Content"));
            string resourcesFontPath = Path.GetFullPath(Path.Combine(Application.dataPath, "Resources", "Fonts"));

            if (!Directory.Exists(sourcePath))
            {
                Debug.LogWarning($"[ContentSync] Source Content directory not found at: {sourcePath}");
                return;
            }

            try
            {
                // 1. Sync all content to StreamingAssets (runtime access)
                CopyDirectory(sourcePath, destPath);
                Debug.Log($"[ContentSync] Successfully synchronized Content from {sourcePath} to {destPath}");

                // 2. Sync Scheme content to Resources as TextAssets for WebGL.
                CopySchemeTextAssets(sourcePath, resourcesContentPath);
                Debug.Log($"[ContentSync] Scheme content synchronized to Resources/Content for WebGL.");

                // 3. Sync fonts to Resources/Fonts. This gives both Editor code and runtime
                //    builds one stable load path: Resources.Load<Font>("Fonts/...").
                string fontSourcePath = Path.Combine(sourcePath, "assets", "fonts");
                if (Directory.Exists(fontSourcePath))
                {
                    CopyDirectory(fontSourcePath, resourcesFontPath, clean: false);
                    Debug.Log($"[ContentSync] Fonts synchronized to Resources/Fonts.");
                }
                
                // Refresh asset database so Unity notices the files
                AssetDatabase.Refresh();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ContentSync] Failed to sync content: {ex.Message}");
            }
        }

        private static void CopyDirectory(string sourceDir, string destDir, bool clean = true)
        {
            if (clean)
            {
                // Clean destination first to remove deleted files
                if (Directory.Exists(destDir))
                {
                    Directory.Delete(destDir, true);
                }
                Directory.CreateDirectory(destDir);
            }
            else if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            foreach (string file in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
            {
                // Skip meta files from source if any
                if (file.EndsWith(".meta")) continue;

                string relativePath = file.Substring(sourceDir.Length + 1);
                string destFile = Path.Combine(destDir, relativePath);

                string destFileDir = Path.GetDirectoryName(destFile);
                if (!Directory.Exists(destFileDir))
                {
                    Directory.CreateDirectory(destFileDir);
                }

                File.Copy(file, destFile, true);
            }
        }

        private static void CopySchemeTextAssets(string sourceDir, string destDir)
        {
            if (Directory.Exists(destDir))
            {
                Directory.Delete(destDir, true);
            }
            Directory.CreateDirectory(destDir);

            foreach (string file in Directory.GetFiles(sourceDir, "*.scm", SearchOption.AllDirectories))
            {
                string relativePath = file.Substring(sourceDir.Length + 1);
                string destFile = Path.Combine(destDir, relativePath + ".txt");
                string destFileDir = Path.GetDirectoryName(destFile);
                if (!Directory.Exists(destFileDir))
                {
                    Directory.CreateDirectory(destFileDir);
                }

                File.Copy(file, destFile, true);
            }
        }
    }
}
