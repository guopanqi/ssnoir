#nullable enable
using System.Collections.Generic;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    // 标注：空间里一段不可操作的说明。
    //
    // <b>这是第三个视觉族，它的定义是「不是板子」。</b>动作卡与人物卡是有框的器物，
    // 地点与普通节点是无边悬浮的浮卡——两者都还是一块 Ink 填充加一道硬投影，
    // 那正是「可以拿起来的东西」的标志。标注把这三样全部去掉：没有底、没有影子、
    // 没有框，只剩一条细线和线下面的字。玩家不必读内容就知道它碰不得。
    //
    // 两条硬规则：
    // - <b>永不可点</b>：没有 hover、没有聚焦、没有点击目标。它唯一的反馈是自己变了
    //   （见 Pulse）——一个不能碰的东西只能靠变化证明它活着。
    // - <b>引线就是那条细线本身</b>：线走到头长出字，而不是一个盒子外接了一根线。
    //   所以标注自己画线，不进 CardLeaderLineDrawer——那一层是给卡片的，
    //   它接的是矩形四边的中点，那个接法一看就是「线连着一个物件」。
    //
    // 两档摆法：
    // - <b>锚定标注</b>（DrawAnchored）：解析得到场景锚点，浮在边距上，一笔指过去。
    // - <b>场景标注</b>（DrawSceneBand）：没有锚点，升到画面上方的带子里，横排、放不下就换行。
    //   它不指任何具体的东西，指的是整个画面，所以没有引线——它是这张图纸的标题栏。
    public static class AnnotationDrawer
    {
        // 标注不是动作卡，不能借用动作卡的宽度。250 会让它在窄画幅里同普通卡争位，
        // 也让一句说明看起来像另一张可操作的牌。锚定标注收成信息节点的基准宽度。
        public const float AnchoredWidth = 220f;

        // 字离竖线多远。调用方按这个值把标注整块推离锚点（见 IMGUIWorldRenderer），
        // 线正好落在 anchor.x 上，字贴在线旁。
        public const float StemGap = 8f;

        // 雾罩：多浓、往字外散多远。散得宽才看不出矩形；浓度只要够把线稿压成底纹。
        private const float FogAlpha = 0.72f;
        private const float FogFeather = 28f;

        // 场景标注带。宽度<b>由内容定</b>：一条短句就只占一条短句那么宽，连它那根细线
        // 一起收到句子的长度。等分整行是错的——它把一句话撑成一条横幅，读起来像标题栏，
        // 而标注只是贴在这个地方的一小条字，该多大就多大。
        private const float SceneNoteMinWidth = 120f;
        private const float SceneNoteMaxWidth = 360f;
        private const float SceneNoteGap = 24f;
        private const float SceneRowGap = 14f;
        private const float SceneBandTopPad = 6f;
        private const float SceneBandBottomPad = 10f;
        private const float SceneBandSideMargin = 16f;

        // 单条标注的内部节奏。
        private const float RuleToTitle = 5f;
        private const float TitleToBody = 4f;
        private const float TitleRowH = 22f;
        private const float LabelReadoutGap = 10f;

        private const int TitleFontSize = 15;
        private const int BodyFontSize = 13;
        private const int ReadoutFontSize = 14;

        private const float SegmentSize = 9f;
        private const float SegmentGap = 4f;
        private const float DialDiameter = 22f;

        // 线与字都要压在亮着的窗户上还能读。垫一层暗色再画本体，比给整条标注
        // 垫一块半透明底板好——底板一加，它就又变回板子了。
        private static readonly Color Halo = new Color(0f, 0f, 0f, 0.62f);
        private static readonly Vector2 HaloOffset = new Vector2(1f, 1f);

        private static Color RuleColor(float pulse)
            => new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, Mathf.Lerp(0.45f, 1f, pulse));

        private static Color ReadoutColor(float pulse)
            => Color.Lerp(IMGUIStyles.Gold, Color.white, pulse * 0.6f);

        public static bool IsAnnotation(GameNode node)
            => node.Resolve != null && node.Resolve.Type == ResolveType.Note;

        // ── 单条标注 ───────────────────────────────────────────────────

        public static float MeasureHeight(GameNode node, float width)
            => Layout(new Rect(0f, 0f, width, 1f), node, draw: false, pulse: 0f);

        /// <summary>锚定标注：那一根从锚点直上的线 + 挂在线旁的字。</summary>
        public static void DrawAnchored(Rect rect, GameNode node, Vector2 anchorPos)
        {
            float pulse = Pulse(node);
            DrawLeader(rect, anchorPos, pulse);
            Layout(rect, node, draw: true, pulse: pulse, topRule: false);
        }

        /// <summary>
        /// 一条标注画多高、画在哪。量和画共用同一段代码——预留的位置和实际画的位置
        /// 分两处算，迟早会有一处忘了改。
        /// </summary>
        private static float Layout(Rect rect, GameNode node, bool draw, float pulse, bool topRule = true)
        {
            // 标注上的钟按宿主记脉冲（见 ClockPulse.BeginHost）。
            ClockPulse.BeginHost(node.Name);
            try { return LayoutInner(rect, node, draw, pulse, topRule); }
            finally { ClockPulse.EndHost(); }
        }

        private static float LayoutInner(Rect rect, GameNode node, bool draw, float pulse, bool topRule)
        {
            var resolve = node.Resolve;
            if (resolve == null) return 0f;

            string title = resolve.NoteTitle ?? string.Empty;
            string body = resolve.NoteText ?? string.Empty;
            var clock = resolve.Clock;

            float y = rect.y;

            // 先罩雾再写字：字底下的线条退进场景底色里，字才读得出来，场景又没被盖住。
            // 量一遍高度是为了知道罩多大——量和画是同一段代码，所以这里递归调自己的不画分支。
            if (draw)
            {
                float height = Layout(rect, node, draw: false, pulse: 0f, topRule);
                var cam = Camera.main;
                Color fog = cam != null ? cam.backgroundColor : IMGUIStyles.Ink;
                IMGUIStyles.DrawFogPatch(new Rect(rect.x, rect.y, rect.width, height), fog, FogAlpha, FogFeather);
            }

            // 顶上那道横线只属于场景标注带（它是标题栏）。锚定标注的线是竖着的那根，
            // 由 DrawLeader 画。
            if (draw && topRule)
                DrawRule(rect.x, rect.xMax, y, pulse);

            // 标题行：标题在左，读数在右。只有读数没标题时，读数占左边——
            // 不留一段莫名其妙的空白。
            bool hasTitleRow = !string.IsNullOrWhiteSpace(title) || clock != null;
            if (hasTitleRow)
            {
                y += RuleToTitle;
                if (draw)
                {
                    var row = new Rect(rect.x, y, rect.width, TitleRowH);
                    float readoutW = clock == null ? 0f : ReadoutWidth(clock);
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        if (clock != null)
                            DrawReadout(new Rect(row.x, row.y, readoutW, row.height), clock, pulse);
                    }
                    else
                    {
                        // 读数跟着标题走，不贴右边缘。正文长、标注被撑到上限时，
                        // 右对齐会把「□□□」甩到标题外几百像素，中间横穿画面——
                        // 那时读数和它的名字已经不像同一条东西了。
                        // 只有标题本身长到顶满可用宽度，读数才退回右端。
                        var titleStyle = TitleStyle();
                        float gap = readoutW > 0f ? LabelReadoutGap : 0f;
                        float titleRoom = Mathf.Max(0f, row.width - readoutW - gap);
                        float titleW = Mathf.Min(titleStyle.CalcSize(new GUIContent(title)).x, titleRoom);
                        LabelWithHalo(new Rect(row.x, row.y, titleW, row.height), title, titleStyle);
                        if (clock != null)
                        {
                            float readoutX = Mathf.Min(row.x + titleW + gap, row.xMax - readoutW);
                            DrawReadout(new Rect(readoutX, row.y, readoutW, row.height), clock, pulse);
                        }
                    }
                }
                y += TitleRowH;
            }

            if (!string.IsNullOrWhiteSpace(body))
            {
                var bodyStyle = BodyStyle(pulse);
                float bodyH = bodyStyle.CalcHeight(new GUIContent(body), rect.width);
                y += hasTitleRow ? TitleToBody : RuleToTitle;
                if (draw)
                    LabelWithHalo(new Rect(rect.x, y, rect.width, bodyH), body, bodyStyle);
                y += bodyH;
            }

            return y - rect.y;
        }

        private static GUIStyle TitleStyle()
        {
            var style = new GUIStyle(IMGUIStyles.ClockLabel)
            {
                fontSize = IMGUIStyles.FontSize(TitleFontSize),
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Clip,
                normal = { textColor = IMGUIStyles.TextPrimary }
            };
            IMGUIStyles.ApplyStrongFont(style);
            return style;
        }

        private static GUIStyle BodyStyle(float pulse)
        {
            return new GUIStyle(IMGUIStyles.CardSubtitle)
            {
                fontSize = IMGUIStyles.FontSize(BodyFontSize),
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                normal =
                {
                    textColor = new Color(
                        IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b,
                        Mathf.Lerp(0.62f, 1f, pulse))
                }
            };
        }

        // ── 那条细线 ───────────────────────────────────────────────────

        private static void DrawRule(float xMin, float xMax, float y, float pulse)
        {
            var a = new Vector2(xMin, y);
            var b = new Vector2(xMax, y);
            IMGUIStyles.DrawLine(a + HaloOffset, b + HaloOffset, Halo, 2f);
            IMGUIStyles.DrawLine(a, b, RuleColor(pulse), 1f);
        }

        /// <summary>
        /// 一根竖线陪着整段字（字顶到字底），锚点从近的那一端接进来；字挂在线的一侧。
        /// 线不接矩形的角、不拐横线——一拐就成了框的一角，整条标注立刻读成「一个盒子
        /// 外接一根线」。要的是：线从地上长出来，字长在线旁边。
        ///
        /// 竖线的 x 由字的近侧边退 StemGap 得到，通常正好落在 anchor.x 上（调用方就是
        /// 这么摆的）。若排布把字整块推偏了，锚点到线脚那一小段斜着走，斜段在字的下方，
        /// 不压字。
        /// </summary>
        private static void DrawLeader(Rect rect, Vector2 anchor, float pulse)
        {
            const float ringSize = 10f;
            const float ringClearance = ringSize / 2f + 1f;

            // 锚点落在标注自己身上：线整段都在字底下，画了也看不见，只留那个环。
            bool overlapping = rect.Contains(anchor);
            Color color = RuleColor(pulse);

            if (!overlapping)
            {
                bool textOnRight = anchor.x < rect.center.x;
                float stemX = textOnRight ? rect.xMin - StemGap : rect.xMax + StemGap;
                // 竖线永远陪完整段字（从字顶到字底），锚点接到离它近的那一端：
                // 锚点在字下方就从线脚进来，锚点在字上方就从线头进来。线不能在字
                // 中途停住——那样字的下半截就悬空了，像线只指着标题。
                var head = new Vector2(stemX, rect.y);
                var foot = new Vector2(stemX, rect.yMax);
                Vector2 entry = anchor.y >= rect.center.y ? foot : head;
                Vector2 start = StepOff(anchor, entry, ringClearance);

                IMGUIStyles.DrawLine(start + HaloOffset, entry + HaloOffset, Halo, 3f);
                IMGUIStyles.DrawLine(head + HaloOffset, foot + HaloOffset, Halo, 3f);
                IMGUIStyles.DrawLine(start, entry, color, 1f);
                IMGUIStyles.DrawLine(head, foot, color, 1f);
            }

            var ringRect = new Rect(anchor.x - ringSize / 2f, anchor.y - ringSize / 2f, ringSize, ringSize);
            var oldColor = GUI.color;
            IMGUIStyles.SetColor(new Color(0f, 0f, 0f, color.a * 0.55f));
            GUI.DrawTexture(new Rect(ringRect.x - 1f, ringRect.y - 1f, ringRect.width + 2f, ringRect.height + 2f),
                IMGUIStyles.AnchorRingTexture);
            IMGUIStyles.SetColor(color);
            GUI.DrawTexture(ringRect, IMGUIStyles.AnchorRingTexture);
            GUI.color = oldColor;
        }

        private static Vector2 StepOff(Vector2 from, Vector2 towards, float clearance)
        {
            Vector2 delta = towards - from;
            float distance = delta.magnitude;
            return distance <= clearance ? from : from + delta / distance * clearance;
        }

        // ── 读数 ───────────────────────────────────────────────────────

        private static float ReadoutWidth(GameClock clock)
        {
            string fraction = $"{clock.Current}/{clock.Max}";
            return clock.Style switch
            {
                ClockStyle.Gauge => Mathf.Max(0f,
                    clock.Max * SegmentSize + Mathf.Max(0, clock.Max - 1) * SegmentGap),
                ClockStyle.Countdown => DialDiameter,
                _ => TextWidth(fraction),
            };
        }

        private static float TextWidth(string text)
        {
            var style = new GUIStyle(IMGUIStyles.ClockValue) { fontSize = IMGUIStyles.FontSize(ReadoutFontSize) };
            IMGUIStyles.ApplyStrongFont(style);
            return style.CalcSize(new GUIContent(text)).x;
        }

        private static void DrawReadout(Rect rect, GameClock clock, float pulse)
        {
            // 整条标注的提亮（pulse）说「这里变了」，格子自己再一格一格换（ClockPulse），
            // 和卡上的徽章走同一串时间表。
            ClockPulse.Note(clock);
            Color active = ReadoutColor(pulse);
            string fraction = $"{clock.Current}/{clock.Max}";

            if (clock.Style == ClockStyle.Gauge)
            {
                float dy = rect.y + (rect.height - SegmentSize) * 0.5f;
                // 空格也得垫一层暗底：没有卡面托着，只画一圈线的话，压到亮窗户上就什么都看不见了。
                var empty = new Color(0f, 0f, 0f, 0.45f);
                var emptyOutline = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.42f);
                for (int i = 0; i < clock.Max; i++)
                {
                    var pip = UIScale.PixelSnap(
                        new Rect(rect.x + i * (SegmentSize + SegmentGap), dy, SegmentSize, SegmentSize));
                    ClockPulse.DrawCell(pip, i < clock.Current, active, empty,
                        ClockPulse.CellPulse(clock, i), r => IMGUIStyles.DrawOutline(r, 1f, emptyOutline));
                }
                return;
            }

            if (clock.Style == ClockStyle.Countdown)
            {
                float diameter = Mathf.Min(DialDiameter, Mathf.Min(rect.width, rect.height));
                // 环、底盘和数字共用同一个整数 Rect：半像素的差在 22px 上就看得出来。
                var dialRect = UIScale.PixelSnap(
                    new Rect(rect.xMax - diameter, rect.center.y - diameter * 0.5f, diameter, diameter));

                // 标注没有卡面托着，直接压在城市上。先垫一层暗底盘，中心的数字才读得出来；
                // 底盘比环略小一圈，免得在环外多出一道边。
                float discInset = diameter * 0.06f;
                ShapeDrawer.DrawDisc(
                    new Rect(dialRect.x + discInset, dialRect.y + discInset,
                        dialRect.width - discInset * 2f, dialRect.height - discInset * 2f),
                    new Color(0f, 0f, 0f, 0.58f * active.a));

                ShapeDrawer.DrawDial(dialRect, clock.Current, clock.Max, active,
                    new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.30f * active.a));

                // 数字落在环围出的空里，不再加光晕描边——细环加描边就糊成一团。
                var centerStyle = ReadoutStyle(active, TextAnchor.MiddleCenter);
                centerStyle.fontSize = IMGUIStyles.FontSize(clock.Current >= 10 ? 10 : 12);
                IMGUIStyles.DrawInkCenteredText(dialRect, clock.Current.ToString(), centerStyle);
                return;
            }

            LabelWithHalo(rect, fraction, ReadoutStyle(active, TextAnchor.MiddleRight));
        }

        private static GUIStyle ReadoutStyle(Color color, TextAnchor alignment)
        {
            var style = new GUIStyle(IMGUIStyles.ClockValue)
            {
                fontSize = IMGUIStyles.FontSize(ReadoutFontSize),
                alignment = alignment,
                wordWrap = false,
                clipping = TextClipping.Clip,
                normal = { textColor = color }
            };
            IMGUIStyles.ApplyStrongFont(style);
            return style;
        }

        // 字自己再描一圈暗边（八个方向各 1px）：雾只把线稿压淡，字与残线相切的地方还得靠
        // 这圈边把笔画从背景里切出来。
        private static readonly Vector2[] HaloRing =
        {
            new Vector2(-1f, 0f), new Vector2(1f, 0f), new Vector2(0f, -1f), new Vector2(0f, 1f),
            new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f),
        };

        private static void LabelWithHalo(Rect rect, string text, GUIStyle style)
        {
            var haloStyle = new GUIStyle(style) { normal = { textColor = Halo } };
            foreach (var o in HaloRing)
                IMGUIStyles.DrawLabel(new Rect(rect.x + o.x, rect.y + o.y, rect.width, rect.height), text, haloStyle);
            IMGUIStyles.DrawLabel(rect, text, style);
        }

        // ── 场景标注带 ─────────────────────────────────────────────────

        /// <summary>带子有多高。渲染器要先拿到它，才知道卡片该从哪儿往下让。</summary>
        public static float MeasureSceneBandHeight(IReadOnlyList<GameNode> notes, float topY, Rect reserved)
            => LayoutSceneBand(notes, topY, reserved, draw: false);

        public static void DrawSceneBand(IReadOnlyList<GameNode> notes, float topY, Rect reserved)
            => LayoutSceneBand(notes, topY, reserved, draw: true);

        /// <summary>
        /// 各条按自己需要多宽排，塞不下就换行。信息带的首要职责是总览：不论正文多长，
        /// 一行都必须留得出三个节点的位置；长正文向下换行，而不是横向挤走别的节点。
        ///
        /// <paramref name="reserved"/> 是画面上沿已经被占掉的那块（钉住条）。带子<b>绕着它排</b>，
        /// 不整条让到它下面：钉住条只有 300 宽，让整行等于把一条短句右边一千多像素全空掉。
        /// 恒定的是钉住条的位置，不是标注带必须下移。
        /// 绕排后剩的宽度放不下一条标注，这一行才落到它底下——窄画幅于是自动退回堆叠，
        /// 同一套数字，不分设备。钉住条在左就从它右边起排，在右就收到它左边为止。
        /// </summary>
        private static float LayoutSceneBand(IReadOnlyList<GameNode> notes, float topY, Rect reserved, bool draw)
        {
            if (notes == null || notes.Count == 0) return 0f;

            Rect safe = UIScale.SafeArea;
            float rightEdge = safe.xMax - SceneBandSideMargin;
            bool hasReserved = reserved.width > 0f && reserved.height > 0f;

            float y = topY + SceneBandTopPad;
            int i = 0;
            while (i < notes.Count)
            {
                // 行的归属按行首那一条线判断。一行偶尔比钉住条长出去一截无所谓——
                // 它只是继续用窄一点的宽度，不会压到任何东西。
                float left = safe.xMin + SceneBandSideMargin;
                float rowRight = rightEdge;
                if (hasReserved && y < reserved.yMax)
                {
                    bool reservedOnLeft =
                        reserved.xMin - safe.xMin <= safe.xMax - reserved.xMax;
                    if (reservedOnLeft)
                    {
                        float beside = reserved.xMax + SceneNoteGap;
                        if (rightEdge - beside >= SceneNoteMinWidth)
                            left = beside;
                        else
                            y = reserved.yMax + SceneBandTopPad;   // 挤不下就落到钉住条底下
                    }
                    else
                    {
                        float capped = reserved.xMin - SceneNoteGap;
                        if (capped - left >= SceneNoteMinWidth)
                            rowRight = capped;
                        else
                            y = reserved.yMax + SceneBandTopPad;   // 挤不下就落到钉住条底下
                    }
                }

                float maxRowW = Mathf.Max(1f, rowRight - left);
                // 两个间隙之外的三等份是单条标注的硬上限。这个值放在布局处计算，
                // 不能只调 SceneNoteMaxWidth：可用宽度会随画幅和钉住条变化。
                float threeColumnWidth = Mathf.Max(1f, (maxRowW - SceneNoteGap * 2f) / 3f);

                var widths = new List<float>();
                float totalW = 0f;
                while (i + widths.Count < notes.Count)
                {
                    float want = Mathf.Min(NaturalWidth(notes[i + widths.Count]), threeColumnWidth);
                    float withGap = widths.Count == 0 ? want : totalW + SceneNoteGap + want;
                    if (widths.Count > 0 && withGap > maxRowW)
                        break;
                    widths.Add(want);
                    totalW = widths.Count == 1 ? widths[0] : withGap;
                }

                float rowHeight = 0f;
                float x = left;
                for (int j = 0; j < widths.Count; j++)
                {
                    var rect = new Rect(x, y, widths[j], 0f);
                    float h = draw
                        ? Layout(rect, notes[i + j], draw: true, pulse: Pulse(notes[i + j]))
                        : MeasureHeight(notes[i + j], widths[j]);
                    rowHeight = Mathf.Max(rowHeight, h);
                    x += widths[j] + SceneNoteGap;
                }

                y += rowHeight + SceneRowGap;
                i += widths.Count;
            }

            return y - SceneRowGap + SceneBandBottomPad - topY;
        }

        /// <summary>
        /// 这条标注按内容需要多宽。正文能一行放下就一行放下，长了才让它在上限处换行——
        /// 这样一句短话得到一条短线，一段长话才铺开。
        /// </summary>
        private static float NaturalWidth(GameNode node)
        {
            var resolve = node.Resolve;
            if (resolve == null) return SceneNoteMinWidth;

            float titleRow = 0f;
            if (!string.IsNullOrWhiteSpace(resolve.NoteTitle))
                titleRow = TitleStyle().CalcSize(new GUIContent(resolve.NoteTitle)).x;
            if (resolve.Clock != null)
                titleRow += (titleRow > 0f ? LabelReadoutGap : 0f) + ReadoutWidth(resolve.Clock);

            float bodyRow = 0f;
            if (!string.IsNullOrWhiteSpace(resolve.NoteText))
            {
                var probe = new GUIStyle(BodyStyle(0f)) { wordWrap = false };
                bodyRow = probe.CalcSize(new GUIContent(resolve.NoteText)).x;
            }

            return Mathf.Clamp(Mathf.Max(titleRow, bodyRow), SceneNoteMinWidth, SceneNoteMaxWidth);
        }

        // ── 变化就是它唯一的反馈 ───────────────────────────────────────

        // 标注不能被摸，所以除了「它变了」之外没有任何东西可以证明它还活着。
        // 值一动，线和读数亮起来再落回去——它在动，但不是为你动。
        private const float PulseDuration = 0.9f;

        private struct PulseState
        {
            public string Signature;
            public float StartedAt;
            public float SeenAt;
        }

        private static readonly Dictionary<string, PulseState> _pulses = new Dictionary<string, PulseState>();

        private static float Pulse(GameNode node)
        {
            var resolve = node.Resolve;
            if (resolve == null) return 0f;

            string signature = resolve.Clock == null
                ? $"{resolve.NoteTitle}|{resolve.NoteText}"
                : $"{resolve.NoteTitle}|{resolve.NoteText}|{resolve.Clock.Current}/{resolve.Clock.Max}";
            float now = Time.time;

            if (!_pulses.TryGetValue(node.Name, out var state))
            {
                // 第一次见到不算「变了」：否则每次进场所有标注会一起闪一下，
                // 那正好把真正的变化淹掉。
                _pulses[node.Name] = new PulseState { Signature = signature, StartedAt = -1f, SeenAt = now };
                PruneStalePulses(now);
                return 0f;
            }

            if (!string.Equals(state.Signature, signature, System.StringComparison.Ordinal))
            {
                state.Signature = signature;
                state.StartedAt = now;
            }
            state.SeenAt = now;
            _pulses[node.Name] = state;

            if (state.StartedAt < 0f) return 0f;
            float elapsed = now - state.StartedAt;
            if (elapsed >= PulseDuration) return 0f;
            // 立刻亮起、慢慢落回：变化本身要抢到眼睛，余韵不该抢。
            return 1f - Mathf.Pow(elapsed / PulseDuration, 0.6f);
        }

        private static void PruneStalePulses(float now)
        {
            if (_pulses.Count <= 64) return;
            var stale = new List<string>();
            foreach (var pair in _pulses)
                if (now - pair.Value.SeenAt > 30f)
                    stale.Add(pair.Key);
            foreach (var key in stale)
                _pulses.Remove(key);
        }
    }
}
