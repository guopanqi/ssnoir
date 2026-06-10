#nullable enable
using UnityEngine;
using TMPro;

namespace SSNoir
{
    public static class FontManager
    {
        private static TMP_FontAsset? _chineseFont;

        public static TMP_FontAsset? GetChineseFont()
        {
            if (_chineseFont != null) return _chineseFont;

            _chineseFont = Resources.Load<TMP_FontAsset>("Fonts/ArialUnicode SDF");

            if (_chineseFont == null)
            {
                Debug.LogWarning("[FontManager] TMP Font Asset not found at Resources/Fonts/ArialUnicode SDF. "
                    + "Run SSNoir > Setup Fonts in the Editor menu, or assign the font manually. "
                    + "Falling back to default TMP font.");
            }

            return _chineseFont;
        }
    }
}
