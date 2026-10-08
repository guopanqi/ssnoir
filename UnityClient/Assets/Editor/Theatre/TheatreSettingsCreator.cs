#nullable enable
using SSNoir.UnityTheatre;
using UnityEditor;
using UnityEngine;

namespace SSNoir.EditorTools
{
    // Creates the tuning asset once; afterwards edit it in the Inspector during rehearsal.
    public static class TheatreSettingsCreator
    {
        private const string Path = "Assets/Resources/Theatre/LineTheatreSettings.asset";
        [MenuItem("SSNoir/剧场/创建舞台参数资产")]
        private static void Create()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TheatreSettings>(Path);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }
            var settings = ScriptableObject.CreateInstance<TheatreSettings>();
            AssetDatabase.CreateAsset(settings, Path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }
    }
}
