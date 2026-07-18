#nullable enable
using System.Collections.Generic;
using System.Linq;
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
    // 判定完成态：命运预览换结算行，结果以旋转的章形盖印呈现（成=金，败=印章红）。
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
            ref CardDrawer.CardInteraction interaction)
        {
            bool disabled = node.Disabled;
            bool isRoll = node.Resolve!.Type == ResolveType.Roll;
            int requiredSlotCount = node.Requires?.Count ?? 0;
            bool hasRequires = requiredSlotCount > 0 && slotted != null && slotted.Count == requiredSlotCount;
            bool isObserve = node.Resolve!.Type == ResolveType.Observe;
            var actors = gameManager.DisplayedSnapshot.Actors;
            bool showOdds = isRoll && !disabled && hasRequires && actors != null && localRoll == null && residue == null;
            var effectiveModifiers = node.Resolve.DifficultyModifiers;
            bool hasClocks = clockBadgesBottom > rect.y;

            // 结算面板优先级最高，会盖住其下的一切（执行按钮本来就画在它底下——这是既有约定）。
            // 先把它的位置算出来，标题/副标题才能知道自己会不会被切成半截字：宁可整条不画，
            // 也不留一道露在面板上沿的字头。
            float contentBottom = residue != null ? ResiduePanelRect(rect, residue).y : rect.yMax;

            // ── 1. 标题区（居中）── clockBadgesBottom 是 CardDrawer 量出的时钟徽章实际底部，
            // 徽章换行也不会被标题压住（旧版固定 44f 只够单行徽章用）。
            // 徽章多到几乎吃满卡高时（如同一节点同时挂 5 个时钟）也不能任由标题被推出卡底——
            // 宁可让标题少量压在徽章区之上，也不让文字画到卡外面去。
            float titleY = Mathf.Min(hasClocks ? clockBadgesBottom + 6f : rect.y + 12f, rect.yMax - 32f);
            if (titleY + 26f <= contentBottom)
            {
                var titleStyle = new GUIStyle(IMGUIStyles.CardTitle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip
                };
                if (disabled) titleStyle.normal.textColor = IMGUIStyles.TextSecondary;
                GUI.Label(new Rect(rect.x + 10f, titleY, rect.width - 20f, 26f), node.Name, titleStyle);
            }

            // ── 2. 三列分区的纵向边界 ──
            // 执行按钮贴底；showOdds 时再往下留出命运六面预览的空间。
            // 骰位区要先于副标题定下预算，所以 exeY / coreDieIndex 都提到副标题之前算。
            float exeH = 26f;
            float exeY = rect.yMax - (showOdds || localRoll != null || residue != null ? 62f : 34f);
            int coreDieIndex = isRoll ? FindCoreDieIndex(node) : -1;

            // 副标题是气氛文本，骰位是这张卡的可交互核心（DESIGN.md 的「属性 → 骰子」纵列才是
            // 判定卡主体）。空间不足时让位的必须是副标题——旧写法把它无条件 Clamp 到 18~36，
            // 完全不看下游还需不需要空间，于是「一艘货船搁浅」这类 32 字副标题 + 2 时钟的真实
            // 节点在 340×190 上把骰位挤到执行按钮上。现在改成从「预留完骰位最小需求后剩下的
            // 空间」里分配：放不下就压到一行，连一行都放不下就整条收起。
            float subtitleBottomY = titleY + 26f;
            if (!string.IsNullOrWhiteSpace(node.Subtitle))
            {
                var subtitleStyle = new GUIStyle(IMGUIStyles.CardSubtitle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                    clipping = TextClipping.Clip
                };
                if (disabled) subtitleStyle.normal.textColor = IMGUIStyles.TextSecondary;
                float subtitleWidth = rect.width - 24f;
                float measuredHeight = subtitleStyle.CalcHeight(new GUIContent(node.Subtitle), subtitleWidth);
                float subtitleHeight = Mathf.Clamp(measuredHeight, 18f, 36f);

                float subtitleBudget = exeY - (titleY + 26f + 6f) - RequirementSlotsMinNeed(node, coreDieIndex);
                if (subtitleHeight > subtitleBudget)
                    subtitleHeight = subtitleBudget >= 18f ? 18f : 0f;

                if (subtitleHeight > 0f && titleY + 26f + subtitleHeight <= contentBottom)
                    GUI.Label(new Rect(rect.x + 12f, titleY + 26f, subtitleWidth, subtitleHeight), node.Subtitle, subtitleStyle);
                subtitleBottomY = titleY + 26f + subtitleHeight;
            }

            float bodyY = subtitleBottomY + 6f;

            // ── 3. 类别 / 风险便签：骑在卡片左边缘外（不遮内容、不占内部空间）。
            DrawEdgeTags(rect, node, effectiveModifiers, disabled);

            // ── 4. 需求骰位：统一为「方块 Slot」（与手牌骰子/物品同族），居中横排。
            //     骰子 slot = 大字 D/值；物品 slot = 符号 + 数量（强调）+ 下方名称展签。
            //     判定的核心骰上方挂技能药丸（属性 → 骰子）。
            DrawRequirementSlots(rect, bodyY, exeY, clockBadgesBottom, node, slotted, coreDieIndex, disabled, ui, gameManager, ref interaction);

            // ── 6. 执行 / 查看按钮（实心金 / 待命 / 不可用 / 执行中）──
            var exeRect = new Rect(rect.x + (rect.width - 112f) / 2f, exeY, 112f, exeH);
            bool allFilled = requiredSlotCount == 0 || (slotted != null && slotted.Count == requiredSlotCount && slotted.All(s => s != null));
            if (isExecuting)
            {
                DrawExecuteProgress(exeRect, executeProgress, executingText);
            }
            else if (isObserve)
            {
                if (DrawExecuteButton(exeRect, disabled ? "不可用" : "查 看", ui, !disabled))
                {
                    interaction.CardClicked = true;
                    Event.current.Use();
                }
            }
            else if (allFilled && !disabled)
            {
                if (DrawExecuteButton(exeRect, "执 行", ui, true))
                    interaction.ExecuteClicked = true;
            }
            else
            {
                DrawExecuteButton(exeRect, disabled ? "不可用" : "待 命", ui, false);
            }

            // ── 7. 右上角：角色能力栏（规范色：Ink 底 + Paper 描边 + 白字，无主题色）──
            if (isRoll && actors != null && actors.Count > 0)
                DrawActorAbilityRail(rect, node.Resolve!.SkillName, actors, hasClocks ? clockBadgesBottom + 4f : rect.y + 6f);

            // ── 8. 底部：命运预览 / 判定中 / 结算行 + 盖印 ──
            if (localRoll != null)
            {
                DrawLocalRoll(rect, localRoll, localRollPhase, localRollDisplayDieValue, localRollDisplayScale);
            }
            else if (residue != null)
            {
                DrawResiduePanel(rect, residue);
            }
            else if (showOdds)
            {
                TryDrawFatePreview(rect, node.Resolve!.SkillName, slotted!, effectiveModifiers, actors!, exeRect.yMax);
            }

            // ── 9. 卡片级点击（仅无 requires 的非 Instant 类型，如 Observe / Clock）──
            if (!disabled && ui.WasClicked(rect) && !hasRequires && node.Resolve!.Type != ResolveType.Instant)
            {
                interaction.CardClicked = true;
                Event.current.Use();
            }
        }

        // ── 边缘便签：类别 / 风险，骑在卡片左边缘外 ────────────────────

        // 同族彩纸便签，贴在卡左边缘外，文字竖排；自上而下堆叠。
        // 不占卡内空间，也不遮主要元素——「贴在物件上的彩色便签」隐喻更足。
        // 卡矮或便签多时会堆不下：之前放不下就直接不画，玩家看不出还有没显示的标签/修正项——
        // 现在放不下的部分折成一张「+N」提示条，信息不会被悄悄藏起来。
        private static void DrawEdgeTags(Rect rect, GameNode node, List<DifficultyModifierInfo> effectiveModifiers, bool disabled)
        {
            const float tagW = 26f;
            const float tagH = 72f;
            const float tagStep = tagH + 6f;
            float tagX = rect.x - 13f;
            float y = rect.y + 22f;
            float maxY = rect.yMax - 40f;
            var tagStyle = new GUIStyle(GUI.skin.label)
            {
                font = IMGUIStyles.ChineseFont,
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip
            };

            var items = new List<(string text, Color bg, Color textColor)>();
            foreach (var mod in effectiveModifiers)
            {
                string modText = $"{mod.Reason} {(mod.Value > 0 ? "+" : "")}{mod.Value}";
                Color noteBg = disabled
                    ? new Color(0.18f, 0.19f, 0.22f, 1f)
                    : mod.Value < 0 ? new Color(0.82f, 0.58f, 0.48f, 1f)
                    : (mod.Value > 0 ? new Color(0.66f, 0.74f, 0.50f, 1f) : new Color(0.58f, 0.68f, 0.80f, 1f));
                Color noteText = disabled
                    ? new Color(0.55f, 0.56f, 0.60f, 1f)
                    : mod.Value < 0 ? new Color(0.30f, 0.07f, 0.03f, 1f)
                    : (mod.Value > 0 ? new Color(0.10f, 0.18f, 0.05f, 1f) : new Color(0.05f, 0.12f, 0.22f, 1f));
                items.Add((modText, noteBg, noteText));
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

            for (int i = 0; i < items.Count; i++)
            {
                float tagY = y + i * tagStep;
                if (tagY + tagH > maxY)
                {
                    var overflowBg = new Color(0.30f, 0.31f, 0.34f, 1f);
                    var overflowText = new Color(0.82f, 0.83f, 0.86f, 1f);
                    DrawVerticalTag(new Rect(tagX, tagY, tagW, tagH), $"+{items.Count - i}", overflowBg, overflowText, tagStyle);
                    return;
                }
                var item = items[i];
                DrawVerticalTag(new Rect(tagX, tagY, tagW, tagH), item.text, item.bg, item.textColor, tagStyle);
            }
        }

        private static void DrawStaticTag(Rect rect, string text, Color bg, Color textColor, GUIStyle baseStyle)
        {
            GUI.color = bg;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(textColor.r, textColor.g, textColor.b, 0.55f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 3f, rect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var style = new GUIStyle(baseStyle)
            {
                font = IMGUIStyles.ChineseFont,
                normal = { textColor = textColor }
            };
            style.hover.textColor = textColor;
            style.active.textColor = textColor;
            style.focused.textColor = textColor;
            style.onNormal.textColor = textColor;
            style.onHover.textColor = textColor;
            style.onActive.textColor = textColor;
            style.onFocused.textColor = textColor;
            GUI.Label(rect, text, style);
        }

        private static void DrawVerticalTag(Rect rect, string text, Color bg, Color textColor, GUIStyle baseStyle)
        {
            GUI.color = bg;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(textColor.r, textColor.g, textColor.b, 0.55f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 3f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var style = new GUIStyle(baseStyle)
            {
                font = IMGUIStyles.ChineseFont,
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = textColor }
            };
            style.hover.textColor = textColor;
            style.active.textColor = textColor;
            style.focused.textColor = textColor;
            style.onNormal.textColor = textColor;
            style.onHover.textColor = textColor;
            style.onActive.textColor = textColor;
            style.onFocused.textColor = textColor;

            string compact = text.Replace(" ", "");
            int count = Mathf.Max(1, compact.Length);
            float lineH = Mathf.Min(16f, rect.height / count);
            float totalH = lineH * count;
            float startY = rect.y + (rect.height - totalH) * 0.5f;

            for (int i = 0; i < compact.Length; i++)
            {
                GUI.Label(new Rect(rect.x, startY + i * lineH, rect.width, lineH), compact[i].ToString(), style);
            }
        }

        // ── 需求骰位：方块 Slot（与手牌骰子/物品同族）──────────────────

        // 每个 require 画成一个方块 Slot，居中横排。骰子 slot = 大字 D/值；
        // 物品 slot = 符号 + 数量（强调）+ 下方名称展签。判定核心骰上方挂技能药丸。
        // 尺寸不小于手牌方块（56），物品略大以容纳信息。
        private const float DieSlot    = 56f;
        private const float ItemSlot   = 62f;
        private const float SlotGap    = 14f;
        private const float PillH      = 18f;
        private const float PillGap    = 8f;
        private const float CaptionH   = 14f;
        private const float CaptionGap = 3f;
        private static readonly Color SlotBlockBg = new Color(0.024f, 0.031f, 0.047f, 1f);

        private static float SlotSize(ActionCost req) => req.Type == "die" ? DieSlot : ItemSlot;

        // 方块能缩到多小由「里面装得下什么」决定，不是拍一个比例：
        // 骰子格里是 26px 的大字 + 描边 → 32；物品格要上下叠符号(22px)和数量(15px)两行 → 48。
        // 旧版那个 0.7 比例下限是假保底：die 格的 need=0.7×56+17=56.2 恒 > 触发缩放的 avail(<73)，
        // 也就是说它一被用到就一定还在溢出，只是数字好看一点。下限必须由内容推导才有意义。
        private static float SlotMinSize(ActionCost req) => req.Type == "die" ? 32f : 48f;

        // 骰位区在最压缩状态下仍需要的高度：缩到内容下限的方块 + 展签行（属性名/物品名在这一行）。
        // 副标题的高度预算要先扣掉它——先保住可交互核心，剩下的才给气氛文本。
        private static float RequirementSlotsMinNeed(GameNode node, int coreDieIndex)
        {
            var reqs = node.Requires;
            if (reqs == null || reqs.Count == 0) return 0f;

            ActionCost biggest = reqs[0];
            for (int j = 1; j < reqs.Count; j++)
                if (SlotSize(reqs[j]) > SlotSize(biggest)) biggest = reqs[j];

            float belowH = AnyCaption(node, reqs, coreDieIndex, showPill: false) ? CaptionRowH : 0f;
            return SlotMinSize(biggest) + belowH;
        }

        private static void DrawRequirementSlots(
            Rect rect, float bodyY, float exeY, float clocksBottomY, GameNode node, List<SlottedResource?>? slotted,
            int coreDieIndex, bool disabled, IMGUIInteractionContext ui, SSNoirGameManager gameManager,
            ref CardDrawer.CardInteraction interaction)
        {
            var reqs = node.Requires;
            if (reqs == null || reqs.Count == 0) return;

            int n = reqs.Count;
            ActionCost biggest = reqs[0];
            for (int j = 1; j < n; j++)
                if (SlotSize(reqs[j]) > SlotSize(biggest)) biggest = reqs[j];
            float maxSize = SlotSize(biggest);

            // 让位顺序的依据：判定属性名是这张卡的核心信息（DESIGN.md「属性 → 骰子」纵列），
            // 任何压缩下都不能丢——空间够就挂成核心骰上方的药丸，不够就降级成下方展签。
            // 展签行是所有 slot 共用的一行：它比药丸矮 9px，而且次要展签（物品名 /「骰子」）
            // 本来就占着这一行，属性名挤进去不额外花钱。所以「保留药丸、砍掉次要展签」这个
            // 中间档没有意义（比降级成展签更高、信息还更少），不设该档。
            float avail = Mathf.Max(0f, exeY - bodyY);
            bool hasCore = coreDieIndex >= 0;
            float belowIfPill = AnyCaption(node, reqs, coreDieIndex, showPill: true) ? CaptionRowH : 0f;
            bool showPill = hasCore && PillH + PillGap + maxSize + belowIfPill <= avail;

            float aboveH = showPill ? PillH + PillGap : 0f;
            float belowH = AnyCaption(node, reqs, coreDieIndex, showPill) ? CaptionRowH : 0f;
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
                bool isRollCore = j == coreDieIndex;

                if (isRollCore && showPill)
                    DrawSkillPill(square, SkillInfo.DisplayName(node.Resolve!.SkillName));

                DrawSlotBlock(square, node, j, req, slotted != null ? slotted[j] : null, disabled, ui, gameManager, ref interaction);

                // 展签行的高度已经由 belowH 预算过，这里无条件按 CaptionFor 的结果画：
                // 核心骰在药丸态下返回空串（属性名已由药丸表达，不重复），药丸被收起时返回属性名。
                string caption = CaptionFor(node, req, isRollCore, showPill);
                if (!string.IsNullOrEmpty(caption))
                    DrawSlotCaption(square, caption);

                x += size + SlotGap;
            }
        }

        private const float CaptionRowH = CaptionH + CaptionGap;

        // 展签文字的唯一来源——预算（AnyCaption）与绘制共用它，避免「算的」和「画的」各写一遍。
        // 核心骰：药丸态下不重复属性名（返回空）；药丸被收起时由展签承担属性名。
        private static string CaptionFor(GameNode node, ActionCost req, bool isRollCore, bool showPill)
        {
            if (isRollCore)
                return showPill ? "" : SkillInfo.DisplayName(node.Resolve!.SkillName);
            return req.Type == "die" ? "骰子" : (string.IsNullOrEmpty(req.ItemId) ? "" : req.ItemId);
        }

        private static bool AnyCaption(GameNode node, List<ActionCost> reqs, int coreDieIndex, bool showPill)
        {
            for (int j = 0; j < reqs.Count; j++)
            {
                if (!string.IsNullOrEmpty(CaptionFor(node, reqs[j], j == coreDieIndex, showPill)))
                    return true;
            }
            return false;
        }

        // 方块 Slot：与手牌方块同族。空 = Ink 槽 + 占位符（提示放什么）；填 = 实心黑方块 + 亮内容。
        private static void DrawSlotBlock(
            Rect rect, GameNode node, int slotIndex, ActionCost req, SlottedResource? res,
            bool disabled, IMGUIInteractionContext ui, SSNoirGameManager gameManager,
            ref CardDrawer.CardInteraction interaction)
        {
            bool slotHover = !disabled && ui.CanHover(rect);
            bool canMatchHeld = !disabled && gameManager.CanMatchRequirement(req);
            bool canDropHeld = !disabled && gameManager.CanPlaceSelectedResource(node, slotIndex);
            Color border = SlotBorderColor(disabled, canDropHeld, canMatchHeld, slotHover);
            bool filled = res != null;

            if (!disabled && (filled || canDropHeld))
                IMGUIStyles.DrawShadow(rect, new Vector2(2f, 2f), 0.4f);
            GUI.color = filled ? SlotBlockBg : IMGUIStyles.Ink;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(rect, (canDropHeld && slotHover) || filled ? 2f : 1f, border);

            Color content = filled ? IMGUIStyles.Paper : SlotPlaceholderColor(canDropHeld, canMatchHeld);

            // 方块被压缩时（见 DrawRequirementSlots 的 scale）内部字号与留白按同一比例跟着走。
            // 否则物品格缩到 48px 时，固定偏移的符号(y+8 高26)和数量(yMax-24 高20)会直接叠在一起。
            // k=1 时与原本的固定数值完全一致，正常尺寸下无任何变化。
            float k = rect.height / SlotSize(req);

            if (req.Type == "die")
            {
                string big = filled ? res!.Value.ToString() : "D";
                var s = new GUIStyle(IMGUIStyles.SlotLabel)
                {
                    fontSize = Mathf.RoundToInt(26f * k),
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = content }
                };
                IMGUIStyles.ApplyStrongFont(s);
                GUI.Label(rect, big, s);
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
                GUI.Label(new Rect(rect.x, rect.y + 8f * k, rect.width, 26f * k), symbol, symStyle);

                var qtyStyle = new GUIStyle(IMGUIStyles.SlotLabel)
                {
                    fontSize = Mathf.RoundToInt(15f * k),
                    alignment = TextAnchor.LowerCenter,
                    normal = { textColor = content }
                };
                IMGUIStyles.ApplyStrongFont(qtyStyle);
                GUI.Label(new Rect(rect.x, rect.yMax - 24f * k, rect.width, 20f * k), $"×{qty}", qtyStyle);
            }

            HandleSlotClick(rect, slotIndex, filled, disabled, canDropHeld, slotHover, ui, ref interaction);
        }

        // 技能药丸 + 连线，挂在判定核心骰方块上方（属性 → 骰子）。
        private static void DrawSkillPill(Rect square, string skill)
        {
            const float pillW = 54f;
            var pill = new Rect(square.center.x - pillW * 0.5f, square.y - PillGap - PillH, pillW, PillH);
            GUI.color = IMGUIStyles.Ink;
            GUI.DrawTexture(pill, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(pill, 1f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f));
            var s = new GUIStyle(IMGUIStyles.CardSubtitle)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = IMGUIStyles.TextSecondary }
            };
            GUI.Label(pill, skill, s);
            IMGUIStyles.DrawLine(new Vector2(pill.center.x, pill.yMax), new Vector2(square.center.x, square.y),
                new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.25f), 1f);
        }

        private static void DrawSlotCaption(Rect square, string text)
        {
            var s = new GUIStyle(IMGUIStyles.CardSubtitle)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = IMGUIStyles.TextSecondary }
            };
            GUI.Label(new Rect(square.x - 12f, square.yMax + CaptionGap, square.width + 24f, CaptionH), text, s);
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
            if (disabled) return new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.35f);
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
            Rect rect, int slotIndex, bool filled, bool disabled, bool canDropHeld, bool slotHover,
            IMGUIInteractionContext ui, ref CardDrawer.CardInteraction interaction)
        {
            if (disabled) return;
            if (filled && ui.WasClicked(rect))
            {
                interaction.ClickedSlotIndex = slotIndex;
                Event.current.Use();
            }
            else if (canDropHeld && slotHover && Event.current.type == EventType.MouseUp && Event.current.button == 0)
            {
                interaction.DroppedSlotIndex = slotIndex;
                Event.current.Use();
            }
        }

        // ── 执行按钮（实心金 = 执行 / 主行动按钮）──────────────────────

        // DESIGN.md 强调色岗位一：实心金 = 执行。金底 + 深字 #2A2107。
        // 禁用态：无填充，描边与文字降到 35%。
        private static bool DrawExecuteButton(Rect rect, string text, IMGUIInteractionContext ui, bool enabled)
        {
            bool isInteractable = enabled && !ui.IsLocked;
            bool isHovered = isInteractable && ui.CanHover(rect);
            bool isClicked = isInteractable && ui.WasClicked(rect);

            var style = new GUIStyle(IMGUIStyles.ExecuteLabel);
            if (isInteractable)
            {
                IMGUIStyles.DrawShadow(rect, new Vector2(2f, 2f), 0.45f);
                GUI.color = isHovered
                    ? new Color(Mathf.Min(1f, IMGUIStyles.Gold.r * 1.08f), Mathf.Min(1f, IMGUIStyles.Gold.g * 1.08f), Mathf.Min(1f, IMGUIStyles.Gold.b * 1.08f), 1f)
                    : IMGUIStyles.Gold;
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = Color.white;
                style.normal.textColor = IMGUIStyles.GoldOnDark;
            }
            else
            {
                var faded = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.35f);
                IMGUIStyles.DrawOutline(rect, 1f, faded);
                style.normal.textColor = faded;
            }
            GUI.Label(rect, text, style);

            return isClicked;
        }

        // 执行中：Ink 底 + 金填充进度 + 金描边 + 金光呼吸（「正在发生」）。
        private static void DrawExecuteProgress(Rect rect, float progress, string text)
        {
            progress = Mathf.Clamp01(progress);
            GUI.color = IMGUIStyles.Ink;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            var fillRect = new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * progress, rect.height - 4f);
            GUI.color = IMGUIStyles.Gold;
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(rect, 1f, IMGUIStyles.Gold);
            IMGUIStyles.DrawGoldPulse(rect);

            string label = string.IsNullOrEmpty(text) ? "执行中" : text;
            if (label.Length > 5) label = "执行中";
            var progressStyle = new GUIStyle(IMGUIStyles.ExecuteLabel);
            progressStyle.normal.textColor = progress > 0.5f ? IMGUIStyles.GoldOnDark : IMGUIStyles.Paper;
            GUI.Label(rect, label, progressStyle);
        }

        // ── 右上角：角色能力栏（规范色，无主题色）──────────────────────

        // 显示当前判定技能下每个在场角色的等级，用 Ink 底 + Paper 描边 + 白字的小芯片。
        // 不再使用主题色，符合 DESIGN.md「全局唯一主强调色」与「黑底安静块」的规则。
        private static void DrawActorAbilityRail(Rect rect, string skill, IReadOnlyList<ActorSnapshot> actors, float startY)
        {
            const float chipW = 54f;
            const float chipH = 20f;
            float x = rect.x + rect.width - chipW - 6f;
            float y = startY;
            int drawn = 0;
            foreach (var actor in actors)
            {
                if (actor.Status == "away" || !actor.Stats.TryGetValue(skill, out int level))
                    continue;

                var chip = new Rect(x, y + drawn * (chipH + 4f), chipW, chipH);
                GUI.color = IMGUIStyles.Ink;
                GUI.DrawTexture(chip, Texture2D.whiteTexture);
                GUI.color = Color.white;
                IMGUIStyles.DrawOutline(chip, 1f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.70f));

                string shortName = actor.Name.Length > 2 ? actor.Name.Substring(0, 2) : actor.Name;
                var nameStyle = new GUIStyle(GUI.skin.label)
                {
                    font = IMGUIStyles.ChineseFont,
                    fontSize = 12,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = IMGUIStyles.Paper }
                };
                IMGUIStyles.ApplyStrongFont(nameStyle);
                GUI.Label(new Rect(chip.x + 5f, chip.y, 30f, chipH), shortName, nameStyle);

                var lvlStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleRight,
                    normal = { textColor = IMGUIStyles.Paper }
                };
                GUI.Label(new Rect(chip.x, chip.y, chip.width - 6f, chipH), level.ToString(), lvlStyle);
                drawn++;
            }
        }

        // ── 底部：命运六面预览 ────────────────────────────────────────

        private static void TryDrawFatePreview(Rect rect, string skill, List<SlottedResource?> slotted,
            List<DifficultyModifierInfo>? modifiers, IReadOnlyList<ActorSnapshot> actors, float executeBottomY)
        {
            SlottedResource? dieSlot = null;
            foreach (var s in slotted)
            {
                if (s != null && s.Type == "die") { dieSlot = s; break; }
            }
            if (dieSlot == null) return;

            int? skillLevel = null;
            foreach (var actor in actors)
            {
                if (actor.Id != dieSlot.ActorId) continue;
                if (!actor.Stats.TryGetValue(skill, out int lv))
                    throw new System.InvalidOperationException($"Actor '{actor.Id}' is missing required stat '{skill}'.");
                skillLevel = lv;
                break;
            }
            if (!skillLevel.HasValue)
                throw new System.InvalidOperationException($"Actor '{dieSlot.ActorId}' was not found for fate preview.");

            int modSum = 0;
            if (modifiers != null)
            {
                foreach (var m in modifiers) modSum += m.Value;
            }

            DrawFateStrip(rect, FateStrip.Compute(dieSlot.Value, skillLevel.Value, modSum), executeBottomY);
        }

        private static void DrawFateStrip(Rect rect, RollOutcome[] strip, float executeBottomY)
        {
            const float pad = 10f;
            var summaryStyle = new GUIStyle(IMGUIStyles.ClockLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = IMGUIStyles.TextSecondary }
            };
            GUI.Label(new Rect(rect.x + pad, executeBottomY, rect.width - pad * 2f, 14f),
                FateStrip.Describe(strip), summaryStyle);

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
            var faceStyle = new GUIStyle(IMGUIStyles.ClockValue) { alignment = TextAnchor.MiddleCenter };
            for (int i = 0; i < 6; i++)
            {
                if (i > 0) x += strip[i] != strip[i - 1] ? boundaryGap : gap;
                Color tier = OutcomeColor(strip[i]);
                bool hi = i + 1 == highlightedFace;
                float pop = hi ? pulse * 5f : 0f;
                var cell = new Rect(x - pop / 2f, area.y + (hi ? -2f : 0f) - pop,
                    cellW + pop, area.height + (hi ? 4f : 0f) + pop * 2f);
                float dim = hi ? 1f : (settled ? 0.34f : 0.70f);
                GUI.color = new Color(tier.r * dim, tier.g * dim, tier.b * dim, 1f);
                GUI.DrawTexture(cell, Texture2D.whiteTexture);
                GUI.color = Color.white;
                if (hi)
                    IMGUIStyles.DrawOutline(cell, 2f, IMGUIStyles.Gold);
                float td = hi ? 0.18f : (settled ? 0.10f : 0.20f);
                faceStyle.normal.textColor = new Color(tier.r * td, tier.g * td * 0.8f, tier.b * td * 0.8f, 1f);
                GUI.Label(cell, (i + 1).ToString(), faceStyle);
                x += cellW;
            }
        }

        // ── 判定中：掷骰瞬间面板（金光呼吸）──────────────────────────

        // 掷骰面板与结算面板共用的锚点：头部固定高度 54，理想情况下贴在 rect.yMax-66（留 12 底边距）。
        // DrawResiduePanel 的 body 会撑大总高度——两者共用这组常量，避免数值各改一遍互相漂移。
        private const float PanelHeaderH = 54f;
        private const float PanelBottomOffset = 66f;
        private const float PanelBottomMargin = 12f;
        private const float PanelTopMargin = 8f;

        // 掷骰中：命运条本身就是动画（减速扫掠 → 落格弹跳 → 定格），而非一个方框里跳变的数字。
        private static void DrawLocalRoll(Rect rect, ActionReport report, int phase, int displayDieValue, float displayScale)
        {
            var panel = new Rect(rect.x + 12f, rect.yMax - PanelBottomOffset, rect.width - 24f, PanelHeaderH);
            IMGUIStyles.DrawShadow(panel, new Vector2(4f, 5f), 0.45f);
            GUI.color = new Color(IMGUIStyles.Ink.r, IMGUIStyles.Ink.g, IMGUIStyles.Ink.b, IMGUIStyles.ModalOpacity);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            bool settled = phase >= 2;
            Color oc = settled ? OutcomeColor(report.Outcome) : IMGUIStyles.Gold;
            IMGUIStyles.DrawOutline(panel, 1.5f, oc);
            if (phase < 2)
                IMGUIStyles.DrawGoldPulse(panel);

            DrawRollHeaderText(panel, settled ? FormatOutcome(report.Outcome) : "判定中",
                settled ? oc : IMGUIStyles.TextPrimary,
                settled ? $"准备 {report.PreparedValue} · 命运骰 {report.FateDieValue}" : "命运骰滚动...");

            int highlightedFace = phase == 0 ? displayDieValue : report.FateDieValue;
            float pulse = phase == 1 ? Mathf.Max(0f, displayScale - 1f) : 0f;
            var strip = FateStrip.StripForPrepared(report.PreparedValue);
            DrawOddsStrip(new Rect(panel.x + 10f, panel.y + 26f, panel.width - 20f, 18f),
                strip, highlightedFace, pulse, settled);

            if (settled && report.Outcome != RollOutcome.Neutral)
                DrawResultSeal(rect, panel.y, report.Outcome);
        }

        // 结算面板的几何是 rect + residue 的纯函数——绘制和「标题会不会被盖住」的判断共用它，
        // 免得两边各算一遍再慢慢漂移。
        //
        // 理想位置与 DrawLocalRoll 落定态同位（rect.yMax - PanelBottomOffset）→ 原地冻结；
        // body 撑破卡底预留时整个面板上移。上移的下界只取卡片自身（rect.y + PanelTopMargin），
        // **不给标题/副标题/骰位让路**：residue 一旦存在，这张卡的使命就从「让你操作」变成
        // 「告诉你结果」，让面板盖住那些操作用的元素是合理的（执行按钮本来就画在它底下），
        // 让结算内容消失不合理。反过来把面板上界钉在骰位区下沿，会在 340×190 这类常见档位上
        // 把叙事和全部影响行静默吞掉——那是更严重的错误。
        //
        // bodyH 必须与 DrawResiduePanel 里 body 的实际画法逐项对齐（头部下方 4 + 副标题 20 +
        // 影响行 16/行 + 底部 4），否则预算与实际可用高度对不上，影响行会被 DrawEffectRows 误折成「+N」。
        private static Rect ResiduePanelRect(Rect rect, CardPresentationResidue residue)
        {
            bool hasSubtitle = !string.IsNullOrWhiteSpace(residue.Subtitle);
            int effectRows = Mathf.Min(3, residue.Effects.Count);
            float bodyH = 0f;
            if (hasSubtitle || effectRows > 0) bodyH += 8f;
            if (hasSubtitle) bodyH += 20f;
            bodyH += effectRows * 16f;

            float totalH = PanelHeaderH + bodyH;
            float top = Mathf.Max(
                Mathf.Min(rect.yMax - PanelBottomOffset, rect.yMax - PanelBottomMargin - totalH),
                rect.y + PanelTopMargin);
            totalH = Mathf.Max(PanelHeaderH, Mathf.Min(totalH, rect.yMax - PanelBottomMargin - top));
            return new Rect(rect.x + 12f, top, rect.width - 24f, totalH);
        }

        // 结算结果 = 命运条「原地定格」+ 结果从其下方揭开。头部与 DrawLocalRoll 落定态同内容，
        // 位置由 ResiduePanelRect 决定（body 不撑破卡底时即同位，形成无缝冻结）；
        // 身体（叙事 + 影响）随揭开轻微下滑弹出。
        private static void DrawResiduePanel(Rect rect, CardPresentationResidue residue)
        {
            // residue 只有动画落定后才被绘制——首帧即结果该「揭开」的时刻，惰性记录起点。
            if (residue.RevealStartTime <= 0f)
                residue.RevealStartTime = Time.time;
            float reveal = Mathf.Clamp01((Time.time - residue.RevealStartTime) / 0.28f);
            float ease = 1f - Mathf.Pow(1f - reveal, 3f);

            var panel = ResiduePanelRect(rect, residue);
            IMGUIStyles.DrawShadow(panel, new Vector2(4f, 5f), 0.45f);
            GUI.color = new Color(IMGUIStyles.Ink.r, IMGUIStyles.Ink.g, IMGUIStyles.Ink.b, IMGUIStyles.ModalOpacity);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var header = new Rect(panel.x, panel.y, panel.width, PanelHeaderH);
            bool hasOutcome = residue.RollOutcome.HasValue;
            RollOutcome outcome = residue.RollOutcome ?? RollOutcome.Neutral;
            Color oc = hasOutcome ? OutcomeColor(outcome) : IMGUIStyles.Gold;
            IMGUIStyles.DrawOutline(header, 1.5f, oc);

            string headerLabel = hasOutcome ? FormatOutcome(outcome)
                : string.IsNullOrWhiteSpace(residue.Title) ? "行动结果" : residue.Title;
            DrawRollHeaderText(header, headerLabel, hasOutcome ? oc : IMGUIStyles.TextPrimary,
                residue.FateDieValue.HasValue ? $"准备 {residue.PreparedValue} · 命运骰 {residue.FateDieValue.Value}" : "");

            if (residue.FateDieValue.HasValue)
            {
                var strip = FateStrip.StripForPrepared(residue.PreparedValue);
                DrawOddsStrip(new Rect(header.x + 10f, header.y + 26f, header.width - 20f, 18f),
                    strip, residue.FateDieValue.Value, 0f, true);
            }

            if (hasOutcome && outcome != RollOutcome.Neutral)
                DrawResultSeal(rect, header.y, outcome);

            // 身体：结果从命运条下方揭开（叙事淡入、整体轻微下滑）。
            // body 预算被压缩到连副标题都放不下的极端情况下，宁可跳过副标题也不画出面板外。
            float y = header.yMax + 4f + (1f - ease) * 5f;
            if (!string.IsNullOrWhiteSpace(residue.Subtitle) && y + 18f <= panel.yMax)
            {
                GUI.color = new Color(oc.r, oc.g, oc.b, ease);
                GUI.DrawTexture(new Rect(panel.x + 8f, y + 1f, 2.5f, 13f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                var subStyle = new GUIStyle(IMGUIStyles.ModalBody)
                {
                    fontSize = 11,
                    wordWrap = true,
                    normal = { textColor = new Color(IMGUIStyles.TextPrimary.r, IMGUIStyles.TextPrimary.g, IMGUIStyles.TextPrimary.b, ease) }
                };
                GUI.Label(new Rect(panel.x + 15f, y, panel.width - 22f, 18f), residue.Subtitle, subStyle);
                y += 20f;
            }

            if (residue.Effects.Count > 0)
                CardDrawer.DrawEffectRows(new Rect(panel.x + 8f, y, panel.width - 16f, panel.yMax - y - 4f), residue.Effects);
        }

        // 判定面板头部：左上结果/状态标签 + 右上「准备 · 命运骰」明细，供掷骰态与结算态共用。
        private static void DrawRollHeaderText(Rect panel, string label, Color labelColor, string detail)
        {
            var labelStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                fontSize = 13,
                normal = { textColor = labelColor }
            };
            GUI.Label(new Rect(panel.x + 8f, panel.y + 3f, 130f, 18f), label, labelStyle);

            if (string.IsNullOrEmpty(detail)) return;
            var detailStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = IMGUIStyles.TextSecondary }
            };
            GUI.Label(new Rect(panel.x + panel.width - 158f, panel.y + 4f, 150f, 16f), detail, detailStyle);
        }

        // 旋转章形盖印（成=金、败=印章红；中性不盖），骑在面板右上角外。
        private static void DrawResultSeal(Rect rect, float panelY, RollOutcome outcome)
        {
            var sealRect = new Rect(rect.xMax - 62f, panelY - 30f, 52f, 52f);
            Color sealColor = outcome == RollOutcome.Success ? IMGUIStyles.Gold : IMGUIStyles.SealRed;
            string sealText = outcome == RollOutcome.Success ? "成" : "败";
            IMGUIStyles.DrawStampSeal(sealRect, sealText, sealColor);
        }

        // ── 标签配色（便签色板，DESIGN.md）──────────────────────────────

        // 同一语义永远同一张纸：工作/低风险=绿纸、高风险=橙红纸、交涉=蓝纸、机遇=紫纸。
        private static (Color bg, Color text) TagColors(string label, bool disabled = false)
        {
            if (disabled)
                return (new Color(0.18f, 0.19f, 0.22f, 1f), new Color(0.55f, 0.56f, 0.60f, 1f));

            switch (label)
            {
                case "交锋":
                    return (new Color(0.82f, 0.58f, 0.48f, 1f), new Color(0.30f, 0.07f, 0.03f, 1f));
                case "工作":
                case "低风险":
                    return (new Color(0.66f, 0.74f, 0.50f, 1f), new Color(0.10f, 0.18f, 0.05f, 1f));
                case "高风险":
                case "非法":
                    return (new Color(0.82f, 0.58f, 0.48f, 1f), new Color(0.30f, 0.07f, 0.03f, 1f));
                case "机遇":
                    return (new Color(0.72f, 0.58f, 0.76f, 1f), new Color(0.19f, 0.08f, 0.22f, 1f));
                default:
                    return (new Color(0.58f, 0.68f, 0.80f, 1f), new Color(0.05f, 0.12f, 0.22f, 1f));
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
