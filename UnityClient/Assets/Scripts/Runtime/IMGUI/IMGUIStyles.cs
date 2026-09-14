#nullable enable
using UnityEngine;

namespace SSNoir.IMGUI
{
    public static class IMGUIStyles
    {
        public static Font? ChineseFont;
        public static Font? SemiboldFont;

        // ══════════════════════════════════════════════════════════════════
        // 图层透明度
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 当前图层的整体不透明度。1 = 照常画。
        ///
        /// IMGUI 没有"图层"这回事：<c>GUI.color</c> 是一个全局乘子，会乘进这之后画的一切
        /// （贴图、线、连 GUIStyle 的文字一起）。所以"整层淡入淡出"本来是免费的——只要
        /// **没有人去覆写它**。而各个 drawer 一直在绝对地写（写死一个颜色、画完还原成
        /// <c>Color.white</c>），一写就把图层那份 alpha 抹掉了。
        ///
        /// 于是有了下面这对函数：颜色照给，alpha 乘上去。所有 drawer 的 <c>GUI.color</c>
        /// 赋值都改走它们，图层透明度才真的是一层的属性，而不是每个 drawer 各自记得乘一下。
        ///
        /// 用法是**成对的作用域**（见 <see cref="BeginLayer"/>）：谁开谁关，不要长期挂着。
        /// </summary>
        public static float LayerAlpha { get; private set; } = 1f;

        /// <summary>
        /// 开一层透明度，返回上一层的值——调用方负责在画完之后 <see cref="EndLayer"/> 还原。
        /// 嵌套是相乘的：一层 0.5 里再开一层 0.5，得到 0.25，和直觉一致。
        /// </summary>
        public static float BeginLayer(float alpha)
        {
            float previous = LayerAlpha;
            LayerAlpha = previous * Mathf.Clamp01(alpha);
            GUI.color = new Color(1f, 1f, 1f, LayerAlpha);
            return previous;
        }

        /// <summary>
        /// 把图层透明度硬掰回 1。每帧开头调一次：BeginLayer / EndLayer 是成对的，中间任何
        /// 一次异常都会让这一对断掉，而它是全局状态——断一次，整个界面从此半透明。
        /// </summary>
        public static void ResetLayer()
        {
            LayerAlpha = 1f;
            GUI.color = Color.white;
        }

        /// <summary>还原 <see cref="BeginLayer"/> 返回的那个值。</summary>
        public static void EndLayer(float previous)
        {
            LayerAlpha = previous;
            GUI.color = new Color(1f, 1f, 1f, LayerAlpha);
        }

        /// <summary>
        /// 设当前绘制颜色：色相照给，alpha 乘上图层那一份。
        /// 一切原本直接写 <c>GUI.color</c> 的地方都改走这里。
        /// </summary>
        public static void SetColor(Color color)
        {
            GUI.color = LayerAlpha >= 1f
                ? color
                : new Color(color.r, color.g, color.b, color.a * LayerAlpha);
        }

        /// <summary>
        /// 画完一笔之后还原：等价于原来的还原成白，但留着图层的 alpha。
        /// </summary>
        public static void ResetColor()
        {
            GUI.color = LayerAlpha >= 1f ? Color.white : new Color(1f, 1f, 1f, LayerAlpha);
        }

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

        // ── 暗版禁用态洗色：换底色而非降透明度——Ink 已接近纯黑，再叠黑不可见。
        // 复用 PaperTextDisabled 的暖灰基调，让"不可用"读成"褪色的纸"，与"待命"的幽灵描边区分开。
        public static readonly Color DisabledWash = PaperTextDisabled; // #8A8375，暗卡上的统一禁用灰

        // ── 强调色：做旧金（全局唯一主强调色）──
        public static readonly Color Gold       = new Color(0.910f, 0.765f, 0.353f, 1f); // #E8C35A
        public static readonly Color GoldOnDark  = new Color(0.165f, 0.129f, 0.027f, 1f); // #2A2107 金底上的深字

        // ── 印章红（高危 / 失败，专用不做按钮）──
        public static readonly Color SealRed = new Color(0.702f, 0.251f, 0.165f, 1f); // #B3402A

        // ── 书签色板（卡顶的类别 / 风险签，DESIGN.md）──
        // 签就是一张 Paper：纸底 + 墨字，和弹出纸层、白底数值格同一张纸。语义不靠换纸色说，
        // 只靠左侧一道色条——色条取结果三色 / 金，和进度格、生命条的阈值上色是同一套语言。
        // 之前那组马卡龙彩纸（鼠尾草绿 / 粉橘 / 粉蓝 / 淡紫）在「墨与纸」色板里谁也不认识，故废。
        public static readonly Color TabPaper = Paper;
        public static readonly Color TabInk   = PaperInk;
        // 禁用态：褪成灰纸、不带色条，而不是压暗——压暗会立刻和深色卡糊在一起。
        public static readonly Color TabMutedPaper = new Color(0.796f, 0.812f, 0.839f, 1f); // #CBCFD6
        public static readonly Color TabMutedInk   = new Color(0.290f, 0.271f, 0.235f, 1f); // #4A453C

        // ── 概率/结果三色（沉着版）──
        public static readonly Color OddsFail    = new Color(0.820f, 0.416f, 0.306f, 1f); // #D16A4E 陶红
        public static readonly Color OddsNeutral = new Color(0.831f, 0.694f, 0.345f, 1f); // #D4B158 赭黄
        public static readonly Color OddsSuccess = new Color(0.576f, 0.690f, 0.416f, 1f); // #93B06A 苔绿

        // ── 面板不透明度阶梯 ──
        public const float PanelOpacity  = 0.88f; // 面板 ≥88%
        public const float ModalOpacity  = 0.96f; // 弹窗 ≥96%
        public const float MaskOpacity   = 0.82f; // 遮罩：60% 在浅色场景里压不住，纸窗背后要暗下去而不是变白

        // ── 组合令牌（由基础令牌推导；旧 Blueprint Noir 调色板已全部退役）──
        // 弹窗是"递到面前的一张纸"：Paper 底 @96%（配 ModalTitle/ModalBody 的 PaperInk 字）
        public static readonly Color ModalBg = new Color(Paper.r, Paper.g, Paper.b, ModalOpacity);
        // 全屏遮罩：深墨 @60%
        public static readonly Color Blocker = new Color(0.039f, 0.047f, 0.071f, MaskOpacity);
        // 判定结果三色（与命运预览同源）
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

            // 这里曾经建一个 Hidden/Internal-Colored 材质给 GL 画饼和气泡尾巴用。
            // 那两处都改成贴图了（见 ShapeDrawer），界面里不再有 GL 绘制。

            // Font sizes are snapped so (fontSize × scale) lands on an integer
            // physical pixel, minimising sub-pixel blur at non-1.0 scales.
            CardTitle      = MakeStyle(FontSize(23), TextPrimary,        TextAnchor.MiddleCenter, FontStyle.Bold);
            CardSubtitle   = MakeStyle(FontSize(14), TextSecondary,      TextAnchor.MiddleCenter, FontStyle.Normal);
            CardTypeTag    = MakeStyle(FontSize(15), TextSecondary,      TextAnchor.MiddleCenter, FontStyle.Normal);
            SlotLabel      = MakeStyle(FontSize(16), PaperInk,           TextAnchor.MiddleCenter, FontStyle.Bold);
            ExecuteLabel   = MakeStyle(FontSize(16), GoldOnDark,         TextAnchor.MiddleCenter, FontStyle.Bold);
            StatusLabel    = MakeStyle(FontSize(14), TextPrimary,        TextAnchor.MiddleLeft,   FontStyle.Bold);
            SectionLabel   = MakeStyle(FontSize(16), TextSecondary,      TextAnchor.MiddleLeft,   FontStyle.Normal);
            ToastLabel     = MakeStyle(FontSize(14), GoldOnDark,         TextAnchor.MiddleCenter, FontStyle.Bold);
            ModalTitle     = MakeStyle(FontSize(22), PaperInk,           TextAnchor.MiddleCenter, FontStyle.Bold);
            ModalBody      = MakeStyle(FontSize(14), PaperTextPrimary,   TextAnchor.MiddleLeft,   FontStyle.Normal);
            FlippedTitle   = MakeStyle(FontSize(18), TextPrimary,        TextAnchor.MiddleCenter, FontStyle.Bold);
            FlippedContent = MakeStyle(FontSize(14), TextPrimary,        TextAnchor.UpperCenter,  FontStyle.Normal);
            FlippedTip     = MakeStyle(FontSize(12), TextSecondary,      TextAnchor.MiddleCenter, FontStyle.Italic);
            ClockLabel     = MakeStyle(FontSize(12), TextPrimary,        TextAnchor.MiddleLeft,   FontStyle.Bold);
            ClockValue     = MakeStyle(FontSize(12), Gold,               TextAnchor.MiddleRight,  FontStyle.Bold);
            // 时钟标签和数值是徽章内的单行信息。MakeStyle 默认允许换行，
            // 会把类似「45/100」的分数拆成两行，破坏徽章的固定高度。
            ClockLabel.wordWrap = false;
            ClockLabel.clipping = TextClipping.Clip;
            ClockValue.wordWrap = false;
            ClockValue.clipping = TextClipping.Clip;
            DropdownItem   = MakeStyle(FontSize(14), TextSecondary,      TextAnchor.MiddleLeft,   FontStyle.Normal);
            DropdownCurrent= MakeStyle(FontSize(14), TextPrimary,        TextAnchor.MiddleLeft,   FontStyle.Normal);
            CursorFollower = MakeStyle(FontSize(12), TextPrimary,        TextAnchor.MiddleCenter, FontStyle.Bold);
            SceneLabel     = MakeStyle(FontSize(14), TextSecondary,      TextAnchor.MiddleLeft,   FontStyle.Normal);
            HelpTip        = MakeStyle(FontSize(14), TextSecondary,      TextAnchor.MiddleLeft,   FontStyle.Normal);
        }

        // 字号的唯一入口。把虚拟字号吸附到「乘以 Scale 后正好是整数物理像素」的那个值，
        // 消掉非整数缩放（0.75 / 1.25）下的次像素模糊。Scale=1 时原样返回。
        //
        // 注意方向：这里是**除以** Scale 反算回虚拟空间，不是乘。GUI.matrix 已经统一缩放了
        // 整个 IMGUI，任何再乘一次 Scale 的写法都会让字号被平方缩放（0.75 下 11px 只剩 6px）。
        // 组件里不要各自再写一份 SF。
        public static int FontSize(int baseSize)
        {
            float scale = UIScale.Scale;
            int physPx = Mathf.RoundToInt(baseSize * scale);
            return Mathf.Max(8, Mathf.RoundToInt(physPx / scale));
        }

        private static GUIStyle MakeStyle(int fontSize, Color textColor, TextAnchor alignment, FontStyle fontStyle)
        {
            var style = new GUIStyle();
            style.font = ResolveFont(fontStyle);
            style.fontSize = fontSize;
            SetStaticTextColor(style, textColor);
            style.alignment = alignment;
            style.fontStyle = ResolveFontStyle(fontStyle);
            style.wordWrap = true;
            return style;
        }

        /// <summary>
        /// 画一段说明性文字，并挡住 Unity 隐式的 hover 变色——鼠标划过一段标签不等于它是控件，
        /// 交互反馈属于控件的底或描边。
        /// </summary>
        public static void DrawLabel(Rect position, string text, GUIStyle style)
            => DrawLabelRaw(position, text, style);

        /// <summary>纸面板的内容顶边（标题 + 分隔线之下）。三个面板共用同一个数。</summary>
        public const float ModalContentTop = 74f;

        /// <summary>
        /// 纸面板的外壳：压暗背景 → 硬投影 + 纸底 → 标题 → 关闭 X → 分隔线。返回是否点了关闭。
        ///
        /// 设置 / 队伍 / 卷宗原本各写一份几乎一样的代码，于是标题字号、X 的位置、分隔线的
        /// 深浅都各飘各的。收成一处之后，"这三个是同一类东西"是**结构上**成立的，不靠自觉维护。
        ///
        /// 标题 28px：对 14px 的正文是 2 倍差。原来 22 只有 1.6 倍，层级几乎全靠灰度撑，
        /// 而灰度差在纸底上最先被吃掉。
        /// </summary>
        public static bool DrawModalChrome(Rect panelRect, string title, IMGUIInteractionContext ui)
        {
            SetColor(Blocker);
            GUI.DrawTexture(new Rect(0f, 0f, UIScale.VW, UIScale.VH), Texture2D.whiteTexture);
            ResetColor();

            DrawShadow(panelRect, new Vector2(5f, 6f), 0.50f);
            SetColor(ModalBg);
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
            ResetColor();

            var titleStyle = new GUIStyle(ModalTitle)
            {
                fontSize = FontSize(28),
                alignment = TextAnchor.MiddleLeft,
            };
            DrawLabel(new Rect(panelRect.x + 24f, panelRect.y + 18f, panelRect.width - 80f, 32f), title, titleStyle);

            // 关闭 X：纸上次级按钮＝1px 墨描边、透明底。
            var closeRect = new Rect(panelRect.xMax - 44f, panelRect.y + 16f, 28f, 28f);
            bool closeHover = ui.CanHover(closeRect);
            if (closeHover)
            {
                SetColor(new Color(PaperInk.r, PaperInk.g, PaperInk.b, 0.08f));
                GUI.DrawTexture(closeRect, Texture2D.whiteTexture);
                ResetColor();
            }
            DrawOutline(closeRect, 1f, closeHover
                ? PaperInk
                : new Color(PaperInk.r, PaperInk.g, PaperInk.b, 0.55f));
            var closeStyle = new GUIStyle(StatusLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = closeHover ? PaperInk : PaperTextSecondary },
            };
            DrawLabel(closeRect, "X", closeStyle);

            DrawLine(
                new Vector2(panelRect.x + 24f, panelRect.y + 58f),
                new Vector2(panelRect.xMax - 24f, panelRect.y + 58f),
                new Color(PaperInk.r, PaperInk.g, PaperInk.b, 0.35f), 1f);

            if (ui.WasTapped(closeRect))
            {
                Event.current.Use();
                return true;
            }
            return false;
        }

        private static void DrawLabelRaw(Rect position, string text, GUIStyle style)
        {
            Color normal = style.normal.textColor;
            Color hover = style.hover.textColor;
            Color active = style.active.textColor;
            Color focused = style.focused.textColor;
            Color onNormal = style.onNormal.textColor;
            Color onHover = style.onHover.textColor;
            Color onActive = style.onActive.textColor;
            Color onFocused = style.onFocused.textColor;

            SetStaticTextColor(style, normal);
            GUI.Label(position, text, style);

            style.normal.textColor = normal;
            style.hover.textColor = hover;
            style.active.textColor = active;
            style.focused.textColor = focused;
            style.onNormal.textColor = onNormal;
            style.onHover.textColor = onHover;
            style.onActive.textColor = onActive;
            style.onFocused.textColor = onFocused;
        }

        private static void SetStaticTextColor(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }

        /// <summary>
        /// 按**字形的实际墨迹**把一小段文字放到 rect 正中，用于表盘中心的剩余数字。
        ///
        /// 为什么不能直接用 TextAnchor.MiddleCenter：IMGUI 居中的是**字框**——横向是
        /// advance（含左右边距），纵向是 ascent/descent（含数字根本用不到的下伸空间）。
        /// 于是「几何居中」和「看起来在正中」差一截，而且**每个数字差的还不一样**：
        /// 我们这套用的是中文半粗体，它的拉丁数字边距逐字不同，2 正了 3 和 9 就偏。
        ///
        /// 这里改成问字体要每个字形的 minX/maxX/minY/maxY，算出墨迹中心和字框中心的差，
        /// 用 contentOffset 补掉。补的是字体真实的度量，不是一个试出来的常数，
        /// 所以换字号、换字体、换数字都成立。
        /// </summary>
        public static void DrawInkCenteredText(Rect rect, string text, GUIStyle style)
        {
            var font = style.font;
            if (font == null || string.IsNullOrEmpty(text))
            {
                DrawLabel(rect, text, style);
                return;
            }

            // 不先请求，动态字体的图集里可能还没有这些字形，取到的度量全是 0。
            font.RequestCharactersInTexture(text, style.fontSize, style.fontStyle);

            float advance = 0f;
            float inkMinX = float.MaxValue, inkMaxX = float.MinValue;
            float inkMinY = float.MaxValue, inkMaxY = float.MinValue;
            bool measured = false;
            foreach (char c in text)
            {
                if (!font.GetCharacterInfo(c, out var info, style.fontSize, style.fontStyle))
                    continue;
                measured = true;
                inkMinX = Mathf.Min(inkMinX, advance + info.minX);
                inkMaxX = Mathf.Max(inkMaxX, advance + info.maxX);
                inkMinY = Mathf.Min(inkMinY, info.minY);
                inkMaxY = Mathf.Max(inkMaxY, info.maxY);
                advance += info.advance;
            }

            if (!measured || advance <= 0f)
            {
                DrawLabel(rect, text, style);
                return;
            }

            float scale = font.fontSize > 0 ? (float)style.fontSize / font.fontSize : 1f;
            float ascent = font.ascent * scale;
            float lineHeight = font.lineHeight * scale;
            float boxCenterAboveBaseline = ascent - lineHeight * 0.5f;
            float inkCenterAboveBaseline = (inkMinY + inkMaxY) * 0.5f;

            var centered = new GUIStyle(style) { alignment = TextAnchor.MiddleCenter };
            centered.contentOffset = new Vector2(
                -((inkMinX + inkMaxX) * 0.5f - advance * 0.5f),
                inkCenterAboveBaseline - boxCenterAboveBaseline);
            DrawLabel(rect, text, centered);
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
            style.fontSize = FontSize(12);
            style.normal.textColor = Paper;
            style.hover.textColor = Paper;
            style.active.textColor = new Color(0.9f, 0.9f, 0.9f);
            style.margin = new RectOffset(0, 0, 0, 0);
            style.padding = new RectOffset(4, 4, 2, 2);
            return style;
        }

        // 世界锚点上那个「就是这儿」的标记：一圈细环加一个圆心点。白色 alpha 贴图，
        // 用 GUI.color 染色，所以一张就够所有轻重档次使用。
        private static Texture2D? _anchorRingTexture;

        public static Texture2D AnchorRingTexture => _anchorRingTexture ??= MakeAnchorRing(64);

        private static Texture2D MakeAnchorRing(int size)
        {
            float center = (size - 1) / 2f;
            float outer = size * 0.47f;
            float inner = size * 0.34f;
            float dot = size * 0.11f;
            // 半个像素的过渡带：这张图会缩到 10 像素画，硬边缘会锯得很明显。
            const float feather = 1.2f;

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    float ring = Mathf.Clamp01((outer - d) / feather) * Mathf.Clamp01((d - inner) / feather);
                    float core = Mathf.Clamp01((dot - d) / feather);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Max(ring, core));
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
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
            SetColor(color);
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
                // start/end are virtual IMGUI coordinates. Compose the local line transform
                // after the current GUI matrix so the global UIScale/group transform also
                // applies to the line's origin. Reversing this order leaves the diagonal
                // line detached from the Rect while axis-aligned lines still look correct.
                GUI.matrix = matrix * Matrix4x4.TRS(start, Quaternion.Euler(0, 0, angle), Vector3.one);
                GUI.DrawTexture(new Rect(0, -thickness / 2f, dist, thickness), Texture2D.whiteTexture);
                GUI.matrix = matrix;
            }
            GUI.color = oldColor;
        }

        public static void DrawOutline(Rect rect, float thickness, Color color)
        {
            var oldColor = GUI.color;
            SetColor(color);
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
            SetColor(color);
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
            SetColor(new Color(0f, 0f, 0f, alpha));
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

        /// <summary>
        /// 雾底：一块朝四周慢慢散掉的场景底色。白描世界里「看不清」的自然形态是线条退进雾里，
        /// 不是被糊成一团——线稿一模糊只剩灰斑，反而更扎眼。所以不动画面，只在字底下罩一层
        /// 与场景底色同色的雾，线条在这里变淡，字浮在雾上。羽化很宽、没有边，看不出是块矩形。
        /// </summary>
        public static void DrawFogPatch(Rect rect, Color fog, float alpha, float feather = 28f)
        {
            if (Event.current.type != EventType.Repaint) return;
            var tex = SoftRectTexture;
            float f = Mathf.Min(feather, Mathf.Min(rect.width, rect.height) / 2f + feather);
            // 纹理三段：[0,1/3) 羽化、[1/3,2/3] 实心、(2/3,1] 羽化。九宫格铺开。
            // 九块的接缝必须落在同一个物理像素边上：矩形随镜头走到非整数坐标时，相邻两块
            // 会在同一列像素上各画半个，叠出一条更暗的线，而且时有时无。四条分界线都按
            // 物理像素取整，接缝就没有了。
            float[] xs = { UIScale.Floor(rect.x - f), UIScale.Floor(rect.x), UIScale.Floor(rect.xMax), UIScale.Floor(rect.xMax + f) };
            float[] ys = { UIScale.Floor(rect.y - f), UIScale.Floor(rect.y), UIScale.Floor(rect.yMax), UIScale.Floor(rect.yMax + f) };
            float[] us = { 0f, 1f / 3f, 2f / 3f, 1f };

            var oldColor = GUI.color;
            SetColor(new Color(fog.r, fog.g, fog.b, alpha));
            for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
            {
                var r = new Rect(xs[i], ys[j], xs[i + 1] - xs[i], ys[j + 1] - ys[j]);
                if (r.width <= 0f || r.height <= 0f) continue;
                // GUI y 向下，纹理 v 向上：第 j 行取 v = 1 - us[j+1] .. 1 - us[j]。
                var uv = new Rect(us[i], 1f - us[j + 1], us[i + 1] - us[i], us[j + 1] - us[j]);
                GUI.DrawTextureWithTexCoords(r, tex, uv);
            }
            GUI.color = oldColor;
        }

        private static Texture2D? _softRect;
        // 96×96：外圈 32px 按 smoothstep 淡出，中间 32×32 实心。用距内矩形的距离算，角上是圆的。
        private static Texture2D SoftRectTexture => _softRect ??= MakeSoftRect(96, 32);

        private static Texture2D MakeSoftRect(int size, int feather)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color[size * size];
            float inner0 = feather, inner1 = size - feather;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(inner0 - (x + 0.5f), (x + 0.5f) - inner1, 0f);
                float dy = Mathf.Max(inner0 - (y + 0.5f), (y + 0.5f) - inner1, 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy) / feather;
                float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d));
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return tex;
        }

        // 便签：RotateAroundPivot 包住"1px 黑影 + 彩纸矩形 + 深色字"。rotationDeg 建议 ±1–2°。
        public static void DrawStickyNote(Rect rect, string text, Color paperColor, Color textColor, float rotationDeg, GUIStyle? baseStyle = null)
        {
            var pivot = rect.center;
            var oldMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(rotationDeg, pivot);

            // 1px 黑影
            var oldColor = GUI.color;
            SetColor(new Color(0f, 0f, 0f, 0.35f));
            GUI.DrawTexture(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), Texture2D.whiteTexture);

            // 彩纸底
            SetColor(paperColor);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = oldColor;

            // 深色字
            var style = new GUIStyle(baseStyle ?? GUI.skin.label)
            {
                font = ChineseFont,
                alignment = TextAnchor.MiddleCenter,
            };
            style.normal.textColor = textColor;
            DrawLabel(rect, text, style);

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
            DrawLabel(square, text, style);

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
                SetColor(i < current ? activeColor : inactiveColor);
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
                SetColor(hoverBgColor);
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

            DrawLabel(rect, text.ToUpper(), style);

            style.normal.textColor = oldTextColor;
            style.hover.textColor = oldHoverColor;
            style.active.textColor = oldActiveColor;

            return enabled && isClicked;
        }
    }
}
