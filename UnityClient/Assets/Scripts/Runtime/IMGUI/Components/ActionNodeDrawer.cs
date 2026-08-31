#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    // 动作 / 判定卡的内容绘制。共享卡框架由 CardDrawer.DrawCard 在调用前画好。
    //
    // DESIGN.md「动作 / 判定卡」布局分区固定：
    //   标题居中 → 左侧类别/风险便签 → 中部判定纵列（属性药丸 → 骰子格）
    //   → 右侧骰位（白框标签 + 白底黑字数值格连体）→ 执行按钮（实心金）
    //   → 底部命运六面预览。
    // 掷骰动画与结算结果是卡片下挂附件，不占卡片主体内部空间。
    //
    // 负片规则（DESIGN.md）：骰子/数值格在暗卡上反转为白底黑字——「暗卡上的白纸片」。
    public static class ActionNodeDrawer
    {
        // ── 入口 ───────────────────────────────────────────────────────

        public static void DrawContent(
            Rect rect,
            GameNode node,
            List<SlottedResource?>? slotted,
            IMGUIInteractionContext ui,
            SSNoirGameManager gameManager,
            bool isExecuting,
            float executeProgress,
            string executingText,
            ActionReport? localRoll,
            int localRollPhase,
            int localRollDisplayDieValue,
            float localRollDisplayScale,
            CardPresentationResidue? residue,
            float clockBadgesBottom,
            bool spaciousAttachments,
            ref CardDrawer.CardInteraction interaction)
        {
            bool disabled = node.Disabled;
            bool isRoll = node.Resolve!.Type == ResolveType.Roll;
            int requiredSlotCount = node.Requires?.Count ?? 0;
            bool hasRequires = requiredSlotCount > 0 && slotted != null && slotted.Count == requiredSlotCount;
            bool isObserve = node.Resolve!.Type == ResolveType.Observe;
            var actors = gameManager.DisplayedSnapshot.Actors;
            bool showOdds = isRoll && !disabled && hasRequires && actors != null && localRoll == null && residue == null;
            // 伤势修正由 SceneManager 在结算时才挂上，内容层的 DifficultyModifiers 里没有它。
            // 卡面必须自己补一份，否则「脸伤 · 交际 −2」在预览里看不见，赔率条也会按未受伤算——
            // 预览写 4、结算按 2 算的两套账。这是同一份数据的两个读取点，不是两条规则。
            var effectiveModifiers = EffectiveModifiers(node, gameManager.DisplayedSnapshot);
            bool hasClocks = clockBadgesBottom > rect.y;

            // ── 1. 标题区（居中）── clockBadgesBottom 是 CardDrawer 量出的时钟徽章实际底部，
            // 徽章换行也不会被标题压住（旧版固定 44f 只够单行徽章用）。
            // 徽章多到几乎吃满卡高时（如同一节点同时挂 5 个时钟）也不能任由标题被推出卡底——
            // 宁可让标题少量压在徽章区之上，也不让文字画到卡外面去。
            float topWidgetsBottom = hasClocks ? clockBadgesBottom : rect.y;
            float titleY = Mathf.Min(TitleTop(topWidgetsBottom, rect.y), rect.yMax - 32f);
            if (titleY + TitleH <= rect.yMax)
            {
                var titleStyle = new GUIStyle(IMGUIStyles.CardTitle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip
                };
                if (disabled) titleStyle.normal.textColor = IMGUIStyles.TextSecondary;
                IMGUIStyles.DrawLabel(new Rect(rect.x + 10f, titleY, rect.width - 20f, TitleH), node.Name, titleStyle);
            }

            // ── 2. 三列分区的纵向边界 ──
            // 执行按钮贴底；showOdds 时再往下留出命运六面预览的空间。
            // 骰位区要先于副标题定下预算，所以 exeY 提到副标题之前算。
            float exeH = 26f;
            float exeY = rect.yMax - (showOdds ? 62f : 34f);

            // 副标题按 MeasureSubtitleHeight 占它真正需要的高度，卡片高度也是按同一个函数算出来的，
            // 所以正常情况下它不需要跟谁抢空间。下面的 budget 只是兜底：卡被外部钉成更矮的尺寸时，
            // 让位的必须是气氛文本，而不是骰位——骰位是这张卡的可交互核心（DESIGN.md「属性 → 骰子」）。
            float subtitleBottomY = titleY + TitleH;
            if (!string.IsNullOrWhiteSpace(node.Subtitle))
            {
                var subtitleStyle = SubtitleStyle();
                if (disabled) subtitleStyle.normal.textColor = IMGUIStyles.TextSecondary;
                float subtitleWidth = rect.width - 24f;
                string subtitle = WrapSubtitle(node.Subtitle, subtitleStyle, subtitleWidth);
                float subtitleHeight = MeasureSubtitleHeight(subtitle, subtitleStyle, subtitleWidth);

                // 卡高由 RecommendedCardHeight 按同一套流量算出来，正常情况下这里放得下。
                // 兜底仍然保留：卡被外部钉死成更矮的尺寸时，让位的必须是气氛文本而不是骰位。
                float subtitleBudget = exeY - (titleY + TitleH + GapTitleToBody) - RequirementSlotsMinNeed(node);
                if (subtitleHeight > subtitleBudget)
                    subtitleHeight = subtitleBudget >= 18f ? subtitleBudget : 0f;

                if (subtitleHeight > 0f && titleY + TitleH + subtitleHeight <= rect.yMax)
                    IMGUIStyles.DrawLabel(new Rect(rect.x + 12f, titleY + TitleH, subtitleWidth, subtitleHeight), subtitle, subtitleStyle);
                subtitleBottomY = titleY + TitleH + subtitleHeight;
            }

            float bodyY = subtitleBottomY + GapTitleToBody;

            // ── 3. 类别 / 风险便签：骑在卡片左边缘外（不遮内容、不占内部空间）。
            // 左便签只说「这件事」：内容层写的难度修正 + 类别/风险标签。
            // 伤势/旧伤是「这个人」的事，挂在右缘的人物能力片上（尼尔 0 −1）。
            {
                // 两边的外挂都伸到卡外，所以谁盖谁只由绘制顺序决定；网格里这个顺序会把
                // 右缘的能力片吃掉（见 BeginDeferredEdgeAttachments 的注释）。这里统一收成
                // 一件事交出去，由排版的那一头决定是当场画还是等所有卡身画完再画。
                var attachRect = rect;
                var attachNode = node;
                var mods = node.Resolve!.DifficultyModifiers;
                bool attachDisabled = disabled;
                string skill = node.Resolve!.SkillName;
                var attachActors = isRoll ? actors : null;
                var attachSnapshot = gameManager.DisplayedSnapshot;
                DrawOrDeferEdgeAttachments(() =>
                {
                    DrawEdgeTags(attachRect, attachNode, mods, attachDisabled);
                    // 能力预览与左侧标签同属 card attachment：它描述这张卡，但不该占主体的垂直预算。
                    if (attachActors != null && attachActors.Count > 0)
                        DrawActorAbilityRail(attachRect, skill, attachActors, attachSnapshot);
                });
            }

            // ── 4. 需求骰位：统一为「方块 Slot」（与手牌骰子/物品同族），居中横排。
            //     骰子 slot = 大字 D/值；物品 slot = 符号 + 数量（强调）+ 下方名称展签。
            //     判定的核心骰上方挂技能药丸（属性 → 骰子）。
            DrawRequirementSlots(rect, bodyY, exeY, clockBadgesBottom, node, slotted, disabled, ui, gameManager, ref interaction);

            // ── 6. 执行 / 查看按钮（实心金 / 待命 / 不可用 / 执行中）──
            var exeRect = new Rect(rect.x + (rect.width - 112f) / 2f, exeY, 112f, exeH);
            bool allFilled = requiredSlotCount == 0 || (slotted != null && slotted.Count == requiredSlotCount && slotted.All(s => s != null));
            if (isExecuting)
            {
                DrawExecuteProgress(exeRect, executeProgress, executingText);
            }
            else if (isObserve)
            {
                if (DrawExecuteButton(exeRect, disabled ? "不可用" : "查 看", ui, !disabled, dead: disabled))
                {
                    interaction.CardClicked = true;
                }
            }
            else if (allFilled && !disabled)
            {
                if (DrawExecuteButton(exeRect, "执 行", ui, true))
                    interaction.ExecuteClicked = true;
            }
            else
            {
                DrawExecuteButton(exeRect, disabled ? "不可用" : "待 命", ui, false, dead: disabled);
            }

            // ── 7. 命运预览留在主体卡；判定中与结算结果由渲染器在全部卡片之后统一画到
            // attachment overlay，避免被较近的世界卡压住。──
            if (showOdds)
            {
                TryDrawFatePreview(rect, node, gameManager.DisplayedSnapshot, slotted!, actors!, exeRect.yMax);
            }

            // ── 8. 卡片级点击（仅无 requires 的非 Instant 类型，如 Observe / Clock）──
            if (!disabled && ui.WasTapped(rect) && !hasRequires && node.Resolve!.Type != ResolveType.Instant)
            {
                interaction.CardClicked = true;
                Event.current.Use();
            }
        }

        // ── 外挂的两遍绘制 ───────────────────────────────────────────
        //
        // 便签（左缘）与能力片（右缘）都伸到卡片外面：便签最宽 172、能力片 78，而网格的
        // 列间距只有 20。也就是说两边都会伸进邻居的地盘——但结果并不对称：网格按序号
        // 从左往右画，一张卡的左便签伸进的是**已经画完**的那一列（便签盖在上面，赢），
        // 右能力片伸进的是**还没画**的那一列（下一张卡的卡身随后盖上来，输）。
        // 于是同一套外挂，一边永远看得见，一边永远被吃掉。
        //
        // 收拾办法是把外挂挪到第二遍：所有卡身画完再统一画。两边就都在最上层了。
        //
        // <b>只有网格这么做。</b>空间投射的卡是按远→近画的，近的卡盖住远的卡的外挂是
        // **正确的**深度关系，把它掀到最上层反而是错的。网格没有深度可言，才轮得到这一招。
        private static bool _deferEdgeAttachments;
        private static readonly List<System.Action> _deferredEdgeAttachments = new List<System.Action>();

        public static void BeginDeferredEdgeAttachments()
        {
            // 上一轮要是没走到 Flush（画卡途中抛了），这里直接丢掉残留：攒着的是上一帧的
            // 坐标，画出来只会是鬼影。宁可少一帧外挂，也不能让标志位卡在"永远在攒"。
            _deferredEdgeAttachments.Clear();
            _deferEdgeAttachments = true;
        }

        /// <summary>把攒下的外挂一次画完。必须和 Begin 在同一个坐标系/分组里调用。</summary>
        public static void FlushDeferredEdgeAttachments()
        {
            _deferEdgeAttachments = false;
            for (int i = 0; i < _deferredEdgeAttachments.Count; i++)
                _deferredEdgeAttachments[i]();
            _deferredEdgeAttachments.Clear();
        }

        private static void DrawOrDeferEdgeAttachments(System.Action draw)
        {
            if (_deferEdgeAttachments)
                _deferredEdgeAttachments.Add(draw);
            else
                draw();
        }

        // ── 边缘便签：类别 / 风险，骑在卡片左边缘外 ────────────────────

        // 同族彩纸便签，横贴在卡左边缘外，文字正常横排；自上而下堆叠。
        // 不占卡内空间，也不遮主要元素——「贴在物件上的彩色便签」隐喻更足。
        // 纸底一律浅色、字一律同族深色（DESIGN.md 便签色板）：便签压在深色卡与深色场景之上，
        // 只有浅纸深字这一种关系才在两种底上都读得清。
        // 空间投影卡的边缘有独立的纵向空间，便签全部逐张展示，不再折叠成「+N」。
        private const float NotePadding = 10f;
        private const float NoteMaxWidth = 172f;
        // 便签压进卡内的宽度，其余露在卡外。这一段是「贴上去」的那一头：胶带画在这儿，
        // 横跨卡边缘，把便签钉在它自己那张卡上。
        private const float NoteOverlap = 18f;
        // 左端是撕口：锯齿的进深与齿高。归属感靠形状说话——右端贴死、左端撕开，
        // 一张纸只可能是从右边那张卡上撕下来贴住的。
        private const float NoteTornDepth = 5f;
        private const float NoteTornTooth = 7f;
        // 纸上除了字以外必然被占掉的横向宽度：左右内边距 + 撕口进深 + 被胶带压住的那一段。
        // 量高度和定宽度必须用同一个数，否则又是「盒子长高了、文字没跟上」那类 bug。
        private const float NoteTextChromeWidth = NotePadding * 2f + NoteTornDepth + NoteOverlap;
        // 文字区不许窄过这个数：低于两个字宽，中文就开始一行一个字竖着排。
        private const float NoteMinTextWidth = 44f;
        private const float NoteScreenPad = 4f;

        // 便签上是卡面最小的字。窗口不足 1080 高时 GUI.matrix 会整体压到 0.75 / 0.5，固定字号
        // 落到物理屏上就只剩几个像素，中文必糊。这里按 Scale 反推出「物理屏上至少 12px」需要的
        // 虚拟字号——只有便签敢这么做：它的纸高是从字号推出来的，字变大盒子跟着变大，不会撑爆
        // 别处那种固定高度的小格子。
        private static int NoteFontSize()
        {
            int wanted = Mathf.Clamp(Mathf.CeilToInt(12f / Mathf.Max(0.5f, UIScale.Scale)), 15, 24);
            return IMGUIStyles.FontSize(wanted);
        }

        // 卡面看见的修正 = 内容层写的 + 主角当前的伤势 + 身上的旧伤。判定用的是同一份 Injury 数据
        // （SceneManager 结算时走 Injury.ModifierFor），这里只是把它提前显示出来。
        private static List<DifficultyModifierInfo> EffectiveModifiers(
            GameNode node, PresentationSnapshot snapshot, string actorRole = "protagonist")
        {
            var baseMods = node.Resolve!.DifficultyModifiers;
            if (node.Resolve.Type != ResolveType.Roll) return baseMods;
            // 伤势只压主角：同伴出的骰子不吃这一笔，预览也不能画上去。
            if (actorRole != "protagonist") return baseMods;
            bool hasInjury = snapshot.InjurySkillPenalty != 0
                && string.Equals(snapshot.InjurySkillKey, node.Resolve.SkillName,
                                 System.StringComparison.OrdinalIgnoreCase);
            snapshot.ScarModifiers.TryGetValue(node.Resolve.SkillName, out var scarMod);
            if (!hasInjury && scarMod == null) return baseMods;

            var merged = new List<DifficultyModifierInfo>(baseMods);
            if (hasInjury)
            {
                merged.Add(new DifficultyModifierInfo
                {
                    Value = snapshot.InjurySkillPenalty,
                    Reason = snapshot.InjuryPart + "伤",
                });
            }
            if (scarMod != null) merged.Add(scarMod);
            return merged;
        }

        private static void DrawEdgeTags(Rect rect, GameNode node, List<DifficultyModifierInfo> effectiveModifiers, bool disabled)
        {
            int fontSize = NoteFontSize();
            float y = rect.y + 22f;
            var noteStyle = new GUIStyle(GUI.skin.label)
            {
                font = IMGUIStyles.ChineseFont,
                fontSize = fontSize,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                wordWrap = true
            };
            IMGUIStyles.ApplyStrongFont(noteStyle);

            var items = new List<(string text, Color bg, Color textColor)>();
            foreach (var mod in effectiveModifiers)
            {
                string modText = $"{mod.Reason} {(mod.Value > 0 ? "+" : "")}{mod.Value}";
                var (modBg, modInk) = disabled
                    ? NoteMuted
                    : mod.Value < 0 ? NoteHighRisk : (mod.Value > 0 ? NoteWork : NoteNegotiate);
                items.Add((modText, modBg, modInk));
            }
            if (node.Tags != null)
            {
                foreach (var tag in node.Tags)
                {
                    if (string.IsNullOrWhiteSpace(tag)) continue;
                    var (bg, text) = TagColors(tag, disabled);
                    items.Add((tag, bg, text));
                }
            }

            // 每张便签按实际可用宽度量高度，长中文自动换行；空间投影允许标签向下延展，
            // 因此不再因为卡片高度而截断或折叠。
            foreach (var item in items)
            {
                float noteHeight = MeasureEdgeNoteHeight(rect, item.text, noteStyle);
                DrawEdgeNote(rect, y, noteHeight, item.text, item.bg, item.textColor, noteStyle);
                y += noteHeight + 6f;
            }
        }

        // 一张便签：左端撕口 + 浅彩纸 + 三边描边 + 深色字 + 右端一段胶带横跨卡边缘。
        // 右端压进卡片，向左伸出。
        // 整块按物理像素对齐（PixelSnap）：非整数缩放下不对齐会让纸边和字一起发虚。
        private static void DrawEdgeNote(Rect card, float noteY, float noteHeight, string text, Color bg, Color ink, GUIStyle baseStyle)
        {
            var style = new GUIStyle(baseStyle) { normal = { textColor = ink } };
            style.hover.textColor = ink;
            style.active.textColor = ink;
            style.focused.textColor = ink;
            style.onNormal.textColor = ink;
            style.onHover.textColor = ink;
            style.onActive.textColor = ink;
            style.onFocused.textColor = ink;

            var span = NoteSpan(card, text, style);
            var note = UIScale.PixelSnap(new Rect(span.x, noteY, span.y, noteHeight));

            // 纸身从撕口进深之后才开始；左边那一段由锯齿补齐，于是左端不是切齐的矩形边。
            var body = new Rect(note.x + NoteTornDepth, note.y,
                Mathf.Max(1f, note.width - NoteTornDepth), note.height);

            IMGUIStyles.DrawShadow(body, new Vector2(2f, 3f), 0.55f);
            IMGUIStyles.SetColor(bg);
            GUI.DrawTexture(body, Texture2D.whiteTexture);
            IMGUIStyles.ResetColor();

            DrawTornLeftEdge(body, bg);

            // 描边只画三条切齐的边（上、下、右）——左边是撕口，撕口不描边。
            var edge = new Color(ink.r, ink.g, ink.b, 0.65f);
            IMGUIStyles.SetColor(edge);
            GUI.DrawTexture(new Rect(body.x, body.y, body.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(body.x, body.yMax - 1f, body.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(body.xMax - 1f, body.y, 1f, body.height), Texture2D.whiteTexture);
            IMGUIStyles.ResetColor();

            DrawNoteTape(card, body, ink);

            IMGUIStyles.DrawLabel(
                new Rect(body.x + NotePadding, body.y,
                    Mathf.Max(1f, body.width - NotePadding * 2f - NoteOverlap), body.height),
                text, style);
        }

        // 撕口：沿左边缘排一列朝左的小三角，用纸的本色画——纸被撕下来的那一侧不是一条直边。
        // 齿数按纸高算，最后一颗不许探出纸外（探出去就成了「纸下面还有东西」）。
        private static void DrawTornLeftEdge(Rect body, Color bg)
        {
            int teeth = Mathf.Max(2, Mathf.FloorToInt(body.height / NoteTornTooth));
            float toothH = body.height / teeth;
            for (int i = 0; i < teeth; i++)
            {
                float top = body.y + i * toothH;
                ShapeDrawer.DrawTriangle(
                    new Vector2(body.x, top),
                    new Vector2(body.x, top + toothH),
                    new Vector2(body.x - NoteTornDepth, top + toothH * 0.5f),
                    bg);
            }
        }

        // 胶带：横跨卡片左边缘的一小段半透明纸白，压在便签和卡片**两边**上。
        // 归属感就靠它——被粘住的是卡边，那这张纸只可能是这张卡的。
        private static void DrawNoteTape(Rect card, Rect body, Color ink)
        {
            float tapeH = Mathf.Min(14f, body.height * 0.5f);
            var tape = UIScale.PixelSnap(new Rect(
                card.x - 10f, body.center.y - tapeH * 0.5f,
                Mathf.Max(12f, NoteOverlap + 14f), tapeH));

            IMGUIStyles.SetColor(new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.45f));
            GUI.DrawTexture(tape, Texture2D.whiteTexture);
            // 两条端线：胶带的边界要看得出来，否则只是一块发白的脏斑。
            IMGUIStyles.SetColor(new Color(ink.r, ink.g, ink.b, 0.30f));
            GUI.DrawTexture(new Rect(tape.x, tape.y, 1f, tape.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(tape.xMax - 1f, tape.y, 1f, tape.height), Texture2D.whiteTexture);
            IMGUIStyles.ResetColor();
        }

        // 便签的左端和宽度。量高和绘制共用这一份，两边各算一遍必然长出「盒子和文字对不上」。
        //
        // 关键是**挤窄时挤的是位置，不是文字**：贴屏幕左边的卡（网格首列）左边没地方了，
        // 就让便签整体往卡内多压一点，而不是把纸压成一条缝、文字一行一个字竖着排下去。
        // 压进卡内最多到卡宽的一半——再多就不像贴在边上的东西了。
        private static Vector2 NoteSpan(Rect card, string text, GUIStyle style)
        {
            var singleLineStyle = new GUIStyle(style) { wordWrap = false };
            float width = Mathf.Clamp(
                NoteTextChromeWidth + singleLineStyle.CalcSize(new GUIContent(text)).x,
                NoteTextChromeWidth + NoteMinTextWidth,
                NoteMaxWidth);

            float right = card.x + NoteOverlap;
            float left = right - width;
            if (left < NoteScreenPad)
            {
                left = NoteScreenPad;
                right = Mathf.Min(left + width, card.x + card.width * 0.5f);
            }
            return new Vector2(left, Mathf.Max(NoteTextChromeWidth, right - left));
        }

        private static float MeasureEdgeNoteHeight(Rect card, string text, GUIStyle style)
        {
            float labelWidth = Mathf.Max(1f, NoteSpan(card, text, style).y - NoteTextChromeWidth);
            float wrappedHeight = style.CalcHeight(new GUIContent(text), labelWidth);
            return Mathf.Max(style.fontSize + 12f, wrappedHeight + 6f);
        }

        // ── 需求骰位：方块 Slot（与手牌骰子/物品同族）──────────────────

        // 每个 require 画成一个方块 Slot，居中横排。骰子 slot = 大字 D/值；
        // 物品 slot = 符号 + 数量（强调）+ 下方名称展签。判定核心骰上方挂技能药丸。
        // 尺寸不小于手牌方块（56），物品略大以容纳信息。
        // 骰位方块必须和手牌方块一样大——拖过去的东西和接它的坑不一样大，手感立刻就错。
        private static float DieSlot => HandPanelDrawer.TokenSize;
        private static float ItemSlot => DieSlot + 6f;
        private const float SlotGap = 10f;
        private const float PillH      = 18f;
        private const float PillGap = 5f;

        private static readonly Color SlotBlockBg = new Color(0.024f, 0.031f, 0.047f, 1f);

        private static float SlotSize(ActionCost req) => req.Type == "die" ? DieSlot : ItemSlot;

        // 方块能缩到多小由「里面装得下什么」决定，不是拍一个比例：
        // 骰子格里是 26px 的大字 + 描边 → 32；物品格要上下叠符号(22px)和数量(15px)两行 → 48。
        // 旧版那个 0.7 比例下限是假保底：die 格的 need=0.7×56+17=56.2 恒 > 触发缩放的 avail(<73)，
        // 也就是说它一被用到就一定还在溢出，只是数字好看一点。下限必须由内容推导才有意义。
        private static float SlotMinSize(ActionCost req) => req.Type == "die" ? 32f : 48f;

        // 骰位区在最压缩状态下仍需要的高度：缩到内容下限的方块；判定核心额外带属性药丸。
        // 副标题的高度预算要先扣掉它——先保住可交互核心，剩下的才给气氛文本。
        /// <summary>
        /// 技能药丸挂在第几个槽上。<b>只有判定卡有</b>：药丸说的是"这颗骰要和哪个属性一起比"，
        /// 非判定的骰子不和任何东西比，硬给它一枚药丸只会让两种卡看起来是同一种。
        /// 非判定由槽位形状去说（圆＝交出去），不在这儿说第二遍。
        /// </summary>
        private static int SkillPillIndex(GameNode node) =>
            node.Resolve != null && node.Resolve.Type == ResolveType.Roll ? FindCoreDieIndex(node) : -1;

        private static float RequirementSlotsMinNeed(GameNode node)
        {
            var reqs = node.Requires;
            if (reqs == null || reqs.Count == 0) return 0f;

            ActionCost biggest = reqs[0];
            for (int j = 1; j < reqs.Count; j++)
                if (SlotSize(reqs[j]) > SlotSize(biggest)) biggest = reqs[j];

            return SlotMinSize(biggest) + (SkillPillIndex(node) >= 0 ? PillH + PillGap : 0f);
        }

        private static void DrawRequirementSlots(
            Rect rect, float bodyY, float exeY, float clocksBottomY, GameNode node, List<SlottedResource?>? slotted,
            bool disabled, IMGUIInteractionContext ui, SSNoirGameManager gameManager,
            ref CardDrawer.CardInteraction interaction)
        {
            var reqs = node.Requires;
            if (reqs == null || reqs.Count == 0) return;

            int n = reqs.Count;
            ActionCost biggest = reqs[0];
            for (int j = 1; j < n; j++)
                if (SlotSize(reqs[j]) > SlotSize(biggest)) biggest = reqs[j];
            float maxSize = SlotSize(biggest);

            // D 和物品符号已经足够说明需求类型；不再重复画“骰子 / 金钱”展签。
            // 判定属性是唯一保留的文字标识，始终挂在核心骰上方的药丸里。
            float avail = Mathf.Max(0f, exeY - bodyY);
            int pillIndex = SkillPillIndex(node);
            bool showPill = pillIndex >= 0;
            float aboveH = showPill ? PillH + PillGap : 0f;
            float belowH = HasItemRequirement(reqs) ? CaptionH + CaptionGap + CaptionBottomPad : 0f;
            float scale = 1f;
            if (aboveH + maxSize + belowH > avail && maxSize > 0f)
                scale = Mathf.Clamp((avail - aboveH - belowH) / maxSize, SlotMinSize(biggest) / maxSize, 1f);

            float scaledMax = maxSize * scale;
            float need = aboveH + scaledMax + belowH;
            float startY = bodyY + Mathf.Max(0f, (avail - need) * 0.5f);
            // 上面这些让位/缩放都只是「尽量好看」，真正的保证在这两行：
            // 骰位与执行按钮都是可交互元素，谁压住谁都是 bug，所以最后无条件把整块（含药丸与
            // 展签）钉在执行按钮上方；实在挤不下就向上侵占标题/副标题区（那是展示，不是交互），
            // 但不盖住时钟徽章。这样结论不依赖上面任何一档参数调得准不准。
            startY = Mathf.Max(startY, clocksBottomY);
            startY = Mathf.Min(startY, exeY - need);
            float centerY = startY + aboveH + scaledMax * 0.5f;

            float rowW = 0f;
            for (int j = 0; j < n; j++) rowW += SlotSize(reqs[j]) * scale;
            rowW += SlotGap * (n - 1);

            float x = rect.center.x - rowW * 0.5f;
            for (int j = 0; j < n; j++)
            {
                var req = reqs[j];
                float size = SlotSize(req) * scale;
                var square = new Rect(x, centerY - size * 0.5f, size, size);
                if (showPill && j == pillIndex)
                    DrawSkillPill(square, SkillInfo.DisplayName(node.Resolve!.SkillName));

                DrawSlotBlock(square, node, j, req, slotted != null ? slotted[j] : null, disabled, ui, gameManager,
                    ref interaction, judged: j == pillIndex);

                x += size + SlotGap;
            }
        }

        // 卡片纵向流量的唯一定义，绘制（DrawContent）与测量（RecommendedCardHeight）共用同一组常量：
        // 顶部挂件（时钟徽章 / 能力栏）→ 标题 → 副标题 → 骰位 → 执行按钮（+命运条）。
        private const float TitleH = 26f;
        private const float TopPad = 8f;               // 无顶部挂件时标题距卡顶
        private const float GapAfterTopWidgets = 6f;   // 顶部挂件与标题之间
        private const float GapTitleToBody = 6f;       // 标题/副标题与骰位之间
        private const float BottomPad = 8f;

        private static float TitleTop(float topWidgetsBottom, float cardTop)
            => topWidgetsBottom > cardTop ? topWidgetsBottom + GapAfterTopWidgets : cardTop + TopPad;

        // 副标题实际占多高。测量与绘制必须调同一个函数：以前两边各写一份 Clamp（测量 22~72、
        // 紧凑绘制 18~36），于是「卡按 72 长高、文字只画 36」这类空转和「卡按 36 定高、文字要 72」
        // 这类拥挤会同时存在。
        private static GUIStyle SubtitleStyle() => new(IMGUIStyles.CardSubtitle)
        {
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            clipping = TextClipping.Clip
        };

        // Unity IMGUI 的 wordWrap 只会在空白等词界断行。中文副标题通常没有空格，长到卡宽时
        // CalcHeight 仍报一行，而 GUI.Label 又会把整段直接画出卡外。先按实际字宽插入换行，
        // 让测量与绘制使用完全相同的文本；这也适用于夹在中文里的英文长词。
        private static string WrapSubtitle(string text, GUIStyle style, float width)
        {
            var result = new StringBuilder(text.Length + 8);
            var line = new StringBuilder(text.Length);

            foreach (char c in text)
            {
                if (c == '\r')
                    continue;
                if (c == '\n')
                {
                    result.Append(line.ToString().TrimEnd());
                    result.Append('\n');
                    line.Clear();
                    continue;
                }

                line.Append(c);
                if (line.Length <= 1 || style.CalcSize(new GUIContent(line.ToString())).x <= width)
                    continue;

                line.Length--;
                result.Append(line.ToString().TrimEnd());
                result.Append('\n');
                line.Clear();
                if (!char.IsWhiteSpace(c))
                    line.Append(c);
            }

            result.Append(line);
            return result.ToString();
        }

        private static float MeasureSubtitleHeight(string subtitle, GUIStyle style, float width)
        {
            return Mathf.Clamp(style.CalcHeight(new GUIContent(subtitle), width), 22f, 96f);
        }

        private static float MeasureSubtitleHeight(GameNode node, float cardWidth)
        {
            if (string.IsNullOrWhiteSpace(node.Subtitle))
                return 0f;

            var style = SubtitleStyle();
            string subtitle = WrapSubtitle(node.Subtitle, style, cardWidth - 24f);
            return MeasureSubtitleHeight(subtitle, style, cardWidth - 24f);
        }

        // 这张动作卡按自身内容该有多高。网格卡与世界投射卡都用它定高——不再有「网格一律 190」
        // 那种与内容无关的固定值。
        public static float RecommendedCardHeight(
            GameNode node,
            float cardWidth,
            IReadOnlyList<ActorSnapshot>? actors)
        {
            if (node.Resolve == null)
                return CardDrawer.MinCardHeight;

            bool isRoll = node.Resolve.Type == ResolveType.Roll;
            float clockHeight = CardDrawer.MeasureClockBadgesHeight(cardWidth, node.Clocks);
            float topWidgetsBottom = clockHeight;
            float titleY = TitleTop(topWidgetsBottom, 0f);

            float subtitleHeight = MeasureSubtitleHeight(node, cardWidth);

            float requirementsHeight = PreferredRequirementSlotsHeight(node);
            float bodyBottom = titleY + TitleH + subtitleHeight;
            if (requirementsHeight > 0f)
                bodyBottom += GapTitleToBody + requirementsHeight;

            // 与 DrawContent 的 exeY 反向对齐：按钮高 26，命运条另占 28，其下留 BottomPad。
            float bottomControls = GapTitleToBody + 26f + (isRoll ? 28f : 0f) + BottomPad;
            return Mathf.Max(CardDrawer.MinCardHeight, bodyBottom + bottomControls);
        }

        private static float PreferredRequirementSlotsHeight(GameNode node)
        {
            var reqs = node.Requires;
            if (reqs == null || reqs.Count == 0)
                return 0f;

            float maxSize = 0f;
            foreach (var req in reqs)
                maxSize = Mathf.Max(maxSize, SlotSize(req));
            float above = SkillPillIndex(node) >= 0 ? PillH + PillGap : 0f;
            float below = HasItemRequirement(reqs) ? CaptionH + CaptionGap + CaptionBottomPad : 0f;
            return above + maxSize + below;
        }

        /// <summary>
        /// 把一个需求槽单独画在卡片之外（随身条那种「一个标题 + 一个骰位」的小挂件）。
        /// 走的就是卡里那个 <see cref="DrawSlotBlock"/>，所以尺寸、占位符、可放置高亮、
        /// 拿起放下全都是同一套，不是仿造的。
        /// </summary>
        public static CardDrawer.CardInteraction DrawStandaloneSlot(
            Rect rect, GameNode node, int slotIndex, ActionCost req, SlottedResource? res,
            bool disabled, IMGUIInteractionContext ui, SSNoirGameManager gameManager)
        {
            var interaction = new CardDrawer.CardInteraction
            {
                CardClicked = false, ClickedSlotIndex = -1, DroppedSlotIndex = -1, ExecuteClicked = false
            };
            // 随身条自己写着这件东西的名字，这里再挂一次展签就是重复。
            // 判定与否照样按节点算：随身的东西今天都是消耗（抽烟、喝酒），但这里不写死——
            // 哪天有一张随身的判定卡，它该是方槽。
            DrawSlotBlock(rect, node, slotIndex, req, res, disabled, ui, gameManager, ref interaction,
                showCaption: false, judged: slotIndex == SkillPillIndex(node));
            return interaction;
        }

        // 方块 Slot：与手牌方块同族。空 = Ink 槽 + 占位符（提示放什么）；填 = 实心黑方块 + 亮内容。
        private static void DrawSlotBlock(
            Rect rect, GameNode node, int slotIndex, ActionCost req, SlottedResource? res,
            bool disabled, IMGUIInteractionContext ui, SSNoirGameManager gameManager,
            ref CardDrawer.CardInteraction interaction, bool showCaption = true, bool judged = false)
        {
            bool slotTargeted = !disabled && ui.IsPointerInside(rect);
            bool slotHover = slotTargeted && IMGUIInteractionContext.HoverAvailable;
            bool canMatchHeld = !disabled && gameManager.CanMatchRequirement(req);
            bool canDropHeld = !disabled && gameManager.CanPlaceSelectedResource(node, slotIndex);
            Color border = SlotBorderColor(disabled, canDropHeld, canMatchHeld, slotHover);
            bool filled = res != null;

            // ── 槽位有两种，形状就是它们的区别 ───────────────────────────
            //
            // <b>方＝判定槽</b>：这颗骰要投出去和一个属性比大小。方是骰子自己的形状，
            // 而它上面永远挂着那枚属性药丸——"属性 → 骰子"是这套判定的读法。
            //
            // <b>八角＝消耗槽</b>：交进去就没了。物品一律是它；非判定动作花掉的那颗行动骰
            // 也是它（养伤、看花、抽烟）。切掉的四角让它一眼不是方——不会被读成"要掷"——
            // 又比圆留得住内部空间，图标和 ×N 不用为形状让路。
            //
            // 这两件事以前只能靠"有没有药丸"分辨，那是缺席在说话——玩家扫一眼卡片阵列时
            // 根本不会注意到少了什么。形状是在场的差别，一眼就分得开。
            //
            // （以后消耗槽可能还要写"需要 ≥4 的骰子"这类条件，位置就在圆里/圆下，
            // 判定槽不会有这种东西——又一条它们不该同形的理由。）
            // 投影必须跟着形状走。IMGUIStyles.DrawShadow 画的是矩形，八角槽用它就会在四个
            // 切角外露出一圈灰方块——看起来像"八角贴在一块方板上"，正好把刚建立的形状区别
            // 又抹掉了。
            if (!disabled && (filled || canDropHeld))
            {
                var shadowRect = new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height);
                if (judged)
                    IMGUIStyles.DrawShadow(rect, new Vector2(2f, 2f), 0.4f);
                else
                    ShapeDrawer.DrawOctagon(shadowRect, new Color(0f, 0f, 0f, 0.4f));
            }
            // 禁用态换成暖灰洗色（同 DrawCardFrame/DrawExecuteButton），别再用 Ink 叠透明——
            // 卡面本身已经因为禁用而发灰发亮，槽位再用近黑填充只会显得突兀地"更黑"。
            Color fill = disabled
                ? new Color(IMGUIStyles.DisabledWash.r, IMGUIStyles.DisabledWash.g, IMGUIStyles.DisabledWash.b, 0.22f)
                : filled ? SlotBlockBg : IMGUIStyles.Ink;
            if (judged)
            {
                IMGUIStyles.SetColor(fill);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                IMGUIStyles.ResetColor();
            }
            else
            {
                ShapeDrawer.DrawOctagon(rect, fill);
            }

            // 可放置的槽位一律 2px、指针压上去 3px。金色本身已经是满不透明的标准金，
            // 不够跳不是颜色被冲淡，是线太细：1px 在 1.8 倍缩放下是最弱的一档笔画，
            // 而"这里能放"是拖着骰子时满屏唯一要看的信息，不该和普通边框一样粗。
            float outlineWidth =
                canDropHeld ? (slotHover ? 3f : 2f)
                            : (filled ? 2f : 1f);
            if (judged)
            {
                IMGUIStyles.DrawOutline(rect, outlineWidth, border);
            }
            else
            {
                // 八角的边是贴图缩上去的，边缘带一层抗锯齿过渡，同样的线宽看着比方槽那条
                // 硬边更粗更亮。减半档补回来，两种槽的笔画才是同一档。
                ShapeDrawer.DrawOctagonOutline(rect, Mathf.Max(1f, outlineWidth - 0.5f), border);
            }

            Color content = disabled
                ? IMGUIStyles.DisabledWash
                : filled ? IMGUIStyles.Paper : SlotPlaceholderColor(canDropHeld, canMatchHeld);

            // 方块被压缩时（见 DrawRequirementSlots 的 scale）内部字号与留白按同一比例跟着走。
            // 否则物品格缩到 48px 时，固定偏移的符号(y+8 高26)和数量(yMax-24 高20)会直接叠在一起。
            // k=1 时与原本的固定数值完全一致，正常尺寸下无任何变化。
            float k = rect.height / SlotSize(req);

            if (req.Type == "die")
            {
                // 判定槽里的点数是**要算的**（它进准备值），所以写成撑满格子的大字。
                // 消耗槽里的点数只是"交出去的是哪一颗"，不参与任何计算——所以它跟物品同一种
                // 读法：上面一个图标说"交的是什么"，下面一个小数字说"多少 / 哪一颗"。
                // 两种槽的内部重量这才对得上，不会一个靠大字撑满、一个靠图标显虚。
                if (filled && !judged)
                {
                    var faceStyle = new GUIStyle(IMGUIStyles.SlotLabel)
                    {
                        fontSize = Mathf.RoundToInt(15f * k),
                        alignment = TextAnchor.LowerCenter,
                        normal = { textColor = content }
                    };
                    IMGUIStyles.ApplyStrongFont(faceStyle);
                    ItemIconLibrary.DrawDiePlaceholder(
                        new Rect(rect.x, rect.y + 7f * k, rect.width, 26f * k), content, 0.62f);
                    IMGUIStyles.DrawLabel(
                        new Rect(rect.x, rect.yMax - 26f * k, rect.width, 20f * k),
                        res!.Value.ToString(), faceStyle);
                }
                else if (filled)
                {
                    var s = new GUIStyle(IMGUIStyles.SlotLabel)
                    {
                        fontSize = Mathf.RoundToInt(26f * k),
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = content }
                    };
                    IMGUIStyles.ApplyStrongFont(s);
                    IMGUIStyles.DrawLabel(rect, res!.Value.ToString(), s);
                }
                else
                {
                    ItemIconLibrary.DrawDiePlaceholder(rect, content, 0.56f);
                }
            }
            else
            {
                // 物品：符号（大）+ 数量（强调），both 突出，名称在方块外下方展签。
                string symbol = ItemSymbol(req.ItemId);
                int qty = filled ? (res!.Qty > 0 ? res.Qty : 1) : req.Qty;
                var symStyle = new GUIStyle(IMGUIStyles.SlotLabel)
                {
                    fontSize = Mathf.RoundToInt(22f * k),
                    alignment = TextAnchor.UpperCenter,
                    normal = { textColor = content }
                };
                IMGUIStyles.ApplyStrongFont(symStyle);
                // 八角的上下切角把最外两行的可用宽度削窄了，内容整体上收一点，
                // 图标不压边、数量不骑在斜切上。
                var symRect = new Rect(rect.x, rect.y + (judged ? 8f : 7f) * k, rect.width, 26f * k);
                if (!ItemIconLibrary.TryDraw(symRect, req.ItemId, content, 0.80f))
                    IMGUIStyles.DrawLabel(symRect, symbol, symStyle);

                var qtyStyle = new GUIStyle(IMGUIStyles.SlotLabel)
                {
                    fontSize = Mathf.RoundToInt(15f * k),
                    alignment = TextAnchor.LowerCenter,
                    normal = { textColor = content }
                };
                IMGUIStyles.ApplyStrongFont(qtyStyle);
                IMGUIStyles.DrawLabel(
                    new Rect(rect.x, rect.yMax - (judged ? 24f : 26f) * k, rect.width, 20f * k), $"×{qty}", qtyStyle);

                if (showCaption)
                {
                    var captionStyle = new GUIStyle(IMGUIStyles.CardSubtitle)
                    {
                        fontSize = Mathf.RoundToInt(11f * k),
                        alignment = TextAnchor.UpperCenter,
                        normal = { textColor = content }
                    };
                    IMGUIStyles.DrawLabel(
                        new Rect(rect.x - 8f, rect.yMax + CaptionGap * k, rect.width + 16f, CaptionH * k),
                        ItemDisplayName.Short(req.ItemId), captionStyle);
                }
            }

            HandleSlotClick(rect, slotIndex, filled, disabled, canDropHeld, slotTargeted, ui, ref interaction);
        }

        // 物品展签：图标说不清"是哪一件"（纸条、照片、底片在剪影里都是纸），名字必须跟着。
        // 高度参与骰位区的排版预算（DrawRequirementSlots 的 belowH 与
        // PreferredRequirementSlotsHeight 的同名项），两边必须同时改。
        private const float CaptionH = 13f;
        private const float CaptionGap = 2f;
        // 展签底下再留一口气：不留的话名字会贴着执行钮，读起来像是按钮的说明而不是槽的标签。
        // 只进排版预算，不改展签自己的位置——它仍然紧跟着槽走。
        private const float CaptionBottomPad = 5f;

        private static bool HasItemRequirement(IReadOnlyList<ActionCost> reqs)
        {
            for (int i = 0; i < reqs.Count; i++)
                if (reqs[i].Type != "die") return true;
            return false;
        }

        // 技能药丸 + 连线，挂在判定骰方块上方（属性 → 骰子）。
        private static void DrawSkillPill(Rect square, string skill)
        {
            const float pillW = 54f;
            var pill = new Rect(square.center.x - pillW * 0.5f, square.y - PillGap - PillH, pillW, PillH);
            IMGUIStyles.SetColor(IMGUIStyles.Ink);
            GUI.DrawTexture(pill, Texture2D.whiteTexture);
            IMGUIStyles.ResetColor();
            IMGUIStyles.DrawOutline(pill, 1f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f));
            var s = new GUIStyle(IMGUIStyles.CardSubtitle)
            {
                fontSize = IMGUIStyles.FontSize(11),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = IMGUIStyles.TextSecondary }
            };
            IMGUIStyles.DrawLabel(pill, skill, s);
            IMGUIStyles.DrawLine(new Vector2(pill.center.x, pill.yMax), new Vector2(square.center.x, square.y),
                new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.25f), 1f);
        }

        private static string ItemSymbol(string name)
        {
            return name switch
            {
                "金钱" => "$",
                "酒" => "酒",
                "药品" => "药",
                _ => string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1)
            };
        }

        private static int FindCoreDieIndex(GameNode node)
        {
            if (node.Requires == null) return -1;
            for (int k = 0; k < node.Requires.Count; k++)
            {
                if (node.Requires[k].Type == "die")
                    return k;
            }
            return -1;
        }

        private static Color SlotBorderColor(bool disabled, bool canDropHeld, bool canMatchHeld, bool hover)
        {
            if (disabled) return new Color(IMGUIStyles.DisabledWash.r, IMGUIStyles.DisabledWash.g, IMGUIStyles.DisabledWash.b, 0.70f);
            if (canDropHeld) return IMGUIStyles.Gold;
            if (canMatchHeld) return IMGUIStyles.SealRed;
            return hover
                ? new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 1f)
                : new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f);
        }

        private static Color SlotPlaceholderColor(bool canDropHeld, bool canMatchHeld)
        {
            if (canDropHeld) return IMGUIStyles.Gold;
            if (canMatchHeld) return IMGUIStyles.SealRed;
            return new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.5f);
        }

        private static void HandleSlotClick(
            Rect rect, int slotIndex, bool filled, bool disabled, bool canDropHeld, bool slotTargeted,
            IMGUIInteractionContext ui, ref CardDrawer.CardInteraction interaction)
        {
            if (disabled) return;
            // 已放入卡槽的资源和手牌资源必须在按下时就进入同一条拖拽状态机。
            // 旧逻辑在 MouseUp 才拿起它，结果第一次松手只会让骰子“挂到鼠标上”，
            // 根本不可能从当前卡连续拖到另一张卡。
            if (filled && ui.WasClicked(rect))
            {
                interaction.ClickedSlotIndex = slotIndex;
                Event.current.Use();
            }
            else if (canDropHeld && slotTargeted && Event.current.type == EventType.MouseUp && Event.current.button == 0)
            {
                interaction.DroppedSlotIndex = slotIndex;
                Event.current.Use();
            }
        }

        // ── 执行按钮（实心金 = 执行 / 主行动按钮）──────────────────────

        // DESIGN.md 强调色岗位一：实心金 = 执行。金底 + 深字 #2A2107。
        // dead=true（"不可用"）与 enabled=false 且 dead=false（"待命"）过去共用同一副淡描边，
        // 只靠文字区分——两者含义完全不同（"待命"还有希望，"不可用"是彻底关闭），必须分开画：
        // 待命＝幽灵描边（还悬着）；不可用＝实心盖章灰块（已经封死），不留几乎不可见的淡边框。
        private static bool DrawExecuteButton(Rect rect, string text, IMGUIInteractionContext ui, bool enabled, bool dead = false)
        {
            bool isInteractable = enabled && !ui.IsLocked;
            bool isHovered = isInteractable && ui.CanHover(rect);
            bool isClicked = isInteractable && ui.WasTapped(rect);

            if (isClicked)
            {
                Event.current.Use();
            }

            var style = new GUIStyle(IMGUIStyles.ExecuteLabel);
            if (isInteractable)
            {
                IMGUIStyles.DrawShadow(rect, new Vector2(2f, 2f), 0.45f);
                IMGUIStyles.SetColor(isHovered
                    ? new Color(Mathf.Min(1f, IMGUIStyles.Gold.r * 1.08f), Mathf.Min(1f, IMGUIStyles.Gold.g * 1.08f), Mathf.Min(1f, IMGUIStyles.Gold.b * 1.08f), 1f)
                    : IMGUIStyles.Gold);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                IMGUIStyles.ResetColor();
                style.normal.textColor = IMGUIStyles.GoldOnDark;
            }
            else if (dead)
            {
                IMGUIStyles.SetColor(IMGUIStyles.DisabledWash);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                IMGUIStyles.ResetColor();
                style.normal.textColor = IMGUIStyles.Ink;
            }
            else
            {
                var faded = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.35f);
                IMGUIStyles.DrawOutline(rect, 1f, faded);
                style.normal.textColor = faded;
            }
            IMGUIStyles.DrawLabel(rect, text, style);

            return isClicked;
        }

        // 执行中：Ink 底 + 金填充进度 + 金描边 + 金光呼吸（「正在发生」）。
        private static void DrawExecuteProgress(Rect rect, float progress, string text)
        {
            progress = Mathf.Clamp01(progress);
            IMGUIStyles.SetColor(IMGUIStyles.Ink);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            var fillRect = new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * progress, rect.height - 4f);
            IMGUIStyles.SetColor(IMGUIStyles.Gold);
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
            IMGUIStyles.ResetColor();
            IMGUIStyles.DrawOutline(rect, 1f, IMGUIStyles.Gold);
            IMGUIStyles.DrawGoldPulse(rect);

            string label = string.IsNullOrEmpty(text) ? "执行中" : text;
            if (label.Length > 5) label = "执行中";
            var progressStyle = new GUIStyle(IMGUIStyles.ExecuteLabel);
            progressStyle.normal.textColor = progress > 0.5f ? IMGUIStyles.GoldOnDark : IMGUIStyles.Paper;
            IMGUIStyles.DrawLabel(rect, label, progressStyle);
        }

        // ── 右缘能力附件（规范色，无主题色）────────────────────────────

        // 显示当前判定技能下每个在场角色的等级，用 Ink 底 + Paper 描边 + 白字的小芯片。
        // 不再使用主题色，符合 DESIGN.md「全局唯一主强调色」与「黑底安静块」的规则。
        private static void DrawActorAbilityRail(
            Rect rect, string skill, IReadOnlyList<ActorSnapshot> actors, PresentationSnapshot snapshot)
        {
            // 人身上的减值和他的技能值长在一起读：0 −1 就是「底子 0，脸伤扣 1」。
            // 只压主角，同伴出骰不吃这一笔——与 EffectiveModifiers 同一条规则。
            int penalty = 0;
            if (snapshot.InjurySkillPenalty != 0
                && string.Equals(snapshot.InjurySkillKey, skill, System.StringComparison.OrdinalIgnoreCase))
                penalty += snapshot.InjurySkillPenalty;
            if (snapshot.ScarModifiers.TryGetValue(skill, out var scar) && scar != null)
                penalty += scar.Value;

            // 带修正的片子要宽一格：修正必须画在片子里，画到片外就会越过下面那条安全区夹紧。
            float chipW = penalty != 0 ? 78f : 54f;
            const float chipH = 20f;
            // 片子压进卡内多少。12 时它几乎整块悬在卡外，和便签一样会被读成邻居那张卡的；
            // 往里收到 34 就只剩一小截探出卡边——够看出「挂在这张卡边上」，又不遮卡内内容。
            const float overlap = 34f;
            // 右缘附件和左便签一样只压进卡边一点。贴右屏的卡收进安全区，宁可贴着卡内缘，
            // 也不让能力信息跑出画布。
            float x = Mathf.Min(rect.xMax - overlap, UIScale.SafeArea.xMax - chipW - 4f);
            float y = rect.y + 22f;
            int drawn = 0;
            foreach (var actor in actors)
            {
                // 只画本场登场的人（在队且还站得住）。谁在队里由场景自己决定——
                // 码头坍塌就是在场内把弗兰克和林请进队，他们的技能从这一刻起挂在每张卡上。
                if (!actor.OnStage || !actor.Stats.TryGetValue(skill, out int level))
                    continue;

                var chip = new Rect(x, y + drawn * (chipH + 4f), chipW, chipH);
                IMGUIStyles.SetColor(IMGUIStyles.Ink);
                GUI.DrawTexture(chip, Texture2D.whiteTexture);
                IMGUIStyles.ResetColor();
                IMGUIStyles.DrawOutline(chip, 1f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.70f));

                string shortName = actor.Name.Length > 2 ? actor.Name.Substring(0, 2) : actor.Name;
                var nameStyle = new GUIStyle(GUI.skin.label)
                {
                    font = IMGUIStyles.ChineseFont,
                    fontSize = IMGUIStyles.FontSize(12),
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = IMGUIStyles.Paper }
                };
                IMGUIStyles.ApplyStrongFont(nameStyle);
                IMGUIStyles.DrawLabel(new Rect(chip.x + 5f, chip.y, 30f, chipH), shortName, nameStyle);

                var lvlStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = IMGUIStyles.FontSize(14),
                    alignment = TextAnchor.MiddleRight,
                    normal = { textColor = IMGUIStyles.Paper }
                };
                // 修正只压主角：同伴出骰不吃这一笔，和 EffectiveModifiers 是同一条规则。
                bool showPenalty = penalty != 0 && actor.Role == "protagonist";
                // 底子值靠右收在修正左边，两个数并排读作「0 −1」。
                float lvlRight = showPenalty ? chip.width - 28f : chip.width - 6f;
                IMGUIStyles.DrawLabel(new Rect(chip.x, chip.y, lvlRight, chipH), level.ToString(), lvlStyle);

                if (showPenalty)
                {
                    var modStyle = new GUIStyle(lvlStyle)
                    {
                        fontSize = IMGUIStyles.FontSize(13),
                        normal = { textColor = penalty < 0 ? IMGUIStyles.OddsFail : IMGUIStyles.OddsSuccess }
                    };
                    IMGUIStyles.DrawLabel(new Rect(chip.x, chip.y, chip.width - 6f, chipH),
                        (penalty > 0 ? "+" : "−") + Mathf.Abs(penalty), modStyle);
                }
                drawn++;
            }
        }

        // ── 底部：命运六面预览 ────────────────────────────────────────

        private static void TryDrawFatePreview(Rect rect, GameNode node, PresentationSnapshot snapshot,
            List<SlottedResource?> slotted, IReadOnlyList<ActorSnapshot> actors, float executeBottomY)
        {
            string skill = node.Resolve!.SkillName;
            SlottedResource? dieSlot = null;
            foreach (var s in slotted)
            {
                if (s != null && s.Type == "die") { dieSlot = s; break; }
            }
            if (dieSlot == null) return;

            int? skillLevel = null;
            string actorRole = string.Empty;
            foreach (var actor in actors)
            {
                if (actor.Id != dieSlot.ActorId) continue;
                if (!actor.Stats.TryGetValue(skill, out int lv))
                    throw new System.InvalidOperationException($"Actor '{actor.Id}' is missing required stat '{skill}'.");
                skillLevel = lv;
                actorRole = actor.Role;
                break;
            }
            if (!skillLevel.HasValue)
                throw new System.InvalidOperationException($"Actor '{dieSlot.ActorId}' was not found for fate preview.");

            // 按放骰子的那个人重算一遍修正：赔率条必须和 SceneManager 的结算走同一笔账。
            int modSum = 0;
            foreach (var m in EffectiveModifiers(node, snapshot, actorRole)) modSum += m.Value;

            DrawFateStrip(rect, FateStrip.Compute(dieSlot.Value, skillLevel.Value, modSum), executeBottomY,
                PreparedBreakdown(node.Resolve!.SkillName, dieSlot.Value, skillLevel.Value, modSum));
        }

        // 命运条上面那一行小字。它以前写的是「1–2 坏 · 3–5 中 · 6 好」——
        // 那正是下面那条彩色赔率条已经在画的东西，等于把同一件事说两遍。
        // 改成算式：这颗骰几点、你的能力加几、卡上的修正加减几，最后凑出多少准备值。
        // 这是玩家投骰前唯一算不出来的数，也是他决定"这颗骰给哪张卡"的依据。
        private static string PreparedBreakdown(string skill, int dieValue, int skillLevel, int modSum)
        {
            int prepared = dieValue + skillLevel + modSum;
            var text = new System.Text.StringBuilder();
            text.Append("准备值 ").Append(prepared).Append(" ＝ 骰 ").Append(dieValue);
            if (skillLevel != 0)
                text.Append(skillLevel > 0 ? " + " : " − ")
                    .Append(SkillInfo.DisplayName(skill)).Append(' ').Append(Mathf.Abs(skillLevel));
            if (modSum != 0)
                text.Append(modSum > 0 ? " + 修正 " : " − 修正 ").Append(Mathf.Abs(modSum));
            return text.ToString();
        }

        private static void DrawFateStrip(Rect rect, RollOutcome[] strip, float executeBottomY, string caption)
        {
            const float pad = 10f;
            var summaryStyle = new GUIStyle(IMGUIStyles.ClockLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = IMGUIStyles.TextSecondary }
            };
            IMGUIStyles.DrawLabel(new Rect(rect.x + pad, executeBottomY, rect.width - pad * 2f, 14f),
                caption, summaryStyle);

            float avail = rect.width - pad * 2f;
            float stripW = Mathf.Min(avail, 178f);
            DrawOddsStrip(new Rect(rect.center.x - stripW / 2f, executeBottomY + 14f, stripW, 17f), strip, 0);
        }

        // 命运条即赔率条：实心染色 + 档间留缝，占比一眼可读。命中格抬起 + 金描边 + 弹跳；
        // settled（结果定格）时压暗其余格，让命中格更跳。掷骰时命中格随扫掠移动，落定后定在最终骰面。
        // 公开供重结算大弹窗复用（掷骰态/结算态同一套视觉）。
        public static void DrawOddsStrip(Rect area, RollOutcome[] strip, int highlightedFace,
            float pulse = 0f, bool settled = false)
        {
            const float gap = 3f, boundaryGap = 9f;
            int boundaries = 0;
            for (int i = 1; i < strip.Length; i++)
                if (strip[i] != strip[i - 1]) boundaries++;

            float cellW = (area.width - gap * (5 - boundaries) - boundaryGap * boundaries) / 6f;
            float x = area.x;
            var faceStyle = EnsureOddsFaceStyle();
            for (int i = 0; i < 6; i++)
            {
                if (i > 0) x += strip[i] != strip[i - 1] ? boundaryGap : gap;
                Color tier = OutcomeColor(strip[i]);
                bool hi = i + 1 == highlightedFace;
                float pop = hi ? pulse * 5f : 0f;
                var cell = new Rect(x - pop / 2f, area.y + (hi ? -2f : 0f) - pop,
                    cellW + pop, area.height + (hi ? 4f : 0f) + pop * 2f);
                float dim = hi ? 1f : (settled ? 0.34f : 0.70f);
                IMGUIStyles.SetColor(new Color(tier.r * dim, tier.g * dim, tier.b * dim, 1f));
                GUI.DrawTexture(cell, Texture2D.whiteTexture);
                IMGUIStyles.ResetColor();
                if (hi)
                    IMGUIStyles.DrawOutline(cell, 2f, IMGUIStyles.Gold);
                float td = hi ? 0.18f : (settled ? 0.10f : 0.20f);
                faceStyle.normal.textColor = new Color(tier.r * td, tier.g * td * 0.8f, tier.b * td * 0.8f, 1f);
                IMGUIStyles.DrawLabel(cell, FateFaceLabels[i], faceStyle);
                x += cellW;
            }
        }

        private static GUIStyle EnsureOddsFaceStyle()
        {
            var baseStyle = IMGUIStyles.ClockValue;
            if (_oddsFaceStyle == null
                || _oddsFaceStyle.font != baseStyle.font
                || _oddsFaceStyle.fontSize != baseStyle.fontSize)
            {
                _oddsFaceStyle = new GUIStyle(baseStyle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = false,
                    clipping = TextClipping.Clip,
                };
            }
            return _oddsFaceStyle;
        }

        // ── 判定 / 结算附件 ───────────────────────────────────────────

        private const float AttachmentGap = 6f;
        private const float AttachmentShadowReserve = 8f;
        private const float ResultAttachmentOpacity = 1f;
        // Attachment 不参与宿主卡片的 footprint，但仍是玩家要读的界面内容，不能被
        // SafeArea 外沿裁掉。这个 inset 只约束 attachment 自己，不会反过来推动卡片。
        private const float AttachmentSafeInset = 8f;

        private static readonly string[] FateFaceLabels = { "1", "2", "3", "4", "5", "6" };
        private static GUIStyle? _oddsFaceStyle;
        private static GUIStyle? _rollLabelStyle;
        private static GUIStyle? _rollDetailStyle;

        public static float AttachmentWidth(float cardWidth, bool spacious)
        {
            float desired = spacious
                ? Mathf.Clamp(cardWidth + 140f, 460f, 540f)
                : Mathf.Max(0f, cardWidth - 12f);
            float available = Mathf.Max(1f, UIScale.SafeArea.width - AttachmentSafeInset * 2f);
            return Mathf.Min(desired, available);
        }

        public static float LocalRollAttachmentHeight(bool spacious)
        {
            return AttachmentGap + RollHeaderHeight(spacious) + AttachmentShadowReserve;
        }

        public static float ResidueAttachmentHeight(CardPresentationResidue residue, bool spacious)
        {
            float panelWidth = spacious ? 460f : 300f;
            return AttachmentGap + ResiduePanelHeight(residue, spacious, panelWidth) + AttachmentShadowReserve;
        }

        public static Rect ResidueAttachmentRect(Rect cardRect, CardPresentationResidue residue, bool spacious)
        {
            float panelWidth = AttachmentWidth(cardRect.width, spacious);
            return KeepAttachmentOnScreen(new Rect(
                cardRect.center.x - panelWidth / 2f,
                cardRect.yMax + AttachmentGap,
                panelWidth,
                ResiduePanelHeight(residue, spacious, panelWidth)));
        }

        /// <summary>
        /// Attachment 的位置跟随宿主卡片，但不参与卡片排布；当宿主靠近屏幕边缘时，
        /// 只把 attachment 自己夹回安全区。这样结果不会把整摞卡推来推去，也不会被
        /// 屏幕边缘裁掉。
        /// </summary>
        private static Rect KeepAttachmentOnScreen(Rect panel)
        {
            Rect safe = UIScale.SafeArea;
            float inset = Mathf.Min(AttachmentSafeInset, Mathf.Min(safe.width, safe.height) * 0.5f);
            float width = Mathf.Min(panel.width, Mathf.Max(1f, safe.width - inset * 2f));
            float height = Mathf.Min(panel.height, Mathf.Max(1f, safe.height - inset * 2f));
            float minX = safe.xMin + inset;
            float minY = safe.yMin + inset;
            float maxX = Mathf.Max(minX, safe.xMax - inset - width);
            float maxY = Mathf.Max(minY, safe.yMax - inset - height);

            return new Rect(
                Mathf.Clamp(panel.x, minX, maxX),
                Mathf.Clamp(panel.y, minY, maxY),
                width,
                height);
        }

        // Attachment 不属于宿主卡的绘制层：调用者应在世界卡与底部物品栏之后统一绘制它。
        // 返回 true 表示玩家点了结果残影，调用者负责删除对应 residue。
        public static bool DrawAttachmentOverlay(
            Rect cardRect,
            ActionReport? localRoll,
            int localRollPhase,
            int localRollDisplayDieValue,
            float localRollDisplayScale,
            CardPresentationResidue? residue,
            IMGUIInteractionContext ui,
            bool spacious)
        {
            if (localRoll != null)
            {
                DrawLocalRoll(cardRect, localRoll, localRollPhase, localRollDisplayDieValue,
                    localRollDisplayScale, ui, spacious);
                return false;
            }
            if (residue == null)
                return false;

            DrawResiduePanel(cardRect, residue, ui, spacious);
            if (!ui.WasTapped(ResidueAttachmentRect(cardRect, residue, spacious)))
                return false;
            Event.current.Use();
            return true;
        }

        private static float RollHeaderHeight(bool spacious) => spacious ? 76f : 58f;
        private static float SimpleHeaderHeight(bool spacious) => spacious ? 36f : 28f;
        private static float EffectRowHeight(bool spacious) => spacious ? 20f : 14f;
        private const float EffectRowGap = 8f;

        // 掷骰中：命运条本身就是动画（减速扫掠 → 落格弹跳 → 定格），而非一个方框里跳变的数字。
        private static void DrawLocalRoll(
            Rect rect,
            ActionReport report,
            int phase,
            int displayDieValue,
            float displayScale,
            IMGUIInteractionContext ui,
            bool spacious)
        {
            float panelWidth = AttachmentWidth(rect.width, spacious);
            float headerHeight = RollHeaderHeight(spacious);
            var panel = KeepAttachmentOnScreen(new Rect(
                rect.center.x - panelWidth / 2f,
                rect.yMax + AttachmentGap,
                panelWidth,
                headerHeight));
            DrawAttachmentConnector(rect, panel);
            ui.CanHover(panel);
            DrawResultAttachmentBackground(panel);

            bool settled = phase >= 2;
            Color oc = settled ? OutcomeColor(report.Outcome) : IMGUIStyles.Gold;
            IMGUIStyles.DrawOutline(panel, 1.5f, oc);
            if (phase < 2)
                IMGUIStyles.DrawGoldPulse(panel);

            DrawRollHeaderText(panel, settled ? FormatOutcome(report.Outcome) : "判定中",
                settled ? oc : IMGUIStyles.TextPrimary,
                // 只写准备值：命运骰几点由下面那条带子说（命中格抬起 + 金描边 + 格里写着面数），
                // 再用文字复述一遍，就是把图形已经说清的事翻译成字。准备值不一样——
                // 它是这条带子为什么这样分档的原因，面板里没有第二处能看到。
                settled ? $"准备 {report.PreparedValue}" : "命运骰滚动...",
                spacious);

            int highlightedFace = phase == 0 ? displayDieValue : report.FateDieValue;
            float pulse = phase == 1 ? Mathf.Max(0f, displayScale - 1f) : 0f;
            var strip = FateStrip.StripForPrepared(report.PreparedValue);
            float stripY = spacious ? panel.y + 38f : panel.y + 26f;
            float stripHeight = spacious ? 24f : 18f;
            DrawOddsStrip(new Rect(panel.x + (spacious ? 16f : 10f), stripY,
                    panel.width - (spacious ? 32f : 20f), stripHeight),
                strip, highlightedFace, pulse, settled);

        }

        private static float ResidueHeaderHeight(CardPresentationResidue residue, bool spacious)
        {
            return residue.FateDieValue.HasValue ? RollHeaderHeight(spacious) : SimpleHeaderHeight(spacious);
        }

        private static float ResiduePanelHeight(CardPresentationResidue residue, bool spacious, float panelWidth)
        {
            float height = ResidueHeaderHeight(residue, spacious);
            bool hasBody = residue.Effects.Count > 0;
            if (hasBody)
            {
                height += spacious ? 17f : 11f; // 分隔 + 揭开动画最多 5px 的下移行程
                float horizontalInset = spacious ? 14f : 8f;
                height += CardDrawer.MeasureEffectRowsHeight(
                    residue.Effects,
                    panelWidth - horizontalInset * 2f,
                    EffectRowHeight(spacious),
                    EffectRowGap,
                    spacious ? 12 : 10);
            }
            return Mathf.Max(spacious ? 92f : 74f, height + (spacious ? 10f : 6f));
        }

        // 结算结果 = 命运条「原地定格」+ 结果从其下方揭开。头部与 DrawLocalRoll 落定态同内容，
        // 挂在卡片下方同一位置，动画停下后无缝冻结；效果继续向下揭开。
        private static void DrawResiduePanel(
            Rect rect,
            CardPresentationResidue residue,
            IMGUIInteractionContext ui,
            bool spacious)
        {
            // residue 只有动画落定后才被绘制——首帧即结果该「揭开」的时刻，惰性记录起点。
            if (residue.RevealStartTime <= 0f)
                residue.RevealStartTime = Time.unscaledTime;
            float reveal = Mathf.Clamp01((Time.unscaledTime - residue.RevealStartTime) / 0.28f);
            float ease = 1f - Mathf.Pow(1f - reveal, 3f);

            var panel = ResidueAttachmentRect(rect, residue, spacious);
            DrawAttachmentConnector(rect, panel);
            ui.CanHover(panel);
            DrawResultAttachmentBackground(panel);

            var header = new Rect(panel.x, panel.y, panel.width, ResidueHeaderHeight(residue, spacious));
            bool hasOutcome = residue.RollOutcome.HasValue;
            RollOutcome outcome = residue.RollOutcome ?? RollOutcome.Neutral;
            Color oc = hasOutcome ? OutcomeColor(outcome) : IMGUIStyles.Gold;
            IMGUIStyles.DrawOutline(header, 1.5f, oc);

            string headerLabel = hasOutcome ? FormatOutcome(outcome)
                : string.IsNullOrWhiteSpace(residue.Title) ? "行动结果" : residue.Title;
            DrawRollHeaderText(header, headerLabel, hasOutcome ? oc : IMGUIStyles.TextPrimary,
                residue.FateDieValue.HasValue ? $"准备 {residue.PreparedValue}" : "",
                spacious);

            if (residue.FateDieValue.HasValue)
            {
                var strip = FateStrip.StripForPrepared(residue.PreparedValue);
                float stripY = spacious ? header.y + 38f : header.y + 26f;
                float stripHeight = spacious ? 24f : 18f;
                DrawOddsStrip(new Rect(header.x + (spacious ? 16f : 10f), stripY,
                        header.width - (spacious ? 32f : 20f), stripHeight),
                    strip, residue.FateDieValue.Value, 0f, true);
            }

            // 身体：效果从命运条下方揭开（淡入、整体轻微下滑）。
            float y = header.yMax + 4f + (1f - ease) * 5f;
            if (residue.Effects.Count > 0)
            {
                float horizontalInset = spacious ? 14f : 8f;
                CardDrawer.DrawEffectRows(
                    new Rect(panel.x + horizontalInset, y, panel.width - horizontalInset * 2f, panel.yMax - y - 4f),
                    residue.Effects,
                    EffectRowHeight(spacious),
                    EffectRowGap,
                    spacious ? 12 : 10);
            }
        }

        private static void DrawAttachmentConnector(Rect card, Rect attachment)
        {
            // attachment 被屏幕边缘夹回去后，中心点可能已经不再和宿主卡片对齐。
            // 连接到 attachment 顶/底边上离卡片最近的点，仍保持“这是它的结果”的阅读关系。
            float attachmentX = Mathf.Clamp(card.center.x, attachment.xMin + 3f, attachment.xMax - 3f);
            bool attachmentBelow = attachment.center.y >= card.center.y;
            var color = new Color(IMGUIStyles.Gold.r, IMGUIStyles.Gold.g, IMGUIStyles.Gold.b, 0.62f);
            IMGUIStyles.DrawLine(
                new Vector2(card.center.x, attachmentBelow ? card.yMax - 1f : card.yMin + 1f),
                new Vector2(attachmentX, attachmentBelow ? attachment.y + 1f : attachment.yMax - 1f),
                color,
                2f);
        }

        private static void DrawResultAttachmentBackground(Rect panel)
        {
            IMGUIStyles.DrawShadow(panel, new Vector2(4f, 5f), 0.45f);
            var oldColor = GUI.color;
            IMGUIStyles.SetColor(new Color(IMGUIStyles.Ink.r, IMGUIStyles.Ink.g, IMGUIStyles.Ink.b,
                ResultAttachmentOpacity));
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        // 判定面板头部：左上结果/状态标签 + 右上「准备 · 命运骰」明细，供掷骰态与结算态共用。
        private static void DrawRollHeaderText(
            Rect panel,
            string label,
            Color labelColor,
            string detail,
            bool spacious)
        {
            var labelStyle = EnsureRollLabelStyle(spacious);
            labelStyle.normal.textColor = labelColor;
            IMGUIStyles.DrawLabel(new Rect(panel.x + (spacious ? 16f : 8f), panel.y + (spacious ? 8f : 3f),
                spacious ? 180f : 130f, spacious ? 22f : 18f), label, labelStyle);

            if (string.IsNullOrEmpty(detail)) return;
            var detailStyle = EnsureRollDetailStyle(spacious);
            detailStyle.normal.textColor = IMGUIStyles.TextSecondary;
            float detailWidth = spacious ? 230f : 150f;
            float detailInset = spacious ? 16f : 8f;
            IMGUIStyles.DrawLabel(new Rect(panel.xMax - detailWidth - detailInset, panel.y + (spacious ? 9f : 4f),
                detailWidth, spacious ? 20f : 16f), detail, detailStyle);
        }

        private static GUIStyle EnsureRollLabelStyle(bool spacious)
        {
            int fontSize = IMGUIStyles.FontSize(spacious ? 15 : 13);
            var baseStyle = IMGUIStyles.ModalBody;
            if (_rollLabelStyle == null
                || _rollLabelStyle.font != baseStyle.font
                || _rollLabelStyle.fontSize != fontSize)
            {
                _rollLabelStyle = new GUIStyle(baseStyle)
                {
                    fontSize = fontSize,
                };
            }
            return _rollLabelStyle;
        }

        private static GUIStyle EnsureRollDetailStyle(bool spacious)
        {
            int fontSize = IMGUIStyles.FontSize(spacious ? 11 : 10);
            var baseStyle = IMGUIStyles.ModalBody;
            if (_rollDetailStyle == null
                || _rollDetailStyle.font != baseStyle.font
                || _rollDetailStyle.fontSize != fontSize)
            {
                _rollDetailStyle = new GUIStyle(baseStyle)
                {
                    fontSize = fontSize,
                    alignment = TextAnchor.MiddleRight,
                };
            }
            return _rollDetailStyle;
        }

        // ── 标签配色（便签色板，DESIGN.md）──────────────────────────────

        // 纸色一律取 IMGUIStyles 的 Sticky* 令牌（DESIGN.md 便签色板），这里不再自备一份色值：
        // 之前正是因为各画各的，纸底被压暗成中间调，深色卡上字就糊了。
        private static (Color bg, Color ink) NoteWork =>
            (IMGUIStyles.StickyWorkBg, IMGUIStyles.StickyWorkText);
        private static (Color bg, Color ink) NoteMidRisk =>
            (IMGUIStyles.StickyMidRiskBg, IMGUIStyles.StickyMidRiskText);
        private static (Color bg, Color ink) NoteHighRisk =>
            (IMGUIStyles.StickyHighRiskBg, IMGUIStyles.StickyHighRiskText);
        private static (Color bg, Color ink) NoteNegotiate =>
            (IMGUIStyles.StickyNegotiateBg, IMGUIStyles.StickyNegotiateText);
        private static (Color bg, Color ink) NoteOpportunity =>
            (IMGUIStyles.StickyOpportunityBg, IMGUIStyles.StickyOpportunityText);
        private static (Color bg, Color ink) NoteMuted =>
            (IMGUIStyles.StickyMutedBg, IMGUIStyles.StickyMutedText);

        // 同一语义永远同一张纸：工作/低风险=绿纸、中风险=琥珀纸、高风险/交锋/非法=橙红纸、
        // 交涉=蓝纸、机遇=紫纸。
        private static (Color bg, Color text) TagColors(string label, bool disabled = false)
        {
            if (label == "不可休息")
                return NoteHighRisk;

            if (disabled)
                return NoteMuted;

            switch (label)
            {
                case "工作":
                case "低风险":
                    return NoteWork;
                case "中风险":
                    return NoteMidRisk;
                case "交锋":
                case "高风险":
                case "非法":
                    return NoteHighRisk;
                case "机遇":
                    return NoteOpportunity;
                default:
                    return NoteNegotiate;
            }
        }

        private static string FormatOutcome(RollOutcome outcome)
        {
            return outcome switch
            {
                RollOutcome.Success => "判定成功",
                RollOutcome.Neutral => "判定中性",
                RollOutcome.Fail => "判定失败",
                _ => "判定结果"
            };
        }

        private static Color OutcomeColor(RollOutcome outcome)
        {
            return outcome switch
            {
                RollOutcome.Success => IMGUIStyles.OutcomeSuccess,
                RollOutcome.Neutral => IMGUIStyles.OutcomeNeutral,
                RollOutcome.Fail => IMGUIStyles.OutcomeFail,
                _ => IMGUIStyles.TextPrimary
            };
        }
    }
}
