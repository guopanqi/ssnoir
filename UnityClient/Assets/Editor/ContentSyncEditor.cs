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

        [MenuItem("SSNoir/Sync Content Now")]
        public static void SyncContent()
        {
            // Source: [ProjectRoot]/Content
            // ProjectRoot is parent of Assets folder (UnityClient)
            // So Source is UnityClient/../Content -> [Root]/Content
            string sourcePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Content"));
            string destPath = Path.GetFullPath(Path.Combine(Application.dataPath, "StreamingAssets", "Content"));
            string resourcesContentPath = Path.GetFullPath(Path.Combine(Application.dataPath, "Resources", "Content"));

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

                // 3. Sync fonts to Assets/Fonts so Unity Editor can import them as real Assets
                //    (Font Asset Creator requires a font to be in Assets/, not StreamingAssets)
                string fontSourcePath = Path.Combine(sourcePath, "assets", "fonts");
                string fontDestPath = Path.GetFullPath(Path.Combine(Application.dataPath, "Fonts"));
                if (Directory.Exists(fontSourcePath))
                {
                CopyDirectory(fontSourcePath, fontDestPath, clean: false);
                Debug.Log($"[ContentSync] Fonts synchronized to Assets/Fonts for Editor import.");
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
