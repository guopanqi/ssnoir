#nullable enable
using UnityEngine;

namespace SSNoir.IMGUI
{
    public static class IMGUIStyles
    {
        public static Font? ChineseFont;
        public static Font? SemiboldFont;

        // ══════════════════════════════════════════════════════════════════
        // 「墨与纸」Ink & Paper Noir 调色板（DESIGN.md 定稿，2026-07）
        // 取代旧 Blueprint Noir（Steel Blue / Muted Slate / Burnt Amber）体系。
        // ══════════════════════════════════════════════════════════════════

        // ── 基底 ──
        public static readonly Color Ink        = new Color(0.051f, 0.071f, 0.125f, 0.96f); // #0D1220 @96% 暗版节点填充
        public static readonly Color PhotoBlack = new Color(0.047f, 0.047f, 0.055f, 1f);     // #0C0C0E 照片块专用黑
        public static readonly Color Paper      = new Color(0.937f, 0.918f, 0.878f, 1f);     // #EFEAE0 纸白
        public static readonly Color PaperInk   = new Color(0.110f, 0.102f, 0.082f, 1f);     // #1C1A15 纸上墨字
        public static readonly Color HudBg      = new Color(0.039f, 0.047f, 0.071f, 0.92f);  // rgba(10,12,18,.92)

        // ── 文字（暗版/HUD 上）──
        public static readonly Color TextPrimary   = Paper;                                   // 主文字
        public static readonly Color TextSecondary = new Color(Paper.r, Paper.g, Paper.b, 0.60f); // 次级
        public static readonly Color TextDisabled  = new Color(Paper.r, Paper.g, Paper.b, 0.35f); // 弱化/禁用

        // ── 文字（纸上）──
        public static readonly Color PaperTextPrimary   = PaperInk;
        public static readonly Color PaperTextSecondary = new Color(0.290f, 0.271f, 0.235f, 1f); // #4A453C
        public static readonly Color PaperTextDisabled  = new Color(0.541f, 0.514f, 0.459f, 1f); // #8A8375

        // ── 强调色：做旧金（全局唯一主强调色）──
        public static readonly Color Gold       = new Color(0.910f, 0.765f, 0.353f, 1f); // #E8C35A
        public static readonly Color GoldOnDark  = new Color(0.165f, 0.129f, 0.027f, 1f); // #2A2107 金底上的深字

        // ── 印章红（高危 / 失败，专用不做按钮）──
        public static readonly Color SealRed = new Color(0.702f, 0.251f, 0.165f, 1f); // #B3402A

        // ── 便签色板（贴在物件上的彩色便签：纸底 + 同族深字）──
        public static readonly Color StickyWorkBg     = new Color(0.847f, 0.894f, 0.769f, 1f); // #D8E4C4
        public static readonly Color StickyWorkText   = new Color(0.239f, 0.322f, 0.149f, 1f); // #3D5226
        public static readonly Color StickyMidRiskBg   = new Color(0.945f, 0.875f, 0.643f, 1f); // #F1DFA4
        public static readonly Color StickyMidRiskText = new Color(0.427f, 0.325f, 0.063f, 1f); // #6D5310
        public static readonly Color StickyHighRiskBg   = new Color(0.937f, 0.788f, 0.722f, 1f); // #EFC9B8
        public static readonly Color StickyHighRiskText = new Color(0.478f, 0.188f, 0.094f, 1f); // #7A3018
        public static readonly Color StickyNegotiateBg   = new Color(0.788f, 0.847f, 0.910f, 1f); // #C9D8E8
        public static readonly Color StickyNegotiateText = new Color(0.173f, 0.275f, 0.400f, 1f); // #2C4666
        public static readonly Color StickyOpportunityBg   = new Color(0.890f, 0.816f, 0.894f, 1f); // #E3D0E4
        public static readonly Color StickyOpportunityText = new Color(0.361f, 0.196f, 0.376f, 1f); // #5C3260

        // ── 概率/结果三色（沉着版）──
        public static readonly Color OddsFail    = new Color(0.820f, 0.416f, 0.306f, 1f); // #D16A4E 陶红
        public static readonly Color OddsNeutral = new Color(0.831f, 0.694f, 0.345f, 1f); // #D4B158 赭黄
        public static readonly Color OddsSuccess = new Color(0.576f, 0.690f, 0.416f, 1f); // #93B06A 苔绿

        // ── 面板不透明度阶梯 ──
        public const float PanelOpacity  = 0.88f; // 面板 ≥88%
        public const float ModalOpacity  = 0.96f; // 弹窗 ≥96%
        public const float MaskOpacity   = 0.60f; // 遮罩 60%

        // ── 组合令牌（由基础令牌推导；旧 Blueprint Noir 调色板已全部退役）──
        // 弹窗是"递到面前的一张纸"：Paper 底 @96%（配 ModalTitle/ModalBody 的 PaperInk 字）
        public static readonly Color ModalBg = new Color(Paper.r, Paper.g, Paper.b, ModalOpacity);
        // 全屏遮罩：深墨 @60%
        public static readonly Color Blocker = new Color(0.039f, 0.047f, 0.071f, MaskOpacity);
        // 判定结果三色（与概率条同源）
        public static readonly Color OutcomeSuccess = OddsSuccess;
        public static readonly Color OutcomeNeutral = OddsNeutral;
        public static readonly Color OutcomeFail    = OddsFail;

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

        private static bool  _initialized;
        private static float _lastScale = -1f;
        private static bool _missingSemiboldFontLogged;

        public static void Init(Font? font, Font? semiboldFont = null)
        {
            float s = UIScale.Scale;
            if (_initialized
                && Mathf.Approximately(s, _lastScale)
                && ChineseFont == font
                && SemiboldFont == semiboldFont) return;
            _initialized = true;
            _lastScale = s;
            ChineseFont = font;
            SemiboldFont = semiboldFont;

            // PieMaterial is only created once (GL material, scale-independent).
            if (PieMaterial == null)
            {
                PieMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
                PieMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            // Font sizes are snapped so (fontSize × scale) lands on an integer
            // physical pixel, minimising sub-pixel blur at non-1.0 scales.
            CardTitle      = MakeStyle(SF(23,s), TextPrimary,        TextAnchor.MiddleCenter, FontStyle.Bold);
            CardSubtitle   = MakeStyle(SF(16,s), TextSecondary,      TextAnchor.MiddleCenter, FontStyle.Normal);
            CardTypeTag    = MakeStyle(SF(15,s), TextSecondary,      TextAnchor.MiddleCenter, FontStyle.Normal);
            SlotLabel      = MakeStyle(SF(16,s), PaperInk,           TextAnchor.MiddleCenter, FontStyle.Bold);
            ExecuteLabel   = MakeStyle(SF(16,s), GoldOnDark,         TextAnchor.MiddleCenter, FontStyle.Bold);
            StatusLabel    = MakeStyle(SF(14,s), TextPrimary,        TextAnchor.MiddleLeft,   FontStyle.Bold);
            SectionLabel   = MakeStyle(SF(16,s), TextSecondary,      TextAnchor.MiddleLeft,   FontStyle.Normal);
            ToastLabel     = MakeStyle(SF(14,s), GoldOnDark,         TextAnchor.MiddleCenter, FontStyle.Bold);
            ModalTitle     = MakeStyle(SF(22,s), PaperInk,           TextAnchor.MiddleCenter, FontStyle.Bold);
            ModalBody      = MakeStyle(SF(14,s), PaperTextPrimary,   TextAnchor.MiddleLeft,   FontStyle.Normal);
            FlippedTitle   = MakeStyle(SF(18,s), TextPrimary,        TextAnchor.MiddleCenter, FontStyle.Bold);
            FlippedContent = MakeStyle(SF(14,s), TextPrimary,        TextAnchor.UpperCenter,  FontStyle.Normal);
            FlippedTip     = MakeStyle(SF(12,s), TextSecondary,      TextAnchor.MiddleCenter, FontStyle.Italic);
            ClockLabel     = MakeStyle(SF(12,s), TextPrimary,        TextAnchor.MiddleLeft,   FontStyle.Bold);
            ClockValue     = MakeStyle(SF(12,s), Gold,               TextAnchor.MiddleRight,  FontStyle.Bold);
            DropdownItem   = MakeStyle(SF(14,s), TextSecondary,      TextAnchor.MiddleLeft,   FontStyle.Normal);
            DropdownCurrent= MakeStyle(SF(14,s), TextPrimary,        TextAnchor.MiddleLeft,   FontStyle.Normal);
            CursorFollower = MakeStyle(SF(12,s), TextPrimary,        TextAnchor.MiddleCenter, FontStyle.Bold);
            SceneLabel     = MakeStyle(SF(14,s), TextSecondary,      TextAnchor.MiddleLeft,   FontStyle.Normal);
            HelpTip        = MakeStyle(SF(14,s), TextSecondary,      TextAnchor.MiddleLeft,   FontStyle.Normal);
        }

        // Snap a virtual fontSize so that (result × scale) is the nearest integer
        // physical pixel count, reducing sub-pixel blur at fractional scales.
        private static int SF(int baseSize, float scale)
        {
            int physPx = Mathf.RoundToInt(baseSize * scale);
            return Mathf.Max(8, Mathf.RoundToInt(physPx / scale));
        }

        private static GUIStyle MakeStyle(int fontSize, Color textColor, TextAnchor alignment, FontStyle fontStyle)
        {
            var style = new GUIStyle();
            style.font = ResolveFont(fontStyle);
            style.fontSize = fontSize;
            style.normal.textColor = textColor;
            style.alignment = alignment;
            style.fontStyle = ResolveFontStyle(fontStyle);
            style.wordWrap = true;
            return style;
        }

        public static void ApplyStrongFont(GUIStyle style)
        {
            if (SemiboldFont != null)
            {
                style.font = SemiboldFont;
                style.fontStyle = FontStyle.Normal;
            }
            else
            {
                LogMissingSemiboldFont();
                style.font = ChineseFont;
                style.fontStyle = FontStyle.Normal;
            }
        }

        private static Font? ResolveFont(FontStyle fontStyle)
        {
            if (fontStyle != FontStyle.Bold)
                return ChineseFont;

            if (SemiboldFont == null)
                LogMissingSemiboldFont();
            return SemiboldFont ?? ChineseFont;
        }

        private static FontStyle ResolveFontStyle(FontStyle fontStyle)
        {
            return fontStyle == FontStyle.Bold ? FontStyle.Normal : fontStyle;
        }

        private static void LogMissingSemiboldFont()
        {
            if (_missingSemiboldFontLogged)
                return;

            _missingSemiboldFontLogged = true;
            Debug.LogError("[SSNoir] MiSans-Semibold font is not assigned. Strong IMGUI text will use the regular font without Unity faux-bold.");
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
            style.fontSize = SF(12, UIScale.Scale);
            style.normal.textColor = Paper;
            style.hover.textColor = Paper;
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

        // ══════════════════════════════════════════════════════════════════
        // 「墨与纸」绘制技法（DESIGN.md「IMGUI 绘制技法」一节配方）
        // ══════════════════════════════════════════════════════════════════

        // 硬投影：偏移处先画纯黑矩形，再由调用方画本体。offset 建议 (4,4)–(5,6)，
        // alpha 建议 0.45–0.50（节点）或 0.50（纸物件）。
        public static void DrawShadow(Rect rect, Vector2 offset, float alpha = 0.48f)
        {
            var oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, alpha);
            GUI.DrawTexture(new Rect(rect.x + offset.x, rect.y + offset.y, rect.width, rect.height), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        // 双线：外框 1px + 内缩 3px 的 20% 细线。始终是"线"的语言，不是"框"。
        public static void DrawDoubleOutline(Rect rect, Color outerColor, float outerThickness = 1f)
        {
            DrawOutline(rect, outerThickness, outerColor);
            var inner = new Rect(rect.x + 3f, rect.y + 3f, rect.width - 6f, rect.height - 6f);
            if (inner.width > 0 && inner.height > 0)
            {
                Color innerLine = new Color(Paper.r, Paper.g, Paper.b, 0.20f);
                DrawOutline(inner, 1f, innerLine);
            }
        }

        // 便签：RotateAroundPivot 包住"1px 黑影 + 彩纸矩形 + 深色字"。rotationDeg 建议 ±1–2°。
        public static void DrawStickyNote(Rect rect, string text, Color paperColor, Color textColor, float rotationDeg, GUIStyle? baseStyle = null)
        {
            var pivot = rect.center;
            var oldMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(rotationDeg, pivot);

            // 1px 黑影
            var oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.35f);
            GUI.DrawTexture(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), Texture2D.whiteTexture);

            // 彩纸底
            GUI.color = paperColor;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = oldColor;

            // 深色字
            var style = new GUIStyle(baseStyle ?? GUI.skin.label)
            {
                font = ChineseFont,
                alignment = TextAnchor.MiddleCenter,
            };
            style.normal.textColor = textColor;
            GUI.Label(rect, text, style);

            GUI.matrix = oldMatrix;
        }

        // 金光呼吸：向外扩 2px/4px(/6px) 各画一圈递减 alpha 的金描边，alpha 随 sin 缓动。
        // 只用于"正在发生"的东西（结算中节点/待处理事件/掷骰瞬间）。
        public static void DrawGoldPulse(Rect rect, float baseAlpha = 0.8f, int rings = 3, float ringStep = 2.5f, float speed = 2.4f)
        {
            float breathe = 0.6f + 0.4f * Mathf.Sin(Time.time * speed);
            for (int i = 1; i <= rings; i++)
            {
                float expand = ringStep * i;
                float ringAlpha = baseAlpha * breathe * (1f - (float)(i - 1) / rings) * 0.7f;
                var ringRect = new Rect(rect.x - expand, rect.y - expand, rect.width + expand * 2f, rect.height + expand * 2f);
                DrawOutline(ringRect, 1f, new Color(Gold.r, Gold.g, Gold.b, ringAlpha));
            }
        }

        // 盖印：旋转 −10°~−15° 的圆框 + 大字，alpha 80%。成=金、败=印章红。
        public static void DrawStampSeal(Rect rect, string text, Color sealColor, float rotationDeg = -12f, float alpha = 0.8f)
        {
            var pivot = rect.center;
            var oldMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(rotationDeg, pivot);

            Color ring = new Color(sealColor.r, sealColor.g, sealColor.b, alpha);
            float radius = Mathf.Min(rect.width, rect.height) / 2f;
            var square = new Rect(pivot.x - radius, pivot.y - radius, radius * 2f, radius * 2f);
            DrawOutline(square, 3f, ring);
            var inner = new Rect(square.x + 6f, square.y + 6f, square.width - 12f, square.height - 12f);
            if (inner.width > 0 && inner.height > 0)
            {
                DrawOutline(inner, 1f, ring);
            }

            var style = new GUIStyle
            {
                font = SemiboldFont ?? ChineseFont,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Max(10, Mathf.RoundToInt(radius * 0.7f)),
            };
            if (SemiboldFont == null)
                LogMissingSemiboldFont();
            style.normal.textColor = ring;
            GUI.Label(square, text, style);

            GUI.matrix = oldMatrix;
        }

        // 分段条：离散数值（行动点等）用等宽小段 + 2px 间隙，不用连续填充。
        public static void DrawSegmentedBar(Rect rect, int current, int max, Color activeColor, Color inactiveColor, float gap = 2f)
        {
            if (max <= 0) return;
            float segW = (rect.width - gap * (max - 1)) / max;
            for (int i = 0; i < max; i++)
            {
                var segRect = new Rect(rect.x + i * (segW + gap), rect.y, segW, rect.height);
                var oldColor = GUI.color;
                GUI.color = i < current ? activeColor : inactiveColor;
                GUI.DrawTexture(segRect, Texture2D.whiteTexture);
                GUI.color = oldColor;
            }
        }

        public static bool DrawTechnicalButton(Rect rect, string text, bool isHovered, bool isClicked, Color outlineColor, Color hoverBgColor, GUIStyle style, bool enabled = true)
        {

            // Draw background
            if (enabled && isHovered)
            {
                var oldBg = GUI.color;
                GUI.color = hoverBgColor;
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = oldBg;
            }

            // Draw outline（悬停描边提到全亮）
            Color border = enabled
                ? (isHovered ? new Color(outlineColor.r, outlineColor.g, outlineColor.b, 1f) : outlineColor)
                : new Color(Paper.r, Paper.g, Paper.b, 0.35f);
            DrawOutline(rect, 1f, border);

            // Draw text
            var oldTextColor = style.normal.textColor;
            var oldHoverColor = style.hover.textColor;
            var oldActiveColor = style.active.textColor;

            // 悬停"变亮不变色"：把传入描边色的 alpha 提满，深浅两种上下文都成立
            Color txtColor = enabled
                ? (isHovered ? new Color(outlineColor.r, outlineColor.g, outlineColor.b, 1f) : outlineColor)
                : new Color(Paper.r, Paper.g, Paper.b, 0.35f);
            style.normal.textColor = txtColor;
            style.hover.textColor = txtColor;
            style.active.textColor = txtColor;

            GUI.Label(rect, text.ToUpper(), style);

            style.normal.textColor = oldTextColor;
            style.hover.textColor = oldHoverColor;
            style.active.textColor = oldActiveColor;

            return enabled && isClicked;
        }
    }
}
