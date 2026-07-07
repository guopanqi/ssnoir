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
    //   → 底部概率条 + 三色百分比。
    // 判定完成态：概率条换结算行，结果以旋转的章形盖印呈现（成=金，败=印章红）。
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
            ref CardDrawer.CardInteraction interaction)
        {
            bool disabled = node.Disabled;
            bool isRoll = node.Resolve!.Type == ResolveType.Roll;
            bool hasRequires = node.Requires != null && node.Requires.Count > 0 && slotted != null && slotted.Count == node.Requires.Count;
            var actors = gameManager.DisplayedSnapshot.Actors;
            bool showOdds = isRoll && !disabled && hasRequires && actors != null && localRoll == null && residue == null;

            // ── 1. 标题区（居中）──
            float titleY = rect.y + 12f;
            var titleStyle = new GUIStyle(IMGUIStyles.CardTitle) { alignment = TextAnchor.MiddleCenter };
            if (disabled) titleStyle.normal.textColor = IMGUIStyles.TextSecondary;
            GUI.Label(new Rect(rect.x + 10f, titleY, rect.width - 20f, 26f), node.Name, titleStyle);

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
                GUI.Label(new Rect(rect.x + 12f, titleY + 26f, subtitleWidth, subtitleHeight), node.Subtitle, subtitleStyle);
                subtitleBottomY = titleY + 26f + subtitleHeight;
            }

            // ── 2. 三列分区的纵向边界 ──
            // 执行按钮贴底；showOdds 时再往下留出概率条 + 三色百分比的空间。
            float exeH = 26f;
            float exeY = rect.yMax - (showOdds || localRoll != null || residue != null ? 54f : 34f);
            float bodyY = subtitleBottomY + 6f;

            // ── 3. 类别 / 风险便签：骑在卡片左边缘外（不遮内容、不占内部空间）。
            DrawEdgeTags(rect, node);

            // ── 4. 需求骰位：统一为「方块 Slot」（与手牌骰子/物品同族），居中横排。
            //     骰子 slot = 大字 D/值；物品 slot = 符号 + 数量（强调）+ 下方名称展签。
            //     判定的核心骰上方挂技能药丸（属性 → 骰子）。
            int coreDieIndex = isRoll ? FindCoreDieIndex(node) : -1;
            DrawRequirementSlots(rect, bodyY, exeY, node, slotted, coreDieIndex, disabled, ui, gameManager, ref interaction);

            // ── 6. 执行按钮（实心金 / 待命 / 不可用 / 执行中）──
            var exeRect = new Rect(rect.x + (rect.width - 112f) / 2f, exeY, 112f, exeH);
            bool allFilled = slotted != null && slotted.All(s => s != null);
            if (isExecuting)
            {
                DrawExecuteProgress(exeRect, executeProgress, executingText);
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
                DrawActorAbilityRail(rect, node.Resolve!.SkillName, actors, node.Clocks != null && node.Clocks.Count > 0);

            // ── 8. 底部：概率条 / 判定中 / 结算行 + 盖印 ──
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
                TryDrawOddsPreview(rect, node.Resolve!.SkillName, slotted!, node.Resolve.DifficultyModifiers, actors!, exeRect.yMax);
            }

            // ── 9. 卡片级点击（仅无 requires 的非 Instant 类型，如 Observe / Clock）──
            if (!disabled && ui.WasClicked(rect) && !hasRequires && node.Resolve!.Type != ResolveType.Instant)
            {
                interaction.CardClicked = true;
                Event.current.Use();
            }
        }

        // ── 边缘便签：类别 / 风险，骑在卡片左边缘外 ────────────────────

        // 同族彩纸便签，半贴在卡左边缘上（约 60% 露在外），自上而下堆叠；微旋转 + 硬影。
        // 不占卡内空间，也不遮主要元素——「贴在物件上的彩色便签」隐喻更足。
        private static void DrawEdgeTags(Rect rect, GameNode node)
        {
            const float tagW = 58f;
            const float tagH = 18f;
            float tagX = rect.x - 34f;
            float y = rect.y + 16f;
            float maxY = rect.yMax - 40f;
            int stickyCount = 0;
            var tagStyle = new GUIStyle(GUI.skin.label) { fontSize = 9 };

            var modifiers = node.Resolve!.DifficultyModifiers;
            if (modifiers != null)
            {
                for (int k = 0; k < modifiers.Count; k++)
                {
                    float tagY = y + stickyCount * (tagH + 4f);
                    if (tagY + tagH > maxY) return;
                    var mod = modifiers[k];
                    string modText = $"{mod.Reason} {(mod.Value > 0 ? "+" : "")}{mod.Value}";
                    Color noteBg = mod.Value < 0 ? IMGUIStyles.StickyHighRiskBg
                                : (mod.Value > 0 ? IMGUIStyles.StickyWorkBg : IMGUIStyles.StickyNegotiateBg);
                    Color noteText = mod.Value < 0 ? IMGUIStyles.StickyHighRiskText
                                : (mod.Value > 0 ? IMGUIStyles.StickyWorkText : IMGUIStyles.StickyNegotiateText);
                    float noteRot = (stickyCount % 2 == 0) ? -2f : 2f;
                    IMGUIStyles.DrawStickyNote(new Rect(tagX, tagY, tagW, tagH), modText, noteBg, noteText, noteRot, tagStyle);
                    stickyCount++;
                }
            }

            if (node.Tags != null)
            {
                for (int k = 0; k < node.Tags.Count; k++)
                {
                    string tag = node.Tags[k];
                    if (string.IsNullOrWhiteSpace(tag)) continue;
                    float tagY = y + stickyCount * (tagH + 4f);
                    if (tagY + tagH > maxY) return;
                    var (bg, text) = TagColors(tag);
                    float noteRot = (stickyCount % 2 == 0) ? -2f : 2f;
                    IMGUIStyles.DrawStickyNote(new Rect(tagX, tagY, tagW, tagH), tag, bg, text, noteRot, tagStyle);
                    stickyCount++;
                }
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

        private static void DrawRequirementSlots(
            Rect rect, float bodyY, float exeY, GameNode node, List<SlottedResource?>? slotted,
            int coreDieIndex, bool disabled, IMGUIInteractionContext ui, SSNoirGameManager gameManager,
            ref CardDrawer.CardInteraction interaction)
        {
            var reqs = node.Requires;
            if (reqs == null || reqs.Count == 0) return;

            int n = reqs.Count;
            float rowW = 0f, maxSize = 0f;
            for (int j = 0; j < n; j++)
            {
                float s = SlotSize(reqs[j]);
                rowW += s;
                maxSize = Mathf.Max(maxSize, s);
            }
            rowW += SlotGap * (n - 1);

            float aboveH = coreDieIndex >= 0 ? (PillH + PillGap) : 0f;
            float belowH = CaptionH + CaptionGap;
            float need = aboveH + maxSize + belowH;
            float startY = bodyY + Mathf.Max(0f, (exeY - bodyY - need) * 0.5f);
            float centerY = startY + aboveH + maxSize * 0.5f;

            float x = rect.center.x - rowW * 0.5f;
            for (int j = 0; j < n; j++)
            {
                var req = reqs[j];
                float size = SlotSize(req);
                var square = new Rect(x, centerY - size * 0.5f, size, size);
                bool isRollCore = j == coreDieIndex;

                if (isRollCore)
                    DrawSkillPill(square, SkillInfo.DisplayName(node.Resolve!.SkillName));

                DrawSlotBlock(square, node, j, req, slotted != null ? slotted[j] : null, disabled, ui, gameManager, ref interaction);

                // 名称展签：物品=物品名；消耗骰=「骰子」；判定核心骰用上方药丸表达，不再重复。
                if (!isRollCore)
                {
                    string caption = req.Type == "die" ? "骰子" : (string.IsNullOrEmpty(req.ItemId) ? "" : req.ItemId);
                    if (!string.IsNullOrEmpty(caption))
                        DrawSlotCaption(square, caption);
                }

                x += size + SlotGap;
            }
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

            if (req.Type == "die")
            {
                string big = filled ? res!.Value.ToString() : "D";
                var s = new GUIStyle(IMGUIStyles.SlotLabel)
                {
                    fontSize = 26,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = content }
                };
                GUI.Label(rect, big, s);
            }
            else
            {
                // 物品：符号（大）+ 数量（强调），both 突出，名称在方块外下方展签。
                string symbol = ItemSymbol(req.ItemId);
                int qty = filled ? (res!.Qty > 0 ? res.Qty : 1) : req.Qty;
                var symStyle = new GUIStyle(IMGUIStyles.SlotLabel)
                {
                    fontSize = 22,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperCenter,
                    normal = { textColor = content }
                };
                GUI.Label(new Rect(rect.x, rect.y + 8f, rect.width, 26f), symbol, symStyle);

                var qtyStyle = new GUIStyle(IMGUIStyles.SlotLabel)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.LowerCenter,
                    normal = { textColor = content }
                };
                GUI.Label(new Rect(rect.x, rect.yMax - 24f, rect.width, 20f), $"×{qty}", qtyStyle);
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
                "食物" => "食",
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
        private static void DrawActorAbilityRail(Rect rect, string skill, IReadOnlyList<ActorSnapshot> actors, bool hasClocks)
        {
            const float chipW = 54f;
            const float chipH = 20f;
            float x = rect.x + rect.width - chipW - 6f;
            float y = rect.y + (hasClocks ? 24f : 6f);
            int drawn = 0;
            foreach (var actor in actors)
            {
                if (actor.Status == "away" || !actor.Stats.TryGetValue(skill, out int level))
                    continue;

                var chip = new Rect(x, y + drawn * (chipH + 4f), chipW, chipH);
                GUI.color = IMGUIStyles.Ink;
                GUI.DrawTexture(chip, Texture2D.whiteTexture);
                GUI.color = Color.white;
                IMGUIStyles.DrawOutline(chip, 1f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f));

                string shortName = actor.Name.Length > 2 ? actor.Name.Substring(0, 2) : actor.Name;
                var nameStyle = new GUIStyle(GUI.skin.label)
                {
                    font = IMGUIStyles.ChineseFont,
                    fontSize = 10,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.60f) }
                };
                GUI.Label(new Rect(chip.x + 5f, chip.y, 28f, chipH), shortName, nameStyle);

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

        // ── 底部：概率条 + 三色百分比 ──────────────────────────────────

        private static void TryDrawOddsPreview(Rect rect, string skill, List<SlottedResource?> slotted,
            List<DifficultyModifierInfo>? modifiers, IReadOnlyList<ActorSnapshot> actors, float executeBottomY)
        {
            SlottedResource? dieSlot = null;
            foreach (var s in slotted)
            {
                if (s != null && s.Type == "die") { dieSlot = s; break; }
            }
            if (dieSlot == null) return;

            int skillLevel = 1;
            foreach (var actor in actors)
            {
                if (actor.Id == dieSlot.ActorId && actor.Stats.TryGetValue(skill, out int lv)) { skillLevel = lv; break; }
            }
            int modSum = 0;
            if (modifiers != null)
            {
                foreach (var m in modifiers) modSum += m.Value;
            }

            var odds = RollOdds.Compute(dieSlot.Value, skillLevel, modSum);
            DrawOddsPreview(rect, odds, executeBottomY);
        }

        // 紧凑三色比例条：失败 / 中性 / 成功 的宽度即概率占比，下方标百分比。
        private static void DrawOddsPreview(Rect rect, RollOddsResult odds, float executeBottomY)
        {
            float pad = 10f;
            float barY = executeBottomY + 5f;
            float barX = rect.x + pad;
            float barW = rect.width - pad * 2f;
            float barH = 5f;

            var bands = new (double p, Color c)[]
            {
                (odds.Fail,    IMGUIStyles.OddsFail),
                (odds.Neutral, IMGUIStyles.OddsNeutral),
                (odds.Success, IMGUIStyles.OddsSuccess),
            };

            float x = barX;
            for (int i = 0; i < 3; i++)
            {
                float w = i == 2 ? (barX + barW - x) : barW * (float)bands[i].p;
                if (w > 0.5f)
                {
                    GUI.color = bands[i].c;
                    GUI.DrawTexture(new Rect(x, barY, w, barH), Texture2D.whiteTexture);
                }
                x += w;
            }
            GUI.color = Color.white;

            float colW = barW / 3f;
            for (int i = 0; i < 3; i++)
            {
                var pctStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = bands[i].c }
                };
                GUI.Label(new Rect(barX + i * colW, barY + barH + 1f, colW, 14f),
                    $"{Mathf.RoundToInt((float)bands[i].p * 100)}%", pctStyle);
            }
        }

        // ── 判定中：掷骰瞬间面板（金光呼吸）──────────────────────────

        private static void DrawLocalRoll(Rect rect, ActionReport report, int phase, int displayDieValue, float displayScale)
        {
            var panel = new Rect(rect.x + 12f, rect.yMax - 66f, rect.width - 24f, 54f);
            GUI.color = new Color(IMGUIStyles.Ink.r, IMGUIStyles.Ink.g, IMGUIStyles.Ink.b, IMGUIStyles.ModalOpacity);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(panel, 1.5f, IMGUIStyles.Gold);

            // 掷骰瞬间是「正在发生」——金光呼吸只在滚动阶段亮起。
            if (phase < 2)
                IMGUIStyles.DrawGoldPulse(panel);

            int dieValue = phase == 0 ? displayDieValue : report.FinalRollValue;
            var dieStyle = new GUIStyle(IMGUIStyles.CardTitle)
            {
                fontSize = Mathf.RoundToInt(26 * Mathf.Clamp(displayScale, 0.8f, 1.35f)),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = IMGUIStyles.Gold }
            };
            GUI.Label(new Rect(panel.x + 10f, panel.y + 4f, 52f, panel.height - 8f), $"D{dieValue}", dieStyle);

            string label = phase >= 2 ? FormatOutcome(report.Outcome) : "判定中";
            var labelStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                fontSize = 15,
                normal = { textColor = phase >= 2 ? OutcomeColor(report.Outcome) : IMGUIStyles.TextPrimary }
            };
            GUI.Label(new Rect(panel.x + 72f, panel.y + 9f, panel.width - 82f, 22f), label, labelStyle);

            string detail = phase >= 2 ? $"最终值 {report.ModifiedRollValue}" : "骰子滚动...";
            var detailStyle = new GUIStyle(IMGUIStyles.ModalBody) { normal = { textColor = IMGUIStyles.TextSecondary } };
            GUI.Label(new Rect(panel.x + 72f, panel.y + 30f, panel.width - 82f, 18f), detail, detailStyle);

            // 判定完成态：旋转章形盖印（成=金，败=印章红；中性不盖）。
            if (phase >= 2 && report.Outcome != RollOutcome.Neutral)
            {
                var sealRect = new Rect(rect.xMax - 62f, panel.y - 30f, 52f, 52f);
                Color sealColor = report.Outcome == RollOutcome.Success ? IMGUIStyles.Gold : IMGUIStyles.SealRed;
                string sealText = report.Outcome == RollOutcome.Success ? "成" : "败";
                IMGUIStyles.DrawStampSeal(sealRect, sealText, sealColor);
            }
        }

        // ── 判定完成态：结算行 + 盖印（残卡内嵌版）────────────────────

        private static void DrawResiduePanel(Rect rect, CardPresentationResidue residue)
        {
            var panel = new Rect(rect.x + 12f, rect.yMax - 68f, rect.width - 24f, 56f);
            GUI.color = new Color(IMGUIStyles.Ink.r, IMGUIStyles.Ink.g, IMGUIStyles.Ink.b, IMGUIStyles.ModalOpacity);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(panel, 1f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.70f));

            // 结果盖印：成=金、败=印章红（中性不盖）。
            if (residue.RollOutcome == RollOutcome.Success || residue.RollOutcome == RollOutcome.Fail)
            {
                var sealRect = new Rect(rect.xMax - 58f, panel.y - 26f, 46f, 46f);
                Color sealColor = residue.RollOutcome == RollOutcome.Success ? IMGUIStyles.Gold : IMGUIStyles.SealRed;
                string sealText = residue.RollOutcome == RollOutcome.Success ? "成" : "败";
                IMGUIStyles.DrawStampSeal(sealRect, sealText, sealColor);
            }

            string title = residue.RollOutcome.HasValue
                ? $"{FormatOutcome(residue.RollOutcome.Value)}：{residue.Title}"
                : residue.Title;
            var titleStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = residue.RollOutcome.HasValue ? OutcomeColor(residue.RollOutcome.Value) : IMGUIStyles.TextPrimary }
            };
            GUI.Label(new Rect(panel.x + 8f, panel.y + 6f, panel.width - 16f, 22f), title, titleStyle);

            var subtitleStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                fontSize = 11,
                wordWrap = true,
                normal = { textColor = IMGUIStyles.TextSecondary }
            };
            GUI.Label(new Rect(panel.x + 8f, panel.y + 28f, panel.width - 16f, 24f), residue.Subtitle, subtitleStyle);
        }

        // ── 标签配色（便签色板，DESIGN.md）──────────────────────────────

        // 同一语义永远同一张纸：工作=绿纸、中风险=黄纸、高风险=橙红纸、交涉=蓝纸、机遇=紫纸。
        private static (Color bg, Color text) TagColors(string label)
        {
            switch (label)
            {
                case "交锋":
                    return (IMGUIStyles.StickyHighRiskBg, IMGUIStyles.StickyHighRiskText);
                case "工作":
                case "低风险":
                    return (IMGUIStyles.StickyWorkBg, IMGUIStyles.StickyWorkText);
                case "中风险":
                    return (IMGUIStyles.StickyMidRiskBg, IMGUIStyles.StickyMidRiskText);
                case "高风险":
                case "越界":
                    return (IMGUIStyles.StickyHighRiskBg, IMGUIStyles.StickyHighRiskText);
                case "机遇":
                    return (IMGUIStyles.StickyOpportunityBg, IMGUIStyles.StickyOpportunityText);
                default:
                    return (IMGUIStyles.StickyNegotiateBg, IMGUIStyles.StickyNegotiateText);
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
