using System.IO;
using UnityEngine;
using UnityEditor;
using TMPro;

namespace SSNoir.Editor
{
    public static class SSNoirFontSetup
    {
        private const string SourceTtfRelative = "../../Content/assets/fonts/ArialUnicode.ttf";

        [MenuItem("SSNoir/Setup Fonts")]
        public static void SetupFonts()
        {
            // 1. Locate source TTF
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var sourcePath = Path.GetFullPath(Path.Combine(Application.dataPath, SourceTtfRelative));

            if (!File.Exists(sourcePath))
            {
                Debug.LogError($"[FontSetup] Source font not found at: {sourcePath}");
                return;
            }

            // 2. Ensure target directory
            var targetDir = Path.Combine(Application.dataPath, "Fonts");
            Directory.CreateDirectory(targetDir);

            // 3. Copy TTF to Assets/Fonts/
            var ttfFileName = "ArialUnicode.ttf";
            var targetTtfPath = Path.Combine(targetDir, ttfFileName);
            File.Copy(sourcePath, targetTtfPath, true);

            AssetDatabase.Refresh();

            // 4. Import the TTF as a Unity Font asset
            var relativeAssetPath = "Assets/Fonts/ArialUnicode.ttf";
            var fontImporter = AssetImporter.GetAtPath(relativeAssetPath) as TrueTypeFontImporter;
            if (fontImporter != null)
            {
                fontImporter.fontNames = new[] { "Arial Unicode MS", "ArialUnicode" };
                fontImporter.fontRenderingMode = FontRenderingMode.Smooth;
                fontImporter.SaveAndReimport();
            }

            AssetDatabase.Refresh();

            // 5. Load the imported Font and create TMP_FontAsset
            var unityFont = AssetDatabase.LoadAssetAtPath<Font>(relativeAssetPath);
            if (unityFont == null)
            {
                Debug.LogError("[FontSetup] Failed to import ArialUnicode.ttf as Unity Font asset.");
                return;
            }

            // Remove existing generated SDF asset to avoid conflicts
            var sdfAssetPath = "Assets/Fonts/ArialUnicode SDF.asset";
            if (File.Exists(Path.GetFullPath(Path.Combine(Application.dataPath, "..", sdfAssetPath))))
            {
                AssetDatabase.DeleteAsset(sdfAssetPath);
                AssetDatabase.Refresh();
            }

            var fontAsset = TMP_FontAsset.CreateFontAsset(unityFont);
            if (fontAsset == null)
            {
                Debug.LogError("[FontSetup] Failed to create TMP_FontAsset from ArialUnicode.ttf");
                return;
            }

            fontAsset.name = "ArialUnicode SDF";
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;

            AssetDatabase.CreateAsset(fontAsset, sdfAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[FontSetup] Success! TMP Font Asset created at: {sdfAssetPath}");
        }
    }
}
