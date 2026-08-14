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
        // 锚定标注与动作卡同宽：同一条边距里的东西共用一个入口 x，几行宽窄不同的
        // 内容才不会各自为政。
        public const float AnchoredWidth = 250f;

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
        private const float PieDiameter = 15f;
        private const float PieValueGap = 5f;

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

        /// <summary>锚定标注：本体 + 那一笔从锚点走过来的线。</summary>
        public static void DrawAnchored(Rect rect, GameNode node, Vector2 anchorPos)
        {
            float pulse = Pulse(node);
            DrawLeader(rect, anchorPos, pulse);
            Layout(rect, node, draw: true, pulse: pulse);
        }

        /// <summary>
        /// 一条标注画多高、画在哪。量和画共用同一段代码——预留的位置和实际画的位置
        /// 分两处算，迟早会有一处忘了改。
        /// </summary>
        private static float Layout(Rect rect, GameNode node, bool draw, float pulse)
        {
            var resolve = node.Resolve;
            if (resolve == null) return 0f;

            string title = resolve.NoteTitle ?? string.Empty;
            string body = resolve.NoteText ?? string.Empty;
            var clock = resolve.Clock;

            float y = rect.y;

            if (draw)
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
                        float gap = readoutW > 0f ? LabelReadoutGap : 0f;
                        float titleW = Mathf.Max(0f, row.width - readoutW - gap);
                        LabelWithHalo(new Rect(row.x, row.y, titleW, row.height), title, TitleStyle());
                        if (clock != null)
                            DrawReadout(new Rect(row.xMax - readoutW, row.y, readoutW, row.height), clock, pulse);
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
        /// 引线接在细线的端点上，同粗同色——看上去就是同一笔折过去的。
        /// 卡片的引线接四边中点（见 CardLeaderLineDrawer），那是「线连着一个物件」；
        /// 这里要的是「一根线走到头，长出了字」。
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
                // 靠锚点那一端的线头。标注贴着边距的内缘排，所以这个端点也是引线
                // 每次都从同一侧进来的那个点。
                var attach = new Vector2(anchor.x < rect.center.x ? rect.xMin : rect.xMax, rect.y);
                var elbow = new Vector2(anchor.x, rect.y);
                Vector2 start = StepOff(anchor, elbow, ringClearance);

                IMGUIStyles.DrawLine(start + HaloOffset, elbow + HaloOffset, Halo, 3f);
                IMGUIStyles.DrawLine(elbow + HaloOffset, attach + HaloOffset, Halo, 3f);
                IMGUIStyles.DrawLine(start, elbow, color, 1f);
                IMGUIStyles.DrawLine(elbow, attach, color, 1f);
            }

            var ringRect = new Rect(anchor.x - ringSize / 2f, anchor.y - ringSize / 2f, ringSize, ringSize);
            var oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, color.a * 0.55f);
            GUI.DrawTexture(new Rect(ringRect.x - 1f, ringRect.y - 1f, ringRect.width + 2f, ringRect.height + 2f),
                IMGUIStyles.AnchorRingTexture);
            GUI.color = color;
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
                ClockStyle.Segments => Mathf.Max(0f,
                    clock.Max * SegmentSize + Mathf.Max(0, clock.Max - 1) * SegmentGap),
                ClockStyle.Pie => PieDiameter + PieValueGap + TextWidth(fraction),
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
            Color active = ReadoutColor(pulse);
            string fraction = $"{clock.Current}/{clock.Max}";

            if (clock.Style == ClockStyle.Segments)
            {
                float dy = rect.y + (rect.height - SegmentSize) * 0.5f;
                for (int i = 0; i < clock.Max; i++)
                {
                    var pip = UIScale.PixelSnap(
                        new Rect(rect.x + i * (SegmentSize + SegmentGap), dy, SegmentSize, SegmentSize));
                    if (i < clock.Current)
                    {
                        GUI.color = active;
                        GUI.DrawTexture(pip, Texture2D.whiteTexture);
                    }
                    else
                    {
                        // 空格也得垫一层暗底：没有卡面托着，只画一圈线的话，
                        // 压到亮窗户上就什么都看不见了。
                        GUI.color = new Color(0f, 0f, 0f, 0.45f);
                        GUI.DrawTexture(pip, Texture2D.whiteTexture);
                        GUI.color = Color.white;
                        IMGUIStyles.DrawOutline(pip, 1f,
                            new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.42f));
                    }
                    GUI.color = Color.white;
                }
                return;
            }

            if (clock.Style == ClockStyle.Pie)
            {
                float pieSize = Mathf.Min(PieDiameter, rect.width);
                var pieRect = new Rect(rect.x, rect.center.y - pieSize * 0.5f, pieSize, pieSize);
                float fill = clock.Max > 0 ? Mathf.Clamp01((float)clock.Current / clock.Max) : 0f;
                if (pieSize >= 4f)
                    PieDrawer.DrawPieBadge(pieRect, fill, active,
                        new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.70f));

                var pieText = new Rect(pieRect.xMax + PieValueGap, rect.y,
                    Mathf.Max(0f, rect.xMax - pieRect.xMax - PieValueGap), rect.height);
                LabelWithHalo(pieText, fraction, ReadoutStyle(active, TextAnchor.MiddleRight));
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

        private static void LabelWithHalo(Rect rect, string text, GUIStyle style)
        {
            var haloStyle = new GUIStyle(style) { normal = { textColor = Halo } };
            GUI.Label(new Rect(rect.x + HaloOffset.x, rect.y + HaloOffset.y, rect.width, rect.height), text, haloStyle);
            GUI.Label(rect, text, style);
        }

        // ── 场景标注带 ─────────────────────────────────────────────────

        /// <summary>带子有多高。渲染器要先拿到它，才知道卡片该从哪儿往下让。</summary>
        public static float MeasureSceneBandHeight(IReadOnlyList<GameNode> notes, float topY)
            => LayoutSceneBand(notes, topY, draw: false);

        public static void DrawSceneBand(IReadOnlyList<GameNode> notes, float topY)
            => LayoutSceneBand(notes, topY, draw: true);

        /// <summary>
        /// 各条按自己需要多宽排，塞不下就换行——不给作者设一个隐形的条数上限。
        /// 贪心装行：一行至少放一条（哪怕它比整行还宽，收到整行为止），之后每多一条
        /// 都要求它按需要的宽度还塞得进去。
        /// </summary>
        private static float LayoutSceneBand(IReadOnlyList<GameNode> notes, float topY, bool draw)
        {
            if (notes == null || notes.Count == 0) return 0f;

            Rect safe = UIScale.SafeArea;
            float left = safe.xMin + SceneBandSideMargin;
            float maxRowW = Mathf.Max(1f, safe.width - SceneBandSideMargin * 2f);

            float y = topY + SceneBandTopPad;
            int i = 0;
            while (i < notes.Count)
            {
                var widths = new List<float>();
                float totalW = 0f;
                while (i + widths.Count < notes.Count)
                {
                    float want = Mathf.Min(NaturalWidth(notes[i + widths.Count]), maxRowW);
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
