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
    // 标题 / 技能同一行，描述 / 资源槽同一行；所有卡共用测量与命中几何。
    // 资源放入后才显示卡底执行条和右侧概率；结算用同一位置的判定条与独立影响行。
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
            float clockBadgesBottom,
            bool mustHandle,
            ref CardDrawer.CardInteraction interaction)
        {
            bool disabled = node.Disabled;
            if (!disabled && IMGUIInteractionContext.HoverAvailable && ui.IsPointerInside(rect))
                ActionRiskFocus.Offer(node);
            int requiredSlotCount = node.Requires?.Count ?? 0;
            bool hasRequires = requiredSlotCount > 0 && slotted != null && slotted.Count == requiredSlotCount;
            bool isObserve = node.Resolve!.Type == ResolveType.Observe;
            var actors = gameManager.DisplayedSnapshot.Actors;
            bool hasClocks = clockBadgesBottom > rect.y;

            float topWidgetsBottom = hasClocks ? clockBadgesBottom : rect.y;
            float titleY = Mathf.Min(TitleTop(topWidgetsBottom, rect.y), rect.yMax - 32f);
            bool judged = node.Resolve.Type == ResolveType.Roll;
            float skillWidth = judged ? 94f : 0f;
            var titleStyle = new GUIStyle(IMGUIStyles.CardTitle) { alignment=TextAnchor.MiddleLeft,
                wordWrap=false,clipping=TextClipping.Clip };
            float titleWidth=rect.width-24f-skillWidth;
            while(titleStyle.fontSize>IMGUIStyles.FontSize(14) && titleStyle.CalcSize(new GUIContent(node.Name)).x>titleWidth) titleStyle.fontSize--;
            if(disabled) titleStyle.normal.textColor=IMGUIStyles.TextSecondary;
            IMGUIStyles.DrawLabel(new Rect(rect.x+12f,titleY,titleWidth,TitleH),node.Name,titleStyle);
            if(judged) DrawHeaderSkill(new Rect(rect.xMax-100f,titleY,88f,TitleH),node,slotted,gameManager.DisplayedSnapshot,disabled);
            IMGUIStyles.DrawLine(new Vector2(rect.x,titleY+TitleH+4f),new Vector2(rect.xMax,titleY+TitleH+4f),IMGUIStyles.CityPlateLine,1f);
            float bodyY=titleY+TitleH+GapTitleToBody+8f;
            float bodyBottom=rect.yMax-BottomPad-(hasRequires?0f:ExeH);
            if(!string.IsNullOrWhiteSpace(node.Subtitle))
            {
                float width=DescriptionWidth(node,rect.width);
                var style=SubtitleStyle();
                if(disabled) style.normal.textColor=IMGUIStyles.TextSecondary;
                string text=WrapSubtitle(node.Subtitle,style,width);
                IMGUIStyles.DrawLabel(new Rect(rect.x+12f,bodyY,width,Mathf.Max(0f,bodyBottom-bodyY)),text,style);
            }

            // ── 3. 类别 / 风险书签：从卡顶边探出（不遮内容、不占内部空间）。
            // 书签只说「这件事」：内容层写的难度修正 + 类别/风险标签。
            // 伤势/旧伤是「这个人」的事，挂在右缘的人物能力片上（尼尔 0 −1）。
            {
                // 两边的外挂都伸到卡外，所以谁盖谁只由绘制顺序决定；网格里这个顺序会把
                // 右缘的能力片吃掉（见 BeginDeferredEdgeAttachments 的注释）。这里统一收成
                // 一件事交出去，由排版的那一头决定是当场画还是等所有卡身画完再画。
                var attachRect = rect;
                var attachNode = node;
                bool attachDisabled = disabled;
                bool attachMustHandle = mustHandle;
                DrawOrDeferEdgeAttachments(() =>
                {
                    DrawTopTabs(attachRect, attachNode, attachDisabled, attachMustHandle);
                });
            }

            // ── 4. 需求骰位：统一为「方块 Slot」（与手牌骰子/物品同族），居中横排。
            //     骰子 slot = 大字 D/值；物品 slot = 符号 + 数量（强调）+ 下方名称展签。
            //     判定的核心骰左侧贴技能药丸（属性 → 骰子），同一行。
            DrawRequirementSlots(rect, bodyY, bodyBottom, clockBadgesBottom, node, slotted, disabled, ui, gameManager, ref interaction);

            // ── 6. 无需求的卡：执行 / 查看按钮就在正文底（实心金 / 不可用 / 执行中）。
            // 有需求的卡到此为止：它的按钮在卡片下挂的操作台里，由渲染器在附件层画。
            if (hasRequires) return;
            var exeRect = new Rect(rect.center.x - ExeW / 2f, bodyBottom, ExeW, ExeH);
            if (isExecuting)
            {
                DrawExecuteProgress(exeRect, executeProgress, executingText);
            }
            else if (isObserve)
            {
                if (DrawExecuteButton(exeRect, disabled ? "不可用" : node.ButtonText ?? "查 看", ui, !disabled, dead: disabled))
                {
                    interaction.CardClicked = true;
                }
            }
            else if (!disabled)
            {
                if (DrawExecuteButton(exeRect, node.ButtonText ?? "执 行", ui, true))
                    interaction.ExecuteClicked = true;
            }
            else
            {
                DrawExecuteButton(exeRect, "不可用", ui, false, dead: true);
            }

            // ── 7. 卡片级点击（仅无 requires 的非 Instant 类型，如 Observe / Clock）──
            if (!disabled && ui.WasTapped(rect) && node.Resolve!.Type != ResolveType.Instant)
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

        // 卡顶标签按实际宽度换行；测量和绘制使用同一布局。
        private const float TabPadding = 8f;
        private const float TabGap = 6f;
        private const float TabInset = 10f;
        private const float TabRowGap = 5f;
        private const float TabCardGap = 6f;
        private static int TabFontSize() => IMGUIStyles.FontSize(13);
        private static float TabHeight() => TabFontSize() + 10f;

        private static GUIStyle TabStyle() => new GUIStyle(GUI.skin.label)
        {
            font = IMGUIStyles.ChineseFont,
            fontSize = TabFontSize(),
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Clip,
            wordWrap = false,
            padding = new RectOffset(),
            margin = new RectOffset()
        };

        private static List<string> TabItems(GameNode node, bool mustHandle)
        {
            var items = new List<string>();
            if (node.Risk != null) items.Add(node.Risk.Label);
            foreach (var tag in node.Tags)
            {
                if (string.IsNullOrWhiteSpace(tag) || tag == "人物" || tag == "地点") continue;
                string shown = tag switch
                {
                    "工作" or "紧急" or "必须处理" or "高风险" or "低风险" or "交锋" => UiText.Get(tag),
                    _ => tag,
                };
                if (!items.Contains(shown)) items.Add(shown);
            }
            if (node.Resolve != null)
                foreach (var mod in node.Resolve.DifficultyModifiers)
                    items.Add($"{mod.Reason} {(mod.Value > 0 ? "+" : "")}{mod.Value}");
            if (mustHandle && !items.Contains(UiText.Get("必须处理")))
                items.Add(UiText.Get("必须处理"));
            return items;
        }

        private static List<Rect> LayoutTabs(float width, List<string> items, GUIStyle style)
        {
            var rects = new List<Rect>();
            float available = Mathf.Max(1f, width - TabInset * 2f);
            float x = 0f, y = 0f;
            foreach (var item in items)
            {
                float w = Mathf.Min(available, Mathf.Ceil(style.CalcSize(new GUIContent(item)).x) + TabPadding * 2f);
                if (x > 0f && x + w > available)
                {
                    x = 0f;
                    y += TabHeight() + TabRowGap;
                }
                rects.Add(new Rect(TabInset + x, y, w, TabHeight()));
                x += w + TabGap;
            }
            return rects;
        }

        public static float TopTabsRise(GameNode node, float width, bool mustHandle = false)
        {
            var tabs = LayoutTabs(width, TabItems(node, mustHandle), TabStyle());
            return tabs.Count == 0 ? 0f : tabs[tabs.Count - 1].yMax + TabCardGap;
        }

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

        public static void DrawTopTabs(Rect rect, GameNode node, bool disabled, bool mustHandle = false)
        {
            var style = TabStyle();
            var items = TabItems(node, mustHandle);
            var tabs = LayoutTabs(rect.width, items, style);
            if (tabs.Count == 0) return;
            float top = rect.y - tabs[tabs.Count - 1].yMax - TabCardGap;
            for (int i = 0; i < tabs.Count; i++)
            {
                var tab = tabs[i];
                tab.position += new Vector2(rect.x, top);
                tab = UIScale.PixelSnap(tab);
                bool urgent = items[i] == UiText.Get("紧急") || items[i] == UiText.Get("必须处理");
                Color ink = disabled ? IMGUIStyles.TextSecondary
                    : items[i] == "危险" ? IMGUIStyles.OddsFail
                    : items[i] == "冒险" ? IMGUIStyles.Gold
                    : items[i] == "稳妥" ? IMGUIStyles.OddsSuccess
                    : urgent ? IMGUIStyles.Gold : IMGUIStyles.Paper;
                IMGUIStyles.DrawShadow(tab, new Vector2(2f, 2f), .35f);
                IMGUIStyles.SetColor(IMGUIStyles.CityPlateBg);
                GUI.DrawTexture(tab, Texture2D.whiteTexture);
                IMGUIStyles.ResetColor();
                IMGUIStyles.DrawOutline(tab, 1f, (items[i] == "危险" || items[i] == "冒险" || items[i] == "稳妥")
                    ? ink : urgent ? IMGUIStyles.Gold : IMGUIStyles.CityPlateLine);
                style.normal.textColor = ink;
                IMGUIStyles.DrawLabel(new Rect(tab.x + TabPadding, tab.y, tab.width - TabPadding * 2f, tab.height), items[i], style);
            }
        }

        // ── 需求骰位：方块 Slot（与手牌骰子/物品同族）──────────────────

        // 每个 require 画成一个方块 Slot，居中横排。骰子 slot = 大字 D/值；
        // 物品 slot = 符号 + 数量（强调）+ 下方名称展签。判定核心骰上方挂技能药丸。
        // 尺寸不小于手牌方块（56），物品略大以容纳信息。
        // 骰位方块必须和手牌方块一样大——拖过去的东西和接它的坑不一样大，手感立刻就错。
        private static float DieSlot => HandPanelDrawer.TokenSize;
        private static float ItemSlot => DieSlot + 6f;
        private const float SlotGap = 10f;

        private static readonly Color SlotBlockBg = new Color(0.024f, 0.031f, 0.047f, 1f);

        private static float SlotSize(ActionCost req) => req.Type == "die" ? DieSlot : ItemSlot;

        // 方块能缩到多小由「里面装得下什么」决定，不是拍一个比例：
        // 骰子格里是 26px 的大字 + 描边 → 32；物品格要上下叠符号(22px)和数量(15px)两行 → 48。
        // 旧版那个 0.7 比例下限是假保底：die 格的 need=0.7×56+17=56.2 恒 > 触发缩放的 avail(<73)，
        // 也就是说它一被用到就一定还在溢出，只是数字好看一点。下限必须由内容推导才有意义。

        // 骰位区在最压缩状态下仍需要的高度：缩到内容下限的方块；判定核心额外带属性药丸。
        // 副标题的高度预算要先扣掉它——先保住可交互核心，剩下的才给气氛文本。
        /// <summary>
        /// 技能药丸挂在第几个槽上。<b>只有判定卡有</b>：药丸说的是"这颗骰要和哪个属性一起比"，
        /// 非判定的骰子不和任何东西比，硬给它一枚药丸只会让两种卡看起来是同一种。
        /// 非判定由槽位形状去说（圆＝交出去），不在这儿说第二遍。
        /// </summary>
        private static int SkillPillIndex(GameNode node) =>
            node.Resolve != null && node.Resolve.Type == ResolveType.Roll ? FindCoreDieIndex(node) : -1;



        private static void DrawRequirementSlots(
            Rect rect, float bodyY, float exeY, float clocksBottomY, GameNode node, List<SlottedResource?>? slotted,
            bool disabled, IMGUIInteractionContext ui, SSNoirGameManager gameManager,
            ref CardDrawer.CardInteraction interaction)
        {
            var reqs = node.Requires;
            if (reqs == null || reqs.Count == 0) return;
            int pillIndex = SkillPillIndex(node);
            var slots = RequirementSlotRects(rect, bodyY, exeY, clocksBottomY, node, gameManager.DisplayedSnapshot.Actors);
            for (int j = 0; j < reqs.Count; j++)
            {
                DrawSlotBlock(slots[j], node, j, reqs[j], slotted != null ? slotted[j] : null, disabled, ui, gameManager,
                    ref interaction, judged: j == pillIndex);
            }
        }

        // 绘制与投放命中共用实际槽位几何，包括多槽、技能栏与紧凑卡的缩放。
        private static Rect[] RequirementSlotRects(Rect rect, float bodyY, float exeY, float clocksBottomY,
            GameNode node, IReadOnlyList<ActorSnapshot>? actors)
        {
            var reqs=node.Requires!;
            float total=RequirementsWidth(node);
            float x=rect.xMax-12f-total;
            var slots=new Rect[reqs.Count];
            for(int j=0;j<reqs.Count;j++)
            {
                float size=SlotSize(reqs[j]);
                slots[j]=new Rect(x,bodyY+Mathf.Max(0f,(exeY-bodyY-size-(HasItemRequirement(reqs)?CaptionH+CaptionGap+CaptionBottomPad:0f))*.5f),size,size);
                x+=size+SlotGap;
            }
            return slots;
        }

        public static bool ContainsDropSlot(Rect rect, GameNode node, float clocksBottomY,
            Vector2 pointer, SSNoirGameManager gameManager)
        {
            if (node.Disabled || node.Requires == null || node.Requires.Count == 0 || node.Resolve == null)
                return false;
            float titleY = Mathf.Min(TitleTop(clocksBottomY, rect.y), rect.yMax - 32f);
            float bodyBottom = rect.yMax - BottomPad;
            float bodyY = titleY + TitleH + GapTitleToBody + 8f;
            var slots = RequirementSlotRects(rect, bodyY, bodyBottom, clocksBottomY, node, gameManager.DisplayedSnapshot.Actors);
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].Contains(pointer) && gameManager.CanPlaceSelectedResource(node, i))
                    return true;
            return false;
        }



        /// <summary>自动行动的飞行骰需要落到和卡内绘制完全一致的位置。</summary>
        public static Rect AutoActionSlotRect(Rect rect, GameNode node, int slotIndex)
        {
            float titleY=TitleTop(rect.y,rect.y);
            return RequirementSlotRects(rect,titleY+TitleH+GapTitleToBody+8f,rect.yMax-BottomPad,rect.y,node,null)[slotIndex];
        }

        // 卡片纵向流量的唯一定义，绘制（DrawContent）与测量（RecommendedCardHeight）共用同一组常量：
        // 顶部挂件（时钟徽章 / 能力栏）→ 标题 → 副标题 → 骰位（无需求的卡再加一个执行按钮）。
        private const float TitleH = 26f;
        private const float TopPad = 8f;               // 无顶部挂件时标题距卡顶
        private const float GapAfterTopWidgets = 6f;   // 顶部挂件与标题之间
        private const float GapTitleToBody = 6f;       // 标题/副标题与骰位之间
        private const float BottomPad = 8f;
        private const float ExeH = 26f;
        private const float ExeW = 112f;

        // ── 操作台附件：执行按钮（+ 命运预览）────────────────────────
        //
        // 有需求的卡装填了（任一槽位放了东西，或正在执行）才长出操作台，挂在卡片下方，
        // 和判定条 / 结果条是同一个位置、同一种「附件」。它不在卡的矩形之内：卡片本身
        // 不因为放进一颗骰子而变高，排布里的位置也就不会跟着挪。
        // 空卡只有标题、副标题和骰位——待命按钮是个什么都不能做的按钮，不值得常驻。
        private const float ConsolePad = 12f;
        private const float ConsoleInset = 8f;
        private const float ConsoleButtonH = 34f;

        public static bool IsConsoleArmed(GameNode node, IReadOnlyList<SlottedResource?>? slotted, bool isExecuting)
        {
            int required = node.Requires?.Count ?? 0;
            if (required == 0 || slotted == null || slotted.Count != required)
                return false;
            return isExecuting || slotted.Any(sl => sl != null);
        }

        public static Rect OddsPreviewRect(Rect cardRect)
        {
            const float width=130f;
            float x=cardRect.xMax+8f;
            if(x+width>UIScale.SafeArea.xMax-8f) x=cardRect.x-width-8f;
            return new Rect(x,cardRect.y+TitleH+12f,width,110f);
        }

        private static bool ConsoleShowsOdds(GameNode node, IReadOnlyList<SlottedResource?> slotted, IReadOnlyList<ActorSnapshot>? actors)
            => node.Resolve!.Type == ResolveType.Roll && !node.Disabled && actors != null
               && slotted.Any(sl => sl != null && sl.Type == "die");

        private static float ConsoleHeight(bool showOdds)
            => ConsoleButtonH;

        public static Rect ConsoleAttachmentRect(Rect cardRect, GameNode node, IReadOnlyList<SlottedResource?> slotted,
            IReadOnlyList<ActorSnapshot>? actors, float reveal = 1f)
        {
            float width = AttachmentWidth(cardRect.width);
            var panel = KeepAttachmentOnScreen(AttachmentPanelRect(cardRect, width,
                ConsoleHeight(ConsoleShowsOdds(node, slotted, actors)), false));
            panel.height *= Mathf.Clamp01(reveal);
            return panel;
        }

        /// <summary>操作台里那颗执行按钮的位置；渲染器在物品栏之前用它接点击（见 DrawConsoleAttachment）。</summary>
        public static Rect ConsoleExecuteRect(Rect consoleRect)
            => new Rect(consoleRect.x, consoleRect.yMax-ConsoleButtonH,consoleRect.width,ConsoleButtonH);

        /// <summary>
        /// 画操作台附件。返回 true 表示这一帧玩家按下了执行。
        /// 附件层画在物品栏之后，操作台若压在物品栏上，视觉在前而点击会被底下的骰子抢走；
        /// 所以渲染器在画物品栏之前先用 <see cref="ConsoleExecuteRect"/> 接一次点击，
        /// 这里的返回值只在那一步没跑时才有机会为 true。
        /// </summary>
        public static bool DrawConsoleAttachment(
            Rect cardRect, GameNode node, List<SlottedResource?> slotted,
            IMGUIInteractionContext ui, SSNoirGameManager gameManager,
            bool isExecuting, float executeProgress, string executingText, float reveal = 1f)
        {
            var actors = gameManager.DisplayedSnapshot.Actors;
            bool disabled = node.Disabled;
            bool ready = slotted.All(sl => sl != null) && !disabled;
            bool showOdds = ConsoleShowsOdds(node, slotted, actors);
            var panel = ConsoleAttachmentRect(cardRect, node, slotted, actors, reveal);
            if (panel.height <= 0f) return false;
            ui.CanHover(panel);
            var line = ready ? IMGUIStyles.Paper : IMGUIStyles.TextSecondary;
            DrawDrawerBackground(cardRect, panel, line);
            if(showOdds && !isExecuting) TryDrawFatePreview(OddsPreviewRect(cardRect),node,gameManager.DisplayedSnapshot,slotted,actors!,OddsPreviewRect(cardRect).y);
            if (node.Risk != null && !isExecuting)
            {
                var target = node.Risk.RiskTarget ?? node.Risk.ProgressTarget;
                if (target != null)
                {
                    string subject = node.Risk.RiskTarget != null ? "风险" : "进展";
                    var style = new GUIStyle(IMGUIStyles.CardSubtitle)
                    {
                        fontSize = IMGUIStyles.FontSize(12),
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = IMGUIStyles.Paper }
                    };
                    var hint = new Rect(panel.x + 8f, panel.y + 2f, panel.width - 16f, 20f);
                    IMGUIStyles.DrawLabel(hint, $"{node.Risk.Label} · {subject}：{target.DisplayName}", style);
                }
            }

            // 内容从主卡下滑出，裁剪与点击使用同一个可见矩形；展开完才允许执行。
            GUI.BeginGroup(panel);
            var localUi = ui.Translated(panel.position);
            float fullHeight = ConsoleHeight(showOdds);
            var content = new Rect(0f, panel.height - fullHeight, panel.width, fullHeight);

            var exeRect = ConsoleExecuteRect(content);
            bool clicked = false;
            if (isExecuting)
                DrawExecuteProgress(exeRect, executeProgress, executingText);
            else
                clicked = DrawExecuteButton(exeRect, disabled ? "不可用" : ready ? node.ButtonText ?? "执 行" : "待 命",
                    localUi, ready && reveal >= 0.999f, dead: disabled);
            GUI.EndGroup();
            return clicked;
        }

        private static float RequirementsWidth(GameNode node)
        {
            if(node.Requires==null || node.Requires.Count==0) return 0f;
            return node.Requires.Sum(SlotSize)+SlotGap*(node.Requires.Count-1);
        }
        private static float DescriptionWidth(GameNode node,float width)
            => Mathf.Max(48f,width-24f-(RequirementsWidth(node)>0f?RequirementsWidth(node)+12f:0f));

        private static void DrawHeaderSkill(Rect rect,GameNode node,List<SlottedResource?>? slotted,PresentationSnapshot snapshot,bool disabled)
        {
            string skill=node.Resolve!.SkillName;
            string? actorId=slotted?.FirstOrDefault(r=>r?.Type=="die")?.ActorId;
            var actor=snapshot.Actors.FirstOrDefault(a=>a.Id==actorId) ?? snapshot.Actors.FirstOrDefault(a=>a.Role=="protagonist");
            if(actor==null) return;
            if(!actor.Stats.TryGetValue(skill,out int level)) throw new System.InvalidOperationException($"Missing skill {skill} on {actor.Id}");
            if(actor.Role=="protagonist")
            {
                if(snapshot.InjurySkillKey==skill) level+=snapshot.InjurySkillPenalty;
                if(snapshot.ScarModifiers.TryGetValue(skill,out var scar) && scar!=null) level+=scar.Value;
            }
            // 名称与数值共享底板；数值独立成格，让投骰前的能力一眼可读。
            Color ink=disabled?IMGUIStyles.TextDisabled:IMGUIStyles.Paper;
            Color valueColor=disabled?IMGUIStyles.TextDisabled:IMGUIStyles.Paper;
            Color valueFill=disabled?IMGUIStyles.HudBg:level<0?IMGUIStyles.SealRed:level>0
                ?new Color(.25f,.32f,.18f,1f):IMGUIStyles.HudBg;
            IMGUIStyles.SetColor(new Color(ink.r,ink.g,ink.b,disabled?.06f:.14f));
            GUI.DrawTexture(rect,Texture2D.whiteTexture);
            IMGUIStyles.ResetColor();
            IMGUIStyles.DrawOutline(rect,1f,new Color(ink.r,ink.g,ink.b,disabled?.25f:.65f));
            float valueWidth=38f;
            var valueRect=new Rect(rect.xMax-valueWidth,rect.y,valueWidth,rect.height);
            IMGUIStyles.SetColor(valueFill);
            GUI.DrawTexture(valueRect,Texture2D.whiteTexture);
            IMGUIStyles.ResetColor();
            var label=new GUIStyle(IMGUIStyles.CardSubtitle){fontSize=IMGUIStyles.FontSize(14),alignment=TextAnchor.MiddleCenter,
                normal={textColor=ink},wordWrap=false,clipping=TextClipping.Clip};
            IMGUIStyles.DrawLabel(new Rect(rect.x+4f,rect.y,rect.width-valueWidth-8f,rect.height),UiText.SkillName(skill),label);
            var value=new GUIStyle(label){fontSize=IMGUIStyles.FontSize(20),fontStyle=FontStyle.Bold,normal={textColor=valueColor}};
            IMGUIStyles.DrawLabel(valueRect,level>0?$"+{level}":level.ToString(),value);
        }

        private static float TitleTop(float topWidgetsBottom, float cardTop)
            => topWidgetsBottom > cardTop ? topWidgetsBottom + GapAfterTopWidgets : cardTop + TopPad;

        // 副标题实际占多高。测量与绘制必须调同一个函数：以前两边各写一份 Clamp（测量 22~72、
        // 紧凑绘制 18~36），于是「卡按 72 长高、文字只画 36」这类空转和「卡按 36 定高、文字要 72」
        // 这类拥挤会同时存在。
        private static GUIStyle SubtitleStyle() => new(IMGUIStyles.CardSubtitle)
        {
            alignment = TextAnchor.UpperLeft,
            wordWrap = true,
            clipping = TextClipping.Clip
        };

        // Unity IMGUI 的 wordWrap 只会在空白等词界断行。中文副标题通常没有空格，长到卡宽时
        // CalcHeight 仍报一行，而 GUI.Label 又会把整段直接画出卡外。先按实际字宽插入换行，
        // 让测量与绘制使用完全相同的文本；这也适用于夹在中文里的英文长词。
        private static string WrapSubtitle(string text, GUIStyle style, float width)
        {
            // Unity can wrap whitespace-delimited words itself. Inserting a line break
            // at a fixed character width splits English words across lines.
            if (text.IndexOf(' ') >= 0)
                return text;

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
            string subtitle = WrapSubtitle(node.Subtitle, style, DescriptionWidth(node, cardWidth));
            return MeasureSubtitleHeight(subtitle, style, DescriptionWidth(node, cardWidth));
        }

        // 这张动作卡按自身内容该有多高。网格卡与世界投射卡都用它定高——不再有「网格一律 190」
        // 那种与内容无关的固定值，也不套 MinCardHeight：空卡就该矮。
        // 高度只看内容，不看装填状态：操作台是下挂附件，不在卡里。
        public static float RecommendedCardHeight(
            GameNode node,
            float cardWidth,
            IReadOnlyList<ActorSnapshot>? actors)
        {
            if(node.Resolve==null) return UIScale.MinTouchSize;
            float clocks=CardDrawer.MeasureClockBadgesHeight(cardWidth,node.Clocks);
            float top=TitleTop(clocks,0f)+TitleH+GapTitleToBody+8f;
            bool required=node.Requires!=null && node.Requires.Count>0;
            float body=Mathf.Max(MeasureSubtitleHeight(node,cardWidth),required?PreferredRequirementSlotsHeight(node,actors):0f);
            return Mathf.Max(UIScale.MinTouchSize,top+body+BottomPad+(required?0f:ExeH+6f));
        }

        private static float PreferredRequirementSlotsHeight(
            GameNode node,
            IReadOnlyList<ActorSnapshot>? actors)
        {
            var reqs = node.Requires;
            if (reqs == null || reqs.Count == 0)
                return 0f;

            float maxSize = 0f;
            foreach (var req in reqs)
                maxSize = Mathf.Max(maxSize, SlotSize(req));

            float below = HasItemRequirement(reqs) ? CaptionH + CaptionGap + CaptionBottomPad : 0f;
            return maxSize + below;
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
            // 可放置提示属于整张场面的状态，不取决于指针此刻命中了哪张卡。
            // Occluded 只禁止接收输入；模态锁仍关闭全部拖放提示与行为。
            bool holdingResource = !ui.IsLocked && gameManager.SelectedResource != null;
            bool canPlaceHeld = holdingResource && !disabled
                && gameManager.CanPlaceSelectedResource(node, slotIndex);
            bool canDropHeld = canPlaceHeld && !ui.IsOccluded;
            Color border = SlotBorderColor(disabled, canPlaceHeld, holdingResource, slotHover);
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
            if (!disabled && (filled || canPlaceHeld))
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
                canPlaceHeld ? (slotHover ? 3f : 2f)
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
                : filled ? IMGUIStyles.Paper : SlotPlaceholderColor(canPlaceHeld, holdingResource);

            // 方块被压缩时（见 DrawRequirementSlots 的 scale）内部字号与留白按同一比例跟着走。
            // 否则物品格缩到 48px 时，固定偏移的符号(y+8 高26)和数量(yMax-24 高20)会直接叠在一起。
            // k=1 时与原本的固定数值完全一致，正常尺寸下无任何变化。
            float k = rect.height / SlotSize(req);

            if (req.Type == "die")
            {
                if (filled)
                {
                    HandPanelDrawer.DrawDieFace(rect, res!.Value, disabled,
                        borderOverride: canPlaceHeld ? border : (Color?)null, octagon: !judged);
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

        // 判定骰左侧的技能列：技能名是有底、有框的主标签；人物姓名与能力值在下面左右分列，
        // 使用白字提高可读性，同时保持无底，避免和技能名标签混为一体。

        // 姓名和数值加大后仍保留足够行高，避免 MiSans 字形被 IMGUI 基线裁切。






        private static string ItemSymbol(string name)
        {
            return name switch
            {
                "金钱" => "$",
                "酒" => UiText.Get("酒"),
                "药品" => UiText.Get("药"),
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

        private static Color SlotBorderColor(bool disabled, bool canDropHeld, bool holdingResource, bool hover)
        {
            if (canDropHeld) return IMGUIStyles.Gold;
            if (holdingResource) return IMGUIStyles.SealRed;
            if (disabled) return new Color(IMGUIStyles.DisabledWash.r, IMGUIStyles.DisabledWash.g, IMGUIStyles.DisabledWash.b, 0.70f);
            return hover
                ? new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 1f)
                : new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f);
        }

        private static Color SlotPlaceholderColor(bool canDropHeld, bool holdingResource)
        {
            if (canDropHeld) return IMGUIStyles.Gold;
            if (holdingResource) return IMGUIStyles.SealRed;
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
            string shownText = text switch
            {
                "执 行" => UiText.Get("执 行"),
                "查 看" => UiText.Get("查 看"),
                "不可用" => UiText.Get("不可用"),
                "待 命" => UiText.Get("待 命"),
                _ => text,
            };
            IMGUIStyles.DrawLabel(rect, shownText, style);

            return isClicked;
        }

        // 执行中：Ink 底 + 金填充进度 + 金描边 + 金光呼吸（「正在发生」）。
        private static void DrawExecuteProgress(Rect rect, float progress, string text)
        {
            progress = Mathf.Clamp01(progress);
            IMGUIStyles.SetColor(IMGUIStyles.Ink);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            var fillRect = new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * progress, rect.height - 4f);
            IMGUIStyles.SetColor(new Color(IMGUIStyles.Gold.r,IMGUIStyles.Gold.g,IMGUIStyles.Gold.b,.22f));
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
            IMGUIStyles.ResetColor();
            IMGUIStyles.DrawOutline(rect, 1f, IMGUIStyles.Gold);
            IMGUIStyles.DrawGoldPulse(rect);

            string label = string.IsNullOrEmpty(text) ? "执行中" : text;
            if (label.Length > 5) label = "执行中";
            if (label == "执行中" || label == "判定中") label = UiText.Get(label);
            var progressStyle = new GUIStyle(IMGUIStyles.ExecuteLabel);
            progressStyle.normal.textColor = IMGUIStyles.Gold;
            IMGUIStyles.DrawLabel(rect, label, progressStyle);
        }

        // ── 底部：概率预览 ────────────────────────────────────────

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
            text.Append(UiText.Get("准备值 ")).Append(prepared)
                .Append(UiText.Get(" ＝ 骰 ")).Append(dieValue);
            if (skillLevel != 0)
                text.Append(skillLevel > 0 ? " + " : " − ")
                    .Append(UiText.SkillName(skill)).Append(' ').Append(Mathf.Abs(skillLevel));
            if (modSum != 0)
                text.Append(modSum > 0
                    ? UiText.Get(" + 修正 ")
                    : UiText.Get(" − 修正 "))
                    .Append(Mathf.Abs(modSum));
            return text.ToString();
        }

        private static void DrawFateStrip(Rect rect, RollOutcome[] strip, float executeBottomY, string caption)
        {
            var style=new GUIStyle(IMGUIStyles.CardSubtitle){fontSize=IMGUIStyles.FontSize(10),alignment=TextAnchor.MiddleLeft};
            IMGUIStyles.DrawLabel(new Rect(rect.x,executeBottomY,rect.width,20f),caption,style);
            DrawOddsTiles(new Rect(rect.x,executeBottomY+22f,rect.width,84f),strip);
        }

        // 非零档位按概率比例分配宽度；间隔不计入概率面积，标签随可用宽度缩小字号。
        private static void DrawOddsTiles(Rect area, RollOutcome[] strip)
        {
            var outcomes=new[]{RollOutcome.Success,RollOutcome.Neutral,RollOutcome.Fail};
            float y=area.y;
            foreach(var outcome in outcomes)
            {
                int count=strip.Count(x=>x==outcome);
                if(count==0) continue;
                var row=new Rect(area.x,y,area.width,26f);
                var color=OutcomeColor(outcome);
                IMGUIStyles.SetColor(IMGUIStyles.CityPlateBg); GUI.DrawTexture(row,Texture2D.whiteTexture); IMGUIStyles.ResetColor();
                IMGUIStyles.DrawOutline(row,1f,new Color(color.r,color.g,color.b,.7f));
                var style=new GUIStyle(IMGUIStyles.CardSubtitle){fontSize=IMGUIStyles.FontSize(12),alignment=TextAnchor.MiddleCenter,normal={textColor=color}};
                IMGUIStyles.DrawLabel(row,UiText.OutcomeOdds(outcome,count),style);
                y+=29f;
            }
        }

        // 按百分比画连续三段：每段面积等于真实概率，结算只突出结果档位。
        // 不再画六个命运骰面，也不显示内部随机抽样值。
        public static void DrawOddsStrip(Rect area, RollOutcome[] strip,
            RollOutcome? highlightedOutcome = null, float pulse = 0f, bool settled = false)
        {
            if (strip == null || strip.Length != 100)
                throw new System.ArgumentException("Expected 100 percentage entries.", nameof(strip));

            int fail = 0, neutral = 0, success = 0;
            foreach (var value in strip)
            {
                switch (value)
                {
                    case RollOutcome.Fail: fail++; break;
                    case RollOutcome.Neutral: neutral++; break;
                    case RollOutcome.Success: success++; break;
                    default: throw new System.ArgumentOutOfRangeException(nameof(strip));
                }
            }

            var portions = new[]
            {
                (Outcome: RollOutcome.Fail, Count: fail),
                (Outcome: RollOutcome.Neutral, Count: neutral),
                (Outcome: RollOutcome.Success, Count: success),
            };
            int visibleCount = (fail > 0 ? 1 : 0) + (neutral > 0 ? 1 : 0) + (success > 0 ? 1 : 0);
            const float gap = 3f;
            float available = Mathf.Max(0f, area.width - gap * Mathf.Max(0, visibleCount - 1));
            float x = area.x;
            int drawn = 0;
            foreach (var portion in portions)
            {
                if (portion.Count == 0) continue;
                if (drawn++ > 0) x += gap;
                float width = available * portion.Count / 100f;
                bool highlighted = highlightedOutcome.HasValue && highlightedOutcome.Value == portion.Outcome;
                float pop = highlighted ? pulse * 4f : 0f;
                var cell = new Rect(x - pop * 0.5f, area.y - pop,
                    width + pop, area.height + pop * 2f);
                Color tier = OutcomeColor(portion.Outcome);
                float brightness = highlighted ? 1f : settled ? 0.34f : 0.78f;
                IMGUIStyles.SetColor(new Color(tier.r * brightness, tier.g * brightness,
                    tier.b * brightness, 1f));
                GUI.DrawTexture(cell, Texture2D.whiteTexture);
                IMGUIStyles.ResetColor();
                if (highlighted)
                    IMGUIStyles.DrawOutline(cell, 2f, IMGUIStyles.Gold);
                x += width;
            }
        }

        // ── 判定 / 结算附件 ───────────────────────────────────────────

        private const float AttachmentGap = 6f;
        private const float ResultAttachmentOpacity = 1f;
        // Attachment 不参与宿主卡片的 footprint，但仍是玩家要读的界面内容，不能被
        // SafeArea 外沿裁掉。这个 inset 只约束 attachment 自己，不会反过来推动卡片。
        private const float AttachmentSafeInset = 8f;

        private static GUIStyle? _rollLabelStyle;
        private static GUIStyle? _rollDetailStyle;

        private static float AttachmentWidth(float cardWidth, bool above = false)
        {
            float desired = Mathf.Max(above ? 220f : 1f, cardWidth - ConsoleInset * 2f);
            float available = Mathf.Max(1f, UIScale.SafeArea.width - AttachmentSafeInset * 2f);
            return Mathf.Min(desired, available);
        }

        private static Rect FullResidueAttachmentRect(Rect cardRect, CardPresentationResidue residue, bool above)
        {
            float panelWidth = AttachmentWidth(cardRect.width, above);
            float panelHeight = ResiduePanelHeight(residue, panelWidth);
            return KeepAttachmentOnScreen(AttachmentPanelRect(cardRect, panelWidth, panelHeight, above));
        }

        public static Rect ResidueAttachmentRect(Rect cardRect, CardPresentationResidue residue, bool above = false)
        {
            var panel = FullResidueAttachmentRect(cardRect, residue, above);
            float height = panel.height * residue.DrawerRemaining01;
            if (above) panel.y += panel.height - height;
            panel.height = height;
            return panel;
        }

        // 附件默认挂在卡下；宿主贴着屏幕底边时挂在卡上。
        private static Rect AttachmentPanelRect(Rect cardRect, float panelWidth, float panelHeight, bool above)
            => new Rect(
                cardRect.center.x - panelWidth / 2f,
                above ? cardRect.y - AttachmentGap - panelHeight : cardRect.yMax + AttachmentGap,
                panelWidth,
                panelHeight);

        // 宿主此刻不在屏幕上时的替身：屏幕中央上方一块只写动作名的牌子，附件照常挂在它下面。
        // 返回它的矩形，供 DrawAttachmentOverlay 当宿主用。
        public static Rect DrawFloatingHost(string actionName)
        {
            bool spacious = UIScale.SafeArea.width >= 700f;
            float w = spacious ? 300f : 220f;
            float h = spacious ? 44f : 36f;
            var rect = new Rect(UIScale.SafeArea.center.x - w / 2f, UIScale.SafeArea.y + UIScale.SafeArea.height * 0.28f, w, h);
            IMGUIStyles.DrawShadow(rect, new Vector2(4f, 5f), 0.45f);
            GUI.color = IMGUIStyles.Ink;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(rect, 1f, IMGUIStyles.Paper);
            var style = new GUIStyle(IMGUIStyles.SectionLabel)
            {
                fontSize = IMGUIStyles.FontSize(spacious ? 18 : 16),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = IMGUIStyles.Paper }
            };
            IMGUIStyles.ApplyStrongFont(style);
            IMGUIStyles.DrawLabel(rect, actionName, style);
            return rect;
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
        // 结果条不接点击：停留的收起由渲染器按倒计时（或任意一次抬手）统一处理。
        public static void DrawAttachmentOverlay(
            Rect cardRect,
            ActionReport? localRoll,
            float localRollProgress,
            CardPresentationResidue? residue,
            IMGUIInteractionContext ui,
            bool above = false)
        {
            if (localRoll != null)
            {
                DrawLocalRoll(cardRect, localRoll, localRollProgress, ui, above);
                return;
            }
            if (residue != null)
                DrawResiduePanel(cardRect, residue, ui, above);
        }

        private const float ResultHeaderHeight = 34f;
        private const float EffectRowHeight = 26f;
        private const float EffectRowGap = 5f;

        // 判定沿用执行前的抽屉：概率与算式原位不变，只有按钮成为同尺寸的进度条。
        private static void DrawLocalRoll(
            Rect rect,
            ActionReport report,
            float progress,
            IMGUIInteractionContext ui,
            bool above)
        {
            var panel = KeepAttachmentOnScreen(AttachmentPanelRect(rect,
                AttachmentWidth(rect.width, above), ConsoleHeight(true), above));
            ui.CanHover(panel);
            DrawDrawerBackground(rect, panel, IMGUIStyles.Paper);

            DrawExecuteProgress(ConsoleExecuteRect(panel), progress, "判定中");
        }

        private static float ResiduePanelHeight(CardPresentationResidue residue, float panelWidth)
        {
            float height = ResultHeaderHeight;
            bool hasBody = residue.Effects.Count > 0;
            if (hasBody)
            {
                height += 9f; // 结果行揭开的下移行程
                float horizontalInset = ConsolePad;
                height += CardDrawer.MeasureEffectRowsHeight(
                    residue.Effects,
                    panelWidth - horizontalInset * 2f,
                    EffectRowHeight,
                    EffectRowGap,
                    13);
            }
            return height + ConsolePad; // 底部留出停留时间条，不压住最后一行
        }

        // 判定结束后，同一抽屉切为结果标题与全部影响行，底边继续显示原有停留时间条。
        private static void DrawResiduePanel(
            Rect rect,
            CardPresentationResidue residue,
            IMGUIInteractionContext ui,
            bool above)
        {
            // residue 只有动画落定后才被绘制——首帧即结果该「揭开」的时刻，惰性记录起点。
            if (residue.RevealStartTime <= 0f)
                residue.RevealStartTime = Time.unscaledTime;
            float reveal = Mathf.Clamp01((Time.unscaledTime - residue.RevealStartTime) / CardPresentationResidue.RevealSeconds);
            float ease = 1f - Mathf.Pow(1f - reveal, 3f);

            var fullPanel = FullResidueAttachmentRect(rect, residue, above);
            var panel = ResidueAttachmentRect(rect, residue, above);
            if (panel.height <= 0f) return;
            // 结果只保留判定条和独立的影响行。

            // 内容随抽屉朝宿主移动，裁剪与命中都跟随可见高度，收回时不压缩文字。
            GUI.BeginGroup(panel);
            var content = new Rect(0f, above ? 0f : panel.height - fullPanel.height,
                panel.width, fullPanel.height);
            var header = new Rect(content.x, content.y, content.width, ResultHeaderHeight);
            bool hasOutcome = residue.RollOutcome.HasValue;
            RollOutcome outcome = residue.RollOutcome ?? RollOutcome.Neutral;
            Color oc = hasOutcome ? OutcomeColor(outcome) : IMGUIStyles.Gold;
            IMGUIStyles.SetColor(IMGUIStyles.CityPlateBg);
            GUI.DrawTexture(header,Texture2D.whiteTexture);
            IMGUIStyles.ResetColor();
            IMGUIStyles.DrawOutline(header, 1f, new Color(oc.r, oc.g, oc.b, 0.8f));

            // 判定卡的头是档位；即时卡没有档位可写，头就只是一个「已结算」的标记——
            // 卡就在上面，这里不重复卡名。
            string headerLabel = hasOutcome ? FormatOutcome(outcome)
                : UiText.Get("行动结果");
            DrawRollHeaderText(header, headerLabel, hasOutcome ? oc : IMGUIStyles.TextPrimary,
                "");

            // 结算后让实际收益与代价成为主体，不再重复整根概率条。
            float y = header.yMax + 8f + (1f - ease) * 5f;
            if (residue.Effects.Count > 0)
            {
                float horizontalInset = ConsolePad;
                CardDrawer.DrawEffectRows(
                    new Rect(content.x + horizontalInset, y, content.width - horizontalInset * 2f, content.yMax - y - 4f),
                    residue.Effects,
                    EffectRowHeight,
                    EffectRowGap,
                    13);
            }

            GUI.EndGroup();

        }

        private static void DrawDrawerBackground(Rect card, Rect panel, Color border)
        {
            IMGUIStyles.DrawShadow(panel, new Vector2(2f, 3f), 0.32f);
            IMGUIStyles.SetColor(IMGUIStyles.CityPlateBg);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            IMGUIStyles.ResetColor();
            IMGUIStyles.DrawOutline(panel, 1f, new Color(border.r, border.g, border.b, 0.45f));
            // 下挂时上缘沉进主卡：两侧短暗影强调抽屉归属，不另拉连接线。
            if (Mathf.Abs(panel.y - card.yMax) < 1f)
            {
                IMGUIStyles.SetColor(new Color(0f, 0f, 0f, 0.28f));
                GUI.DrawTexture(new Rect(panel.x + 1f, panel.y, panel.width - 2f, 3f), Texture2D.whiteTexture);
                IMGUIStyles.ResetColor();
            }
        }

        // 抽屉头部：结果/状态在左，准备值在右；判定与结算共用。
        private static void DrawRollHeaderText(
            Rect panel,
            string label,
            Color labelColor,
            string detail)
        {
            var labelStyle = new GUIStyle(EnsureRollLabelStyle())
            {
                wordWrap = false,
                clipping = TextClipping.Clip,
                normal = { textColor = labelColor },
                alignment = TextAnchor.MiddleCenter
            };
            float detailWidth = string.IsNullOrEmpty(detail) ? 0f : 78f;
            float labelWidth = panel.width - ConsolePad * 2f - detailWidth;
            while (labelStyle.fontSize > IMGUIStyles.FontSize(12)
                   && labelStyle.CalcSize(new GUIContent(label)).x > labelWidth)
                labelStyle.fontSize--;
            IMGUIStyles.DrawLabel(new Rect(panel.x + ConsolePad, panel.y + 7f,
                labelWidth, 24f), label, labelStyle);
            if (detailWidth == 0f) return;
            var detailStyle = EnsureRollDetailStyle();
            detailStyle.normal.textColor = IMGUIStyles.TextSecondary;
            IMGUIStyles.DrawLabel(new Rect(panel.xMax - detailWidth - ConsolePad, panel.y + 10f,
                detailWidth, 20f), detail, detailStyle);
        }

        private static GUIStyle EnsureRollLabelStyle()
        {
            int fontSize = IMGUIStyles.FontSize(16);
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

        private static GUIStyle EnsureRollDetailStyle()
        {
            int fontSize = IMGUIStyles.FontSize(11);
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

        private static string FormatOutcome(RollOutcome outcome)
        {
            return outcome switch
            {
                RollOutcome.Success => UiText.Get("成功"),
                RollOutcome.Neutral => UiText.Get("部分成功"),
                RollOutcome.Fail => UiText.Get("失败"),
                _ => UiText.Get("判定结果")
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
