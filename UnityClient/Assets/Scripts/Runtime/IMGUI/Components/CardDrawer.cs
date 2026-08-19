#nullable enable
using System.Collections.Generic;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    // 节点卡入口与共享框架。
    //
    // DESIGN.md「节点规范（暗版，场景内）」通用规则：
    //   Ink 填充 + 1px 纸白描边 @ 70–75%（与建筑线同宽，偏纸白暖调）
    //   内侧 3px 加一根 20% 细线（双线，始终是「线」的语言，不是「框」）
    //   直角无圆角；硬投影偏移 (5,6) 纯黑 @ 45–50%，无模糊
    //
    // 交互状态（DESIGN.md）：默认白线 70% / 悬停白线 100%（变亮不变色）/ 选中金描边 /
    // 正在发生金光呼吸 / 禁用降到 35%。
    //
    // 本类只画共享框架与时钟徽章；动作 / 判定卡内容委托 ActionNodeDrawer，
    // 普通 / 地点 / 人物节点内容委托 ContainerNodeDrawer。
    public static class CardDrawer
    {
        public const float ExternalRestBlockerMarkerHeight = 22f;
        public const float ExternalRestBlockerMarkerGap = 6f;
        public const float ExternalRestBlockerMarkerSpace = ExternalRestBlockerMarkerHeight + ExternalRestBlockerMarkerGap;

        // 场景内每张节点卡都需要稳定的点击目标；内容再少也不能退化成难以点中的细条。
        // 需求展签已移除，短动作卡不再需要旧的 150 高空白；仍保留足够的触控目标与
        // 标题/骰位/执行键间距。长副标题的卡高继续完全由内容测量决定。
        public const float MinCardHeight = 132f;

        public struct CardInteraction
        {
            public bool CardClicked;
            public int ClickedSlotIndex;
            public int DroppedSlotIndex;
            public bool ExecuteClicked;
        }

        // ── 入口 ───────────────────────────────────────────────────────

        // 卡片的视觉类别。卡分两族，两族都是「可操作」的东西：
        //   有框（Action / Character）——框起来的「器物」，你在上面操作。
        //   无边悬浮（Location / Common）——漂在图纸上的「一个地方 / 一件事」，你点进去。
        //
        // 第三族「标注」不在这里：它没有底、没有影子、没有框，也不可点，
        // 整个由 AnnotationDrawer 负责——Ink 填充加硬投影正是「可以拿起来」的标志，
        // 所以标注必须连这层框架一起绕开，而不是在这里多一个 CardKind。
        public enum CardKind { Action, Location, Character, Common }

        // 数据驱动分类。内容里并没有「地点 / 人物」标签，真正的「这是个地方」信号是它
        // 锚定在场景 3D 建筑上（projected）；网格里的容器则是地点内部的事件/分组（Common）。
        // 标签留作显式覆盖，未来内容想强制某类时可用。
        //
        // 标注（ResolveType.Note）不在这里分类——它根本不是卡，由 AnnotationDrawer 独立
        // 绘制，调用方必须先用 AnnotationDrawer.IsAnnotation 把它分流出去。
        public static CardKind Classify(GameNode node, bool anchored)
        {
            if (node.HasResolve) return CardKind.Action;
            if (node.Tags.Contains("人物")) return CardKind.Character;
            if (anchored || node.Tags.Contains("地点")) return CardKind.Location;
            return CardKind.Common;
        }

        // 这张卡按自身内容该有多高。所有摆卡的地方（网格瀑布流、世界投射）都必须问它，
        // 不要再各自写「网格一律 190 / 地点 168」这类与内容无关的常量——那正是标题被时钟徽章
        // 压住、副标题被截掉、按钮贴着骰位的根因：内容变了，盒子不跟着变。
        public static float MeasureCardHeight(
            GameNode node, CardKind kind, float cardWidth, IReadOnlyList<ActorSnapshot>? actors)
        {
            if (kind == CardKind.Action)
                return ActionNodeDrawer.RecommendedCardHeight(node, cardWidth, actors);

            float clocksHeight = MeasureClockBadgesHeight(cardWidth, node.Clocks);
            return kind switch
            {
                CardKind.Character => ContainerNodeDrawer.MeasureCharacterHeight(node, cardWidth, clocksHeight),
                CardKind.Location => ContainerNodeDrawer.MeasureLocationHeight(node, cardWidth, clocksHeight),
                _ => ContainerNodeDrawer.MeasureCommonHeight(node, cardWidth, clocksHeight),
            };
        }

        public static CardInteraction DrawCard(
            Rect rect, GameNode node, CardKind kind, bool isHovered, bool isFlipped, bool isFocused,
            List<SlottedResource?>? slotted, List<GameClock> clocks, string backText,
            IMGUIInteractionContext ui, SSNoirGameManager gameManager,
            bool isExecuting = false, float executeProgress = 0f, string executingText = "执行中",
            ActionReport? localRoll = null, int localRollPhase = 0, int localRollDisplayDieValue = 1, float localRollDisplayScale = 1f,
            CardPresentationResidue? residue = null,
            bool isRestBlockerTarget = false,
            bool containsRestBlockerTarget = false,
            bool spaciousAttachments = false)
        {
            var interaction = new CardInteraction { CardClicked = false, ClickedSlotIndex = -1, DroppedSlotIndex = -1, ExecuteClicked = false };
            bool disabled = node.Disabled;

            // 翻转卡（观察线索背面）：正交状态，独立绘制。
            if (isFlipped)
            {
                DrawFlippedCard(rect, node, backText, isHovered && !disabled, ui, ref interaction);
                if (disabled) interaction.CardClicked = false;
                return interaction;
            }

            // 时钟徽章可能换行——先量出它实际占到哪，卡内其余内容（标题/头像/能力栏）
            // 才知道该从哪开始画，不会被压在徽章下面。
            float clocksBottomY = MeasureClockBadgesBottom(rect, clocks);

            // ── 无边悬浮族（地点 / 普通）：Ink + 硬投影，不走框架，整卡可点。
            if (kind == CardKind.Location || kind == CardKind.Common)
            {
                ContainerNodeDrawer.DrawFloating(rect, node, kind == CardKind.Location, isHovered && !disabled, disabled, clocksBottomY);
                DrawClockBadges(rect, clocks);
                DrawRestBlockerMarker(rect, isRestBlockerTarget, containsRestBlockerTarget);
                if (!disabled && ui.WasTapped(rect))
                {
                    interaction.CardClicked = true;
                    Event.current.Use();
                }
                return interaction;
            }

            // ── 有框族（动作 / 人物相册页）：共享卡框架 + 内容。
            bool isCharacter = kind == CardKind.Character;
            Matrix4x4 oldMatrix = GUI.matrix;
            if (isCharacter && isFocused)
            {
                float rotationDeg = (node.Name.GetHashCode() % 2 == 0) ? -2f : 2f;
                GUIUtility.RotateAroundPivot(rotationDeg, rect.center);
            }

            DrawCardFrame(rect, isHovered, isFocused, disabled, isExecuting || localRoll != null);
            DrawClockBadges(rect, clocks);

            if (isCharacter)
            {
                ContainerNodeDrawer.DrawCharacter(rect, node, disabled, clocksBottomY);
                if (!disabled && ui.WasTapped(rect))
                {
                    interaction.CardClicked = true;
                    Event.current.Use();
                }
            }
            else
            {
                ActionNodeDrawer.DrawContent(
                    rect, node, slotted, ui, gameManager,
                    isExecuting, executeProgress, executingText,
                    localRoll, localRollPhase, localRollDisplayDieValue, localRollDisplayScale,
                    residue, clocksBottomY, spaciousAttachments, ref interaction);
            }

            // 重要性是卡片最上层的状态标记，必须在动作内容与结果附件之后绘制。
            DrawRestBlockerMarker(rect, isRestBlockerTarget, containsRestBlockerTarget);

            if (isCharacter && isFocused)
                GUI.matrix = oldMatrix;

            return interaction;
        }

        // 休息阻塞目标属于“正在等待玩家处理”，使用金色而不是失败/高危的印章红：
        // 目标动作呼吸并挂“必须处理”签；路径容器只保留稳定索引，不与目标争夺注意力。
        private static void DrawRestBlockerMarker(Rect rect, bool isTarget, bool containsTarget)
        {
            if (!containsTarget) return;
            DrawExternalRestBlockerMarker(rect, isTarget);
        }

        private static void DrawExternalRestBlockerMarker(Rect rect, bool isTarget)
        {
            const float markerHeight = ExternalRestBlockerMarkerHeight;
            const float markerGap = ExternalRestBlockerMarkerGap;
            const float markerWidth = 84f;

            // 处理标签统一放到卡片上方，避免遮住建筑线稿、时钟和标题。
            float markerX = Mathf.Clamp(rect.x + 10f, 4f, UIScale.VW - markerWidth - 4f);
            float markerY = Mathf.Max(4f, rect.y - markerHeight - markerGap);
            var marker = UIScale.PixelSnap(new Rect(markerX, markerY, markerWidth, markerHeight));

            if (isTarget)
            {
                IMGUIStyles.DrawGoldPulse(rect, baseAlpha: 0.72f, rings: 3, ringStep: 2.5f, speed: 2.2f);
                GUI.color = IMGUIStyles.Gold;
                GUI.DrawTexture(marker, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = new Color(IMGUIStyles.Ink.r, IMGUIStyles.Ink.g, IMGUIStyles.Ink.b, 0.94f);
                GUI.DrawTexture(marker, Texture2D.whiteTexture);
                GUI.color = Color.white;
                IMGUIStyles.DrawOutline(marker, 1.5f, IMGUIStyles.Gold);
            }

            var style = new GUIStyle(IMGUIStyles.StatusLabel)
            {
                fontSize = IMGUIStyles.FontSize(12),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = isTarget ? new Color(0.16f, 0.13f, 0.03f, 1f) : IMGUIStyles.Gold }
            };
            IMGUIStyles.DrawLabel(marker, "必须处理", style);
        }

        // ── 共享卡框架 ─────────────────────────────────────────────────

        // 无边悬浮：Ink 填充 + 硬投影，与地点/普通浮卡一致。去掉默认白边框与内双线，
        // 只保留「金框=选中/当前」「金光=正在发生」两个信号。存在感靠阴影，不靠线。
        private static void DrawCardFrame(Rect rect, bool isHovered, bool isFocused, bool disabled, bool isHappening)
        {
            IMGUIStyles.DrawShadow(rect, new Vector2(7f, 9f), 0.58f);

            GUI.color = IMGUIStyles.Ink;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (disabled)
            {
                // 禁用态不能只靠按钮文字区分。Ink 本身已接近纯黑，再叠黑几乎看不出变化——
                // 换成暖灰洗色（DisabledWash）整张覆盖：卡面会明显发灰发亮，读成"褪色的纸"
                // 而不是"更暗的黑"。侧栏与描边同用这支灰，三处统一成一套禁用态身份。
                GUI.color = new Color(IMGUIStyles.DisabledWash.r, IMGUIStyles.DisabledWash.g, IMGUIStyles.DisabledWash.b, 0.30f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = Color.white;
                IMGUIStyles.DrawOutline(rect, 1f,
                    new Color(IMGUIStyles.DisabledWash.r, IMGUIStyles.DisabledWash.g, IMGUIStyles.DisabledWash.b, 0.75f));
                GUI.color = IMGUIStyles.DisabledWash;
                GUI.DrawTexture(new Rect(rect.x, rect.y, 4f, rect.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            if (isFocused && !disabled)
                IMGUIStyles.DrawOutline(rect, 2f, IMGUIStyles.Gold);

            if (isHappening && !disabled)
                IMGUIStyles.DrawGoldPulse(rect);
        }

        // ── 时钟徽章（卡右上角，居中，超一行自动换行）─────────────────

        private const float ClockBadgeH = 28f;
        private const float ClockBadgeGap = 8f;
        private const float ClockBadgeRowGap = 6f;
        private const float ClockBadgeMinW = 112f;
        // 长标签与多段进度（如「与乔熟悉起来」+ 8 格）需要约 230px；卡片仍会按自身可用
        // 宽度收缩，避免徽章越出窄卡，而不是把标签静默裁掉。
        private const float ClockBadgeMaxW = 240f;
        private const float ClockBadgePadX = 10f;
        private const float ClockLabelValueGap = 8f;
        private const float PieDiameter = 18f;
        private const float PieValueGap = 6f;

        // 三枚以上的节点时钟在聚焦卡上很常见。正常规格会选择换行，信息虽没丢，
        // 但形成的「两枚 + 一枚」会把任务卡的标题区挤成不稳定的两层。紧凑规格只
        // 在所有徽章能完整同排时启用：字号、内边距和段格稍收，而不裁掉标签、分数或段数。
        private const float CompactClockBadgeH = 24f;
        private const float CompactClockBadgeMinW = 80f;
        private const float CompactClockBadgePadX = 6f;
        private const float CompactClockLabelValueGap = 4f;
        private const float CompactSegmentSize = 7f;
        private const float CompactSegmentSpacing = 3f;
        private const float CompactPieDiameter = 16f;
        private const float CompactPieValueGap = 4f;

        // 量出徽章会占到哪一行的哪个 Y——只做计算不画，供上层在画标题/头像前先留够空间。
        private static float MeasureClockBadgesBottom(Rect rect, List<GameClock>? clocks)
            => LayoutClockBadges(rect, clocks, draw: false);

        public static float MeasureClockBadgesHeight(float cardWidth, List<GameClock>? clocks)
        {
            if (clocks == null || clocks.Count == 0)
                return 0f;
            var measureRect = new Rect(0f, 0f, cardWidth, 1f);
            return LayoutClockBadges(measureRect, clocks, draw: false);
        }

        private static void DrawClockBadges(Rect rect, List<GameClock>? clocks)
            => LayoutClockBadges(rect, clocks, draw: true);

        // 量和画共用同一套换行逻辑——避免像旧版那样「预留的位置」和「实际画的位置」各算一遍、
        // 改一处忘了改另一处。
        //
        // 排法是「按各自真正需要多宽，能塞下就塞，塞不下就换行」。
        // 以前是先用固定的最小宽度 112 反推一行放几个，再把这一行的宽度均分下去：手机上卡宽
        // 约 380，于是三个时钟被判成"一行放得下"，每个只分到 115——而「交割款 45/100」需要
        // 约 146，标签就被裁成了「交」。一行放几个必须由内容说了算，不能由一个猜出来的下限
        // 说了算。
        private static float LayoutClockBadges(Rect rect, List<GameClock>? clocks, bool draw)
        {
            if (clocks == null || clocks.Count == 0) return rect.y;

            float maxRowW = Mathf.Max(0f, rect.width - 24f);
            bool compactSingleRow = clocks.Count >= 3 && FitsCompactSingleRow(clocks, maxRowW);

            float y = rect.y + 8f;
            int i = 0;
            while (i < clocks.Count)
            {
                // 贪心装一行：至少放一个（哪怕它比整行还宽，收窄到整行为止），
                // 之后每多一个都要求它按需要的宽度还塞得进去。
                var widths = new List<float>();
                float totalW = 0f;
                while (i + widths.Count < clocks.Count)
                {
                    bool compact = compactSingleRow;
                    float want = Mathf.Clamp(
                        MeasureNodeClockBadgeWidth(clocks[i + widths.Count], compact),
                        Mathf.Min(compact ? CompactClockBadgeMinW : ClockBadgeMinW, maxRowW),
                        compact ? maxRowW : ClockBadgeMaxW);
                    float withGap = widths.Count == 0 ? want : totalW + ClockBadgeGap + want;
                    if (widths.Count > 0 && withGap > maxRowW)
                        break;

                    widths.Add(Mathf.Min(want, maxRowW));
                    totalW = widths.Count == 1 ? widths[0] : withGap;
                }

                if (draw)
                {
                    float x = rect.x + (rect.width - totalW) * 0.5f;
                    for (int j = 0; j < widths.Count; j++)
                    {
                        float badgeH = compactSingleRow ? CompactClockBadgeH : ClockBadgeH;
                        DrawNodeClockBadge(new Rect(x, y, widths[j], badgeH), clocks[i + j], compactSingleRow);
                        x += widths[j] + ClockBadgeGap;
                    }
                }

                y += (compactSingleRow ? CompactClockBadgeH : ClockBadgeH) + ClockBadgeRowGap;
                i += widths.Count;
            }

            return y - ClockBadgeRowGap;
        }

        private static bool FitsCompactSingleRow(IReadOnlyList<GameClock> clocks, float maxRowW)
        {
            float totalW = ClockBadgeGap * (clocks.Count - 1);
            foreach (var clock in clocks)
                totalW += MeasureNodeClockBadgeWidth(clock, compact: true);
            return totalW <= maxRowW;
        }

        private static float MeasureNodeClockBadgeWidth(GameClock clock, bool compact = false)
        {
            var labelStyle = new GUIStyle(IMGUIStyles.ClockLabel)
            {
                fontSize = IMGUIStyles.FontSize(compact ? 12 : 14),
                fontStyle = FontStyle.Bold
            };
            IMGUIStyles.ApplyStrongFont(labelStyle);

            float labelW = labelStyle.CalcSize(new GUIContent(clock.Label)).x;
            float padX = compact ? CompactClockBadgePadX : ClockBadgePadX;
            float gap = compact ? CompactClockLabelValueGap : ClockLabelValueGap;
            return padX * 2f + labelW + gap + MeasureNodeClockValueWidth(clock, compact);
        }

        private static void DrawNodeClockBadge(Rect rect, GameClock clock, bool compact)
        {
            Color activeColor = IMGUIStyles.Gold;
            Color inactiveColor = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.25f);
            Color outline = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.55f);

            GUI.color = new Color(IMGUIStyles.Ink.r, IMGUIStyles.Ink.g, IMGUIStyles.Ink.b, 0.96f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(rect, 1f, outline);

            var labelStyle = new GUIStyle(IMGUIStyles.ClockLabel)
            {
                fontSize = IMGUIStyles.FontSize(compact ? 12 : 14),
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };
            IMGUIStyles.ApplyStrongFont(labelStyle);

            // 徽章会随卡片收窄。不能只收窄外框、继续以固定的 valueW 画内容：这样长分数
            // （如 74/100）与饼图会从框内探出去。值优先占据可用空间，标签再使用剩余部分；
            // 两个 Label 都显式 Clip，作为数值或卡片异常窄时的最后一道边界。
            float padX = compact ? CompactClockBadgePadX : ClockBadgePadX;
            float labelValueGap = compact ? CompactClockLabelValueGap : ClockLabelValueGap;
            float innerW = Mathf.Max(0f, rect.width - padX * 2f);
            float desiredValueW = MeasureNodeClockValueWidth(clock, compact);
            float valueW = Mathf.Min(desiredValueW, innerW);
            float gap = valueW > 0f && !string.IsNullOrEmpty(clock.Label) ? labelValueGap : 0f;
            float labelW = Mathf.Min(
                labelStyle.CalcSize(new GUIContent(clock.Label)).x,
                Mathf.Max(0f, innerW - valueW - gap));

            var valueRect = new Rect(rect.xMax - padX - valueW, rect.y, valueW, rect.height);
            IMGUIStyles.DrawLabel(new Rect(rect.x + padX, rect.y, labelW, rect.height), clock.Label, labelStyle);

            if (clock.Style == ClockStyle.Countdown)
            {
                var valueStyle = new GUIStyle(IMGUIStyles.ClockValue)
                {
                    fontSize = IMGUIStyles.FontSize(compact ? 13 : 15),
                    alignment = TextAnchor.MiddleRight,
                    clipping = TextClipping.Clip,
                    normal = { textColor = IMGUIStyles.Gold }
                };
                IMGUIStyles.DrawLabel(valueRect, $"{clock.Current}/{clock.Max}", valueStyle);
            }
            else if (clock.Style == ClockStyle.Segments)
            {
                float dot = compact ? CompactSegmentSize : 10f;
                float spacing = compact ? CompactSegmentSpacing : 5f;
                float dotStartX = valueRect.x;
                float dotY = rect.y + (rect.height - dot) * 0.5f;
                int visibleCount = Mathf.Min(clock.Max, Mathf.Max(0, Mathf.FloorToInt((valueW + spacing) / (dot + spacing))));
                for (int i = 0; i < visibleCount; i++)
                {
                    var dotRect = new Rect(dotStartX + i * (dot + spacing), dotY, dot, dot);
                    if (i < clock.Current)
                    {
                        GUI.color = activeColor;
                        GUI.DrawTexture(dotRect, Texture2D.whiteTexture);
                        GUI.color = Color.white;
                    }
                    else
                    {
                        GUI.color = inactiveColor;
                        GUI.DrawTexture(dotRect, Texture2D.whiteTexture);
                        GUI.color = Color.white;
                        IMGUIStyles.DrawOutline(dotRect, 1f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f));
                    }
                    GUI.color = Color.white;
                }
            }
            else // Pie
            {
                // 先给圆形分配实际放得下的直径，再让分数占剩余宽度；两者都不会越过 valueRect。
                float pieDiameter = compact ? CompactPieDiameter : PieDiameter;
                float pieValueGap = compact ? CompactPieValueGap : PieValueGap;
                float pieSize = Mathf.Min(pieDiameter, valueW);
                float fractionGap = pieSize > 0f && valueW > pieSize ? Mathf.Min(pieValueGap, valueW - pieSize) : 0f;
                var pieRect = new Rect(valueRect.x, rect.center.y - pieSize * 0.5f, pieSize, pieSize);
                float fillPct = clock.Max > 0 ? Mathf.Clamp01((float)clock.Current / clock.Max) : 0f;
                if (pieSize >= 4f)
                    PieDrawer.DrawPieBadge(pieRect, fillPct, activeColor, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.70f));

                var fracStyle = new GUIStyle(IMGUIStyles.ClockValue)
                {
                    fontSize = IMGUIStyles.FontSize(compact ? 12 : 14),
                    alignment = TextAnchor.MiddleRight,
                    clipping = TextClipping.Clip,
                    normal = { textColor = IMGUIStyles.Gold }
                };
                IMGUIStyles.DrawLabel(new Rect(pieRect.xMax + fractionGap, rect.y,
                    Mathf.Max(0f, valueRect.xMax - pieRect.xMax - fractionGap), rect.height),
                    $"{clock.Current}/{clock.Max}", fracStyle);
            }
        }

        private static float MeasureNodeClockValueWidth(GameClock clock, bool compact = false)
        {
            string fraction = $"{clock.Current}/{clock.Max}";
            return clock.Style switch
            {
                ClockStyle.Countdown => MeasureClockValueTextWidth(fraction, compact ? 13 : 15),
                ClockStyle.Segments => compact
                    ? Mathf.Max(0f, clock.Max * CompactSegmentSize + Mathf.Max(0, clock.Max - 1) * CompactSegmentSpacing)
                    : Mathf.Max(0f, clock.Max * 10f + Mathf.Max(0, clock.Max - 1) * 5f),
                ClockStyle.Pie => (compact ? CompactPieDiameter : PieDiameter)
                    + (compact ? CompactPieValueGap : PieValueGap)
                    + MeasureClockValueTextWidth(fraction, compact ? 12 : 14),
                _ => MeasureClockValueTextWidth(fraction, compact ? 13 : 15)
            };
        }

        private static float MeasureClockValueTextWidth(string text, int fontSize)
        {
            var style = new GUIStyle(IMGUIStyles.ClockValue)
            {
                fontSize = IMGUIStyles.FontSize(fontSize),
                fontStyle = FontStyle.Bold
            };
            IMGUIStyles.ApplyStrongFont(style);
            return style.CalcSize(new GUIContent(text)).x;
        }

        // ── 翻转卡（观察线索背面）──────────────────────────────────────

        // Ink 底 + 1px 纸白描边（70% 默认 / 100% 悬停）+ 标题 + 「已解读线索」+ 线索正文 + 返回提示。
        private static void DrawFlippedCard(Rect rect, GameNode node, string backText, bool isHovered, IMGUIInteractionContext ui,
            ref CardInteraction interaction)
        {
            Color outline = isHovered
                ? new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 1f)
                : new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.70f);
            float thickness = isHovered ? 2f : 1f;

            IMGUIStyles.DrawShadow(rect, new Vector2(4f, 4f), 0.45f);
            GUI.color = IMGUIStyles.Ink;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(rect, thickness, outline);

            IMGUIStyles.DrawLabel(new Rect(rect.x + 8, rect.y + 8, rect.width - 16, 20), node.Name, IMGUIStyles.FlippedTitle);
            IMGUIStyles.DrawLabel(new Rect(rect.x + 8, rect.y + 28, rect.width - 16, 16), "— 已解读线索 —", IMGUIStyles.FlippedTip);

            var contentRect = new Rect(rect.x + 8, rect.y + 48, rect.width - 16, rect.height - 68);
            string clueText = node.Resolve != null ? node.Resolve.ObserveText : "";
            IMGUIStyles.DrawLabel(contentRect, clueText, IMGUIStyles.FlippedContent);

            IMGUIStyles.DrawLabel(new Rect(rect.x + 8, rect.y + rect.height - 18, rect.width - 16, 14), "点击返回", IMGUIStyles.FlippedTip);

            if (ui.WasTapped(rect))
            {
                interaction.CardClicked = true;
                Event.current.Use();
            }
        }

        public static void DrawEffectRows(
            Rect area,
            IReadOnlyList<ActionEffectRecord> effects,
            float rowHeight = 14f,
            float rowGap = 16f,
            int fontSize = 10)
        {
            if (effects.Count == 0 || area.height <= 0f)
                return;

            int maxRows = Mathf.Max(0, Mathf.FloorToInt(area.height / rowGap));
            if (maxRows == 0)
            {
                // 连一整行都放不下时也不能直接 return：影响行是结算的核心信息，什么都不画等于
                // 告诉玩家「这次行动什么都没发生」。挤出一条「+N」提示，把「有内容、但这里没
                // 地方显示」明说出来（AGENTS.md：信息不能被悄悄藏起来）。
                if (area.height >= 8f)
                    DrawEffectMoreRow(new Rect(area.x, area.y, area.width, area.height), effects.Count, fontSize);
                return;
            }

            int visibleCount = effects.Count <= maxRows ? effects.Count : maxRows - 1;
            for (int i = 0; i < visibleCount; i++)
                DrawSingleEffectRow(effects[i], new Rect(area.x, area.y + i * rowGap, area.width, rowHeight), fontSize);

            if (effects.Count > maxRows)
                DrawEffectMoreRow(
                    new Rect(area.x, area.y + visibleCount * rowGap, area.width, rowHeight),
                    effects.Count - visibleCount,
                    fontSize);
        }

        // 「还有 N 项影响」折叠提示。行高不固定——高度紧张时它会被压到不足一行，
        // 但只要还有 ≥8px 就必须画出来，这是「信息不被悄悄藏起来」的最后一道兜底。
        private static void DrawEffectMoreRow(Rect row, int hiddenCount, int fontSize)
        {
            Color accent = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.50f);
            DrawEffectRowBg(row, accent);

            var style = new GUIStyle(IMGUIStyles.ModalBody)
            {
                fontSize = IMGUIStyles.FontSize(fontSize),
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = IMGUIStyles.TextSecondary }
            };
            IMGUIStyles.DrawLabel(new Rect(row.x + 8f, row.y, row.width - 16f, row.height), $"+ 还有 {hiddenCount} 项影响...", style);
        }

        private static void DrawSingleEffectRow(ActionEffectRecord effect, Rect row, int fontSize)
        {
            Color accent = effect.Tone switch
            {
                ActionEffectTone.Positive => IMGUIStyles.OutcomeSuccess,
                ActionEffectTone.Negative => IMGUIStyles.OutcomeFail,
                _ => IMGUIStyles.TextSecondary
            };
            DrawEffectRowBg(row, accent);

            var labelStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                fontSize = IMGUIStyles.FontSize(fontSize),
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = IMGUIStyles.TextPrimary }
            };

            if (effect.Kind == ActionEffectKind.Note)
            {
                IMGUIStyles.DrawLabel(new Rect(row.x + 8f, row.y, row.width - 16f, row.height), effect.Text, labelStyle);
                return;
            }

            IMGUIStyles.DrawLabel(new Rect(row.x + 8f, row.y, row.width - 56f, row.height), effect.Label, labelStyle);

            string value = effect.Delta.HasValue
                ? (effect.Delta.Value > 0 ? $"+{effect.Delta.Value}" : effect.Delta.Value.ToString())
                : string.Empty;
            var valueStyle = new GUIStyle(labelStyle)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = accent }
            };
            IMGUIStyles.ApplyStrongFont(valueStyle);
            IMGUIStyles.DrawLabel(new Rect(row.x + row.width - 52f, row.y, 44f, row.height), value, valueStyle);
        }

        private static void DrawEffectRowBg(Rect row, Color accent)
        {
            GUI.color = new Color(accent.r, accent.g, accent.b, 0.16f);
            GUI.DrawTexture(row, Texture2D.whiteTexture);
            GUI.color = accent;
            GUI.DrawTexture(new Rect(row.x, row.y, 3f, row.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

    }
}
