using UnityEditor;
using UnityEngine;
using SSNoir.IMGUI;

namespace SSNoir.Editor
{
    public sealed class CharacterPortraitWindow : EditorWindow
    {
        private SerializedObject _serialized;
        private CharacterPortraitSettings _settings;
        [MenuItem("SSNoir/City/人物视觉调节")]
        public static void Open() => GetWindow<CharacterPortraitWindow>("人物视觉调节");
        private void OnEnable()
        {
            _settings = AssetDatabase.LoadAssetAtPath<CharacterPortraitSettings>("Assets/Resources/Portraits/CharacterVisuals.asset");
            _serialized = new SerializedObject(_settings);
        }
        private void OnGUI()
        {
            _serialized.Update();
            EditorGUILayout.HelpBox("共享参数实时作用于人物舞台。正冷暖值偏暖，负值偏冷。", MessageType.Info);
            Field("Brightness", "亮度"); Field("Contrast", "对比度"); Field("Saturation", "饱和度");
            Field("Temperature", "冷暖"); Field("BottomFade", "底部淡出");
            if (_serialized.ApplyModifiedProperties()) UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            if (GUILayout.Button("恢复原图")) { Undo.RecordObject(_settings, "恢复人物原色"); _settings.ResetLook(); EditorUtility.SetDirty(_settings); UnityEditorInternal.InternalEditorUtility.RepaintAllViews(); }
            if (GUILayout.Button("保存参数")) AssetDatabase.SaveAssets();
            var source = Resources.Load<Texture2D>("Portraits/Characters/夜莺");
            if (source != null)
            {
                var rect = GUILayoutUtility.GetAspectRect((float)source.width / source.height, GUILayout.MaxHeight(500));
                EditorGUI.DrawTextureTransparent(rect, CharacterPortraitVisuals.Adjust(source), ScaleMode.ScaleToFit);
            }
        }
        private void Field(string name, string label) => EditorGUILayout.PropertyField(_serialized.FindProperty(name), new GUIContent(label));
    }
}
