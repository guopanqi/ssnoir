#nullable enable
using UnityEngine;

namespace SSNoir.IMGUI
{
    public static class IMGUIStyles
    {
        public static Font? ChineseFont;

        // Blueprint Noir Palette
        public static readonly Color PrimaryColor = new Color(0.671f, 0.780f, 1.0f, 1f);      // Steel Blue
        public static readonly Color OnPrimaryColor = new Color(0.0f, 0.184f, 0.40f, 1f);
        public static readonly Color SecondaryColor = new Color(0.694f, 0.780f, 0.961f, 1f);   // Muted Slate
        public static readonly Color OnSecondaryColor = new Color(0.098f, 0.188f, 0.337f, 1f);
        public static readonly Color TertiaryColor = new Color(1.0f, 0.714f, 0.576f, 1f);      // Burnt Amber
        public static readonly Color OnTertiaryColor = new Color(0.337f, 0.122f, 0.0f, 1f);
        public static readonly Color ErrorColor = new Color(1.0f, 0.706f, 0.671f, 1f);
        public static readonly Color OnErrorColor = new Color(0.412f, 0.0f, 0.020f, 1f);
        public static readonly Color OutlineColor = new Color(0.549f, 0.565f, 0.620f, 1f);
        public static readonly Color OutlineVariantColor = new Color(0.259f, 0.278f, 0.325f, 1f);
        public static readonly Color SurfaceColor = new Color(0.067f, 0.075f, 0.102f, 1f);
        public static readonly Color OnSurface = new Color(0.882f, 0.886f, 0.922f, 1f);
        public static readonly Color OnSurfaceVariant = new Color(0.761f, 0.776f, 0.835f, 1f);
 
        // Modern Noir Palette mapped to Blueprint Noir
        public static readonly Color CardBg = new Color(0.114f, 0.125f, 0.149f, 0.90f); // SurfaceContainer
        public static readonly Color CardHoverBg = new Color(0.153f, 0.165f, 0.192f, 0.95f); // SurfaceContainerHigh
        public static readonly Color CardOutline = new Color(0.549f, 0.565f, 0.620f, 0.8f); // Outline
        public static readonly Color CardHoverOutline = new Color(0.671f, 0.780f, 1.0f, 1.0f); // Primary (Steel Blue)
        public static readonly Color TitleColor = new Color(0.882f, 0.886f, 0.922f, 1f); // OnSurface
        public static readonly Color SubtitleColor = new Color(0.761f, 0.776f, 0.835f, 1f); // OnSurfaceVariant
        public static readonly Color SlotEmpty = new Color(0.043f, 0.055f, 0.078f, 0.8f); // SurfaceContainerLowest
        public static readonly Color SlotEmptyBorder = new Color(0.259f, 0.278f, 0.325f, 0.8f); // OutlineVariant
        public static readonly Color SlotFilled = new Color(0.671f, 0.780f, 1.0f, 0.2f); // Primary with 20% alpha
        public static readonly Color SlotFilledBorder = new Color(0.671f, 0.780f, 1.0f, 1f); // Primary
        public static readonly Color ExecuteBtn = Color.clear;
        public static readonly Color ExecuteBtnHover = new Color(0.671f, 0.780f, 1.0f, 0.10f); // 10% Primary tint
        public static readonly Color DisabledBtn = Color.clear;
        public static readonly Color PanelBg = new Color(0.067f, 0.075f, 0.102f, 0.40f); // 40% Surface
        public static readonly Color BottomBarBg = new Color(0.043f, 0.055f, 0.078f, 0.40f); // 40% Lowest
        public static readonly Color ToastBg = new Color(1.0f, 0.714f, 0.576f, 0.20f); // 20% Tertiary
        public static readonly Color ModalBg = new Color(0.067f, 0.075f, 0.102f, 0.85f); // 85% Surface
        public static readonly Color Blocker = new Color(0.043f, 0.055f, 0.078f, 0.60f); // 60% Lowest
        public static readonly Color HealthColor = new Color(1.0f, 0.706f, 0.671f, 1f); // Error
        public static readonly Color MoneyColor = new Color(0.694f, 0.780f, 0.961f, 1f); // Secondary (Muted Slate)
        public static readonly Color DieNormal = new Color(0.098f, 0.110f, 0.133f, 0.60f); // SurfaceContainerLow
        public static readonly Color DieHover = new Color(0.671f, 0.780f, 1.0f, 0.10f); // 10% Primary
        public static readonly Color DieSelected = new Color(0.671f, 0.780f, 1.0f, 0.20f); // 20% Primary
        public static readonly Color ItemNormal = new Color(0.098f, 0.110f, 0.133f, 0.60f);
        public static readonly Color ItemHover = new Color(0.694f, 0.780f, 0.961f, 0.10f); // 10% Secondary
        public static readonly Color ClockActive = new Color(1.0f, 0.714f, 0.576f, 1f); // Tertiary
        public static readonly Color ClockInactive = new Color(0.098f, 0.110f, 0.133f, 0.80f); // SurfaceContainerLow
        public static readonly Color ProgressTrack = new Color(0.098f, 0.110f, 0.133f, 1f);
        public static readonly Color ProgressFill = new Color(0.671f, 0.780f, 1.0f, 1f); // Primary
        public static readonly Color OutcomeSuccess = new Color(0.671f, 0.780f, 1.0f, 1f); // Primary
        public static readonly Color OutcomeNeutral = new Color(0.694f, 0.780f, 0.961f, 1f); // Secondary
        public static readonly Color OutcomeFail = new Color(1.0f, 0.706f, 0.671f, 1f); // Error
        public static readonly Color FlippedBg = new Color(0.114f, 0.125f, 0.149f, 0.90f); // SurfaceContainer
        public static readonly Color FlippedOutline = new Color(0.694f, 0.780f, 0.961f, 1f); // Secondary
        public static readonly Color DropdownBg = new Color(0.098f, 0.110f, 0.133f, 0.60f); // SurfaceContainerLow
        public static readonly Color DropdownHover = new Color(0.671f, 0.780f, 1.0f, 0.10f); // 10% Primary

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

        public static Material? PieMaterial;

        private static bool _initialized;

        public static void Init(Font? font)
        {
            if (_initialized) return;
            _initialized = true;
            ChineseFont = font;

            // Initialize Pie drawing material
            if (PieMaterial == null)
            {
                PieMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
                PieMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            CardTitle = MakeStyle(20, TitleColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            CardSubtitle = MakeStyle(14, SubtitleColor, TextAnchor.MiddleCenter, FontStyle.Normal);
            CardTypeTag = MakeStyle(14, SubtitleColor, TextAnchor.MiddleCenter, FontStyle.Normal);
            SlotLabel = MakeStyle(14, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            ExecuteLabel = MakeStyle(14, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            StatusLabel = MakeStyle(14, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            SectionLabel = MakeStyle(14, SubtitleColor, TextAnchor.MiddleLeft, FontStyle.Normal);
            ToastLabel = MakeStyle(14, OnTertiaryColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            ModalTitle = MakeStyle(22, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            ModalBody = MakeStyle(14, OnSurface, TextAnchor.MiddleLeft, FontStyle.Normal);
            FlippedTitle = MakeStyle(18, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            FlippedContent = MakeStyle(14, OnSurface, TextAnchor.UpperCenter, FontStyle.Normal);
            FlippedTip = MakeStyle(12, SecondaryColor, TextAnchor.MiddleCenter, FontStyle.Italic);
            ClockLabel = MakeStyle(12, OnSurface, TextAnchor.MiddleLeft, FontStyle.Bold);
            ClockValue = MakeStyle(12, ClockActive, TextAnchor.MiddleRight, FontStyle.Bold);
            DropdownItem = MakeStyle(14, OnSurfaceVariant, TextAnchor.MiddleLeft, FontStyle.Normal);
            DropdownCurrent = MakeStyle(14, Color.white, TextAnchor.MiddleLeft, FontStyle.Normal);
            CursorFollower = MakeStyle(12, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            SceneLabel = MakeStyle(14, OnSurfaceVariant, TextAnchor.MiddleLeft, FontStyle.Normal);
            HelpTip = MakeStyle(12, OnSurfaceVariant, TextAnchor.MiddleLeft, FontStyle.Normal);
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

        public static void DrawLine(Vector2 start, Vector2 end, Color color, float thickness = 1f)
        {
            var oldColor = GUI.color;
            GUI.color = color;
            if (Mathf.Approximately(start.x, end.x))
            {
                float y = Mathf.Min(start.y, end.y);
                float h = Mathf.Abs(start.y - end.y);
                GUI.DrawTexture(new Rect(start.x - thickness / 2f, y, thickness, h), Texture2D.whiteTexture);
            }
            else if (Mathf.Approximately(start.y, end.y))
            {
                float x = Mathf.Min(start.x, end.x);
                float w = Mathf.Abs(start.x - end.x);
                GUI.DrawTexture(new Rect(x, start.y - thickness / 2f, w, thickness), Texture2D.whiteTexture);
            }
            else
            {
                var matrix = GUI.matrix;
                Vector2 d = end - start;
                float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                float dist = d.magnitude;
                GUI.matrix = Matrix4x4.TRS(start, Quaternion.Euler(0, 0, angle), Vector3.one) * matrix;
                GUI.DrawTexture(new Rect(0, -thickness / 2f, dist, thickness), Texture2D.whiteTexture);
                GUI.matrix = matrix;
            }
            GUI.color = oldColor;
        }

        public static void DrawOutline(Rect rect, float thickness, Color color)
        {
            var oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        public static void DrawScanLine(Rect rect, Color color, float speed = 120f, float thickness = 1f)
        {
            if (rect.height <= 0) return;
            float scanY = rect.y + ((Time.time * speed) % rect.height);
            var oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, scanY, rect.width, thickness), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        public static bool DrawTechnicalButton(Rect rect, string text, Vector2 mousePos, Color outlineColor, Color hoverBgColor, GUIStyle style, bool enabled = true)
        {
            bool isHovered = enabled && rect.Contains(mousePos);
            
            // Draw background
            if (enabled && isHovered)
            {
                var oldBg = GUI.color;
                GUI.color = hoverBgColor;
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = oldBg;
            }
            
            // Draw outline
            Color border = enabled ? outlineColor : OutlineVariantColor;
            DrawOutline(rect, 1f, border);
            
            // Draw text
            var oldTextColor = style.normal.textColor;
            var oldHoverColor = style.hover.textColor;
            var oldActiveColor = style.active.textColor;
            
            Color txtColor = enabled ? (isHovered ? Color.white : outlineColor) : new Color(0.549f, 0.565f, 0.620f, 0.5f);
            style.normal.textColor = txtColor;
            style.hover.textColor = txtColor;
            style.active.textColor = txtColor;
            
            GUI.Label(rect, text.ToUpper(), style);
            
            style.normal.textColor = oldTextColor;
            style.hover.textColor = oldHoverColor;
            style.active.textColor = oldActiveColor;
            
            return enabled && isHovered && Event.current.type == EventType.MouseDown && Event.current.button == 0;
        }
    }
}

