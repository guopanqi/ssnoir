#nullable enable
using UnityEngine;

namespace SSNoir.IMGUI
{
    public static class IMGUIStyles
    {
        public static Font? ChineseFont;

        // Colors
        public static readonly Color CardBg = new Color(0.12f, 0.12f, 0.16f, 0.95f);
        public static readonly Color CardHoverBg = new Color(0.18f, 0.18f, 0.24f, 0.98f);
        public static readonly Color CardOutline = new Color(0.35f, 0.35f, 0.45f, 1f);
        public static readonly Color CardHoverOutline = new Color(0.55f, 0.55f, 0.75f, 1f);
        public static readonly Color TitleColor = Color.white;
        public static readonly Color SubtitleColor = new Color(0.7f, 0.7f, 0.8f, 1f);
        public static readonly Color SlotEmpty = new Color(0.2f, 0.2f, 0.25f, 1f);
        public static readonly Color SlotEmptyBorder = new Color(0.35f, 0.35f, 0.45f, 1f);
        public static readonly Color SlotFilled = new Color(0.15f, 0.45f, 0.25f, 1f);
        public static readonly Color SlotFilledBorder = new Color(0.3f, 0.7f, 0.4f, 1f);
        public static readonly Color ExecuteBtn = new Color(0.6f, 0.25f, 0.1f, 1f);
        public static readonly Color ExecuteBtnHover = new Color(0.8f, 0.35f, 0.15f, 1f);
        public static readonly Color DisabledBtn = new Color(0.25f, 0.25f, 0.28f, 1f);
        public static readonly Color PanelBg = new Color(0.06f, 0.06f, 0.08f, 0.95f);
        public static readonly Color BottomBarBg = new Color(0.04f, 0.04f, 0.06f, 1f);
        public static readonly Color ToastBg = new Color(0.7f, 0.15f, 0.15f, 0.95f);
        public static readonly Color ModalBg = new Color(0.08f, 0.08f, 0.12f, 0.98f);
        public static readonly Color Blocker = new Color(0, 0, 0, 0.6f);
        public static readonly Color HealthColor = new Color(0.9f, 0.3f, 0.3f, 1f);
        public static readonly Color MoneyColor = new Color(0.3f, 0.9f, 0.4f, 1f);
        public static readonly Color DieNormal = new Color(0.18f, 0.35f, 0.55f, 1f);
        public static readonly Color DieHover = new Color(0.35f, 0.55f, 0.8f, 1f);
        public static readonly Color DieSelected = new Color(1f, 0.75f, 0.2f, 1f);
        public static readonly Color ItemNormal = new Color(0.15f, 0.45f, 0.3f, 1f);
        public static readonly Color ItemHover = new Color(0.3f, 0.65f, 0.45f, 1f);
        public static readonly Color ClockActive = new Color(1f, 0.75f, 0.2f, 1f);
        public static readonly Color ClockInactive = new Color(0.25f, 0.25f, 0.3f, 1f);
        public static readonly Color ProgressTrack = new Color(0.12f, 0.12f, 0.16f, 1f);
        public static readonly Color ProgressFill = new Color(1f, 0.75f, 0.2f, 1f);
        public static readonly Color OutcomeSuccess = new Color(0.2f, 0.8f, 0.4f, 1f);
        public static readonly Color OutcomeNeutral = new Color(0.9f, 0.8f, 0.2f, 1f);
        public static readonly Color OutcomeFail = new Color(0.9f, 0.2f, 0.2f, 1f);
        public static readonly Color FlippedBg = new Color(0.06f, 0.16f, 0.12f, 0.95f);
        public static readonly Color FlippedOutline = new Color(0.2f, 0.5f, 0.35f, 1f);
        public static readonly Color DropdownBg = new Color(0.12f, 0.12f, 0.18f, 1f);
        public static readonly Color DropdownHover = new Color(0.2f, 0.2f, 0.3f, 1f);

        // Styles
        public static GUIStyle CardTitle = null!;
        public static GUIStyle CardSubtitle = null!;
        public static GUIStyle CardTypeTag = null!;
        public static GUIStyle SlotLabel = null!;
        public static GUIStyle ExecuteLabel = null!;
        public static GUIStyle StatusLabel = null!;
        public static GUIStyle SectionLabel = null!;
        public static GUIStyle ToastLabel = null!;
        public static GUIStyle ModalTitle = null!;
        public static GUIStyle ModalBody = null!;
        public static GUIStyle FlippedTitle = null!;
        public static GUIStyle FlippedContent = null!;
        public static GUIStyle FlippedTip = null!;
        public static GUIStyle ClockLabel = null!;
        public static GUIStyle ClockValue = null!;
        public static GUIStyle DropdownItem = null!;
        public static GUIStyle DropdownCurrent = null!;
        public static GUIStyle CursorFollower = null!;
        public static GUIStyle SceneLabel = null!;
        public static GUIStyle HelpTip = null!;

        private static bool _initialized;

        public static void Init(Font? font)
        {
            if (_initialized) return;
            _initialized = true;
            ChineseFont = font;

            CardTitle = MakeStyle(16, TitleColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            CardSubtitle = MakeStyle(11, SubtitleColor, TextAnchor.MiddleCenter, FontStyle.Italic);
            CardTypeTag = MakeStyle(12, SubtitleColor, TextAnchor.MiddleCenter, FontStyle.Normal);
            SlotLabel = MakeStyle(11, Color.white, TextAnchor.MiddleCenter, FontStyle.Normal);
            ExecuteLabel = MakeStyle(12, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            StatusLabel = MakeStyle(14, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            SectionLabel = MakeStyle(11, new Color(0.7f, 0.7f, 0.7f), TextAnchor.MiddleLeft, FontStyle.Normal);
            ToastLabel = MakeStyle(13, Color.white, TextAnchor.MiddleCenter, FontStyle.Normal);
            ModalTitle = MakeStyle(15, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            ModalBody = MakeStyle(12, new Color(0.85f, 0.85f, 0.85f), TextAnchor.MiddleLeft, FontStyle.Normal);
            FlippedTitle = MakeStyle(13, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            FlippedContent = MakeStyle(11, new Color(0.9f, 0.9f, 0.9f), TextAnchor.UpperCenter, FontStyle.Normal);
            FlippedTip = MakeStyle(10, new Color(1f, 0.8f, 0.2f), TextAnchor.MiddleCenter, FontStyle.Italic);
            ClockLabel = MakeStyle(10, new Color(0.9f, 0.9f, 0.9f), TextAnchor.MiddleLeft, FontStyle.Bold);
            ClockValue = MakeStyle(10, new Color(1f, 0.8f, 0.2f), TextAnchor.MiddleRight, FontStyle.Bold);
            DropdownItem = MakeStyle(13, new Color(0.8f, 0.8f, 0.9f), TextAnchor.MiddleLeft, FontStyle.Normal);
            DropdownCurrent = MakeStyle(13, Color.white, TextAnchor.MiddleLeft, FontStyle.Normal);
            CursorFollower = MakeStyle(11, Color.black, TextAnchor.MiddleCenter, FontStyle.Normal);
            SceneLabel = MakeStyle(13, new Color(0.8f, 0.8f, 0.9f), TextAnchor.MiddleLeft, FontStyle.Normal);
            HelpTip = MakeStyle(11, new Color(0.5f, 0.5f, 0.6f), TextAnchor.MiddleLeft, FontStyle.Normal);
        }

        private static GUIStyle MakeStyle(int fontSize, Color textColor, TextAnchor alignment, FontStyle fontStyle)
        {
            var style = new GUIStyle();
            style.font = ChineseFont;
            style.fontSize = fontSize;
            style.normal.textColor = textColor;
            style.alignment = alignment;
            style.fontStyle = fontStyle;
            style.wordWrap = true;
            return style;
        }

        public static GUIStyle BoxStyle(Color bg, Color border, int borderWidth = 1)
        {
            var style = new GUIStyle(GUI.skin.box);
            style.normal.background = MakeTexture(2, 2, bg);
            style.border = new RectOffset(borderWidth, borderWidth, borderWidth, borderWidth);
            style.normal.background = MakeTexture(2, 2, bg);
            return style;
        }

        public static GUIStyle ButtonStyle(Color bg, Color hover, Color border, int borderWidth = 1)
        {
            var style = new GUIStyle(GUI.skin.button);
            style.normal.background = MakeTexture(2, 2, bg);
            style.hover.background = MakeTexture(2, 2, hover);
            style.active.background = MakeTexture(2, 2, new Color(bg.r * 0.8f, bg.g * 0.8f, bg.b * 0.8f, bg.a));
            style.border = new RectOffset(borderWidth, borderWidth, borderWidth, borderWidth);
            style.alignment = TextAnchor.MiddleCenter;
            style.font = ChineseFont;
            style.fontSize = 12;
            style.normal.textColor = Color.white;
            style.hover.textColor = Color.white;
            style.active.textColor = new Color(0.9f, 0.9f, 0.9f);
            style.margin = new RectOffset(0, 0, 0, 0);
            style.padding = new RectOffset(4, 4, 2, 2);
            return style;
        }

        public static Texture2D MakeTexture(int width, int height, Color color)
        {
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            var texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        public static GUIStyle GetCardStyle(Color bg, Color border, int borderWidth = 1)
        {
            var style = new GUIStyle(GUI.skin.box);
            style.normal.background = MakeTexture(2, 2, bg);
            style.border = new RectOffset(borderWidth, borderWidth, borderWidth, borderWidth);
            style.normal.background = MakeTexture(2, 2, bg);
            style.margin = new RectOffset(0, 0, 0, 0);
            style.padding = new RectOffset(0, 0, 0, 0);
            return style;
        }
    }
}
