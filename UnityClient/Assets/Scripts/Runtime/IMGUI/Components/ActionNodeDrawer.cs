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
            ref CardDrawer.CardInteraction interaction)
        {
            bool disabled = node.Disabled;
            bool isRoll = node.Resolve!.Type == ResolveType.Roll;
            int requiredSlotCount = node.Requires?.Count ?? 0;
            bool hasRequires = requiredSlotCount > 0 && slotted != null && slotted.Count == requiredSlotCount;
            bool isObserve = node.Resolve!.Type == ResolveType.Observe;
            var actors = gameManager.DisplayedSnapshot.Actors;
            bool showOdds = isRoll && !disabled && hasRequires && actors != null && localRoll == null && residue == null;
            var effectiveModifiers = BuildEffectiveModifiers(
                node.Resolve.DifficultyModifiers, slotted, actors, node.Resolve.IgnoresStressPenalty);

            // ── 1. 标题区（居中）──
            float titleY = rect.y + (node.Clocks != null && node.Clocks.Count > 0 ? 44f : 12f);
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
            // 执行按钮贴底；showOdds 时再往下留出命运六面预览的空间。
            float exeH = 26f;
            float exeY = rect.yMax - (showOdds || localRoll != null || residue != null ? 62f : 34f);
            float bodyY = subtitleBottomY + 6f;

            // ── 3. 类别 / 风险便签：骑在卡片左边缘外（不遮内容、不占内部空间）。
            DrawEdgeTags(rect, node, effectiveModifiers, disabled);

            // ── 4. 需求骰位：统一为「方块 Slot」（与手牌骰子/物品同族），居中横排。
            //     骰子 slot = 大字 D/值；物品 slot = 符号 + 数量（强调）+ 下方名称展签。
            //     判定的核心骰上方挂技能药丸（属性 → 骰子）。
            int coreDieIndex = isRoll ? FindCoreDieIndex(node) : -1;
            DrawRequirementSlots(rect, bodyY, exeY, node, slotted, coreDieIndex, disabled, ui, gameManager, ref interaction);

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
                DrawActorAbilityRail(rect, node.Resolve!.SkillName, actors, node.Clocks != null && node.Clocks.Count > 0);

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
        private static void DrawEdgeTags(Rect rect, GameNode node, List<DifficultyModifierInfo> effectiveModifiers, bool disabled)
        {
            const float tagW = 26f;
            const float tagH = 72f;
            float tagX = rect.x - 13f;
            float y = rect.y + 22f;
            float maxY = rect.yMax - 40f;
            int stickyCount = 0;
            var tagStyle = new GUIStyle(GUI.skin.label)
            {
                font = IMGUIStyles.ChineseFont,
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip
            };

            if (effectiveModifiers.Count > 0)
            {
                for (int k = 0; k < effectiveModifiers.Count; k++)
                {
                    float tagY = y + stickyCount * (tagH + 6f);
                    if (tagY + tagH > maxY) return;
                    var mod = effectiveModifiers[k];
                    string modText = $"{mod.Reason} {(mod.Value > 0 ? "+" : "")}{mod.Value}";
                    Color noteBg = disabled
                        ? new Color(0.18f, 0.19f, 0.22f, 1f)
                        : mod.Value < 0 ? new Color(0.82f, 0.58f, 0.48f, 1f)
                        : (mod.Value > 0 ? new Color(0.66f, 0.74f, 0.50f, 1f) : new Color(0.58f, 0.68f, 0.80f, 1f));
                    Color noteText = disabled
                        ? new Color(0.55f, 0.56f, 0.60f, 1f)
                        : mod.Value < 0 ? new Color(0.30f, 0.07f, 0.03f, 1f)
                        : (mod.Value > 0 ? new Color(0.10f, 0.18f, 0.05f, 1f) : new Color(0.05f, 0.12f, 0.22f, 1f));
                    DrawVerticalTag(new Rect(tagX, tagY, tagW, tagH), modText, noteBg, noteText, tagStyle);
                    stickyCount++;
                }
            }

            if (node.Tags != null)
            {
                for (int k = 0; k < node.Tags.Count; k++)
                {
                    string tag = node.Tags[k];
                    if (string.IsNullOrWhiteSpace(tag)) continue;
                    float tagY = y + stickyCount * (tagH + 6f);
                    if (tagY + tagH > maxY) return;
                    var (bg, text) = TagColors(tag, disabled);
                    DrawVerticalTag(new Rect(tagX, tagY, tagW, tagH), tag, bg, text, tagStyle);
                    stickyCount++;
                }
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
                    fontSize = 22,
                    alignment = TextAnchor.UpperCenter,
                    normal = { textColor = content }
                };
                IMGUIStyles.ApplyStrongFont(symStyle);
                GUI.Label(new Rect(rect.x, rect.y + 8f, rect.width, 26f), symbol, symStyle);

                var qtyStyle = new GUIStyle(IMGUIStyles.SlotLabel)
                {
                    fontSize = 15,
                    alignment = TextAnchor.LowerCenter,
                    normal = { textColor = content }
                };
                IMGUIStyles.ApplyStrongFont(qtyStyle);
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

        private static List<DifficultyModifierInfo> BuildEffectiveModifiers(
            List<DifficultyModifierInfo> baseModifiers,
            List<SlottedResource?>? slotted,
            IReadOnlyList<ActorSnapshot>? actors,
            bool ignoresStressPenalty)
        {
            var result = new List<DifficultyModifierInfo>(baseModifiers);
            if (ignoresStressPenalty || slotted == null || actors == null) return result;

            SlottedResource? die = slotted.FirstOrDefault(s => s?.Type == "die");
            if (die == null) return result;
            ActorSnapshot? actor = actors.FirstOrDefault(a => a.Id == die.ActorId);
            if (actor == null)
                throw new System.InvalidOperationException($"Actor '{die.ActorId}' was not found for stress modifier preview.");

            int value = TeamState.GetStressRollModifier(actor.Stress);
            if (value != 0)
                result.Add(new DifficultyModifierInfo { Value = value, Reason = "心绪不宁" });
            return result;
        }

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

        // 掷骰中：命运条本身就是动画（减速扫掠 → 落格弹跳 → 定格），而非一个方框里跳变的数字。
        private static void DrawLocalRoll(Rect rect, ActionReport report, int phase, int displayDieValue, float displayScale)
        {
            var panel = new Rect(rect.x + 12f, rect.yMax - 66f, rect.width - 24f, 54f);
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

        // 结算结果 = 命运条「原地定格」+ 结果从其下方揭开。头部与 DrawLocalRoll 落定态同位置、同内容，
        // 形成无缝冻结（动画一停，命运条就留在原处）；身体（叙事 + 影响）随揭开轻微下滑弹出。
        private static void DrawResiduePanel(Rect rect, CardPresentationResidue residue)
        {
            // residue 只有动画落定后才被绘制——首帧即结果该「揭开」的时刻，惰性记录起点。
            if (residue.RevealStartTime <= 0f)
                residue.RevealStartTime = Time.time;
            float reveal = Mathf.Clamp01((Time.time - residue.RevealStartTime) / 0.28f);
            float ease = 1f - Mathf.Pow(1f - reveal, 3f);

            float bodyH = 0f;
            if (!string.IsNullOrWhiteSpace(residue.Subtitle)) bodyH += 20f;
            int effectRows = Mathf.Min(3, residue.Effects.Count);
            if (effectRows > 0) bodyH += effectRows * 16f + 4f;

            const float headerH = 54f;
            // 头部与 DrawLocalRoll 落定态同位置（rect.yMax-66, 高 54）→ 原地冻结；身体向下延展。
            var panel = new Rect(rect.x + 12f, rect.yMax - 66f, rect.width - 24f, headerH + bodyH);
            IMGUIStyles.DrawShadow(panel, new Vector2(4f, 5f), 0.45f);
            GUI.color = new Color(IMGUIStyles.Ink.r, IMGUIStyles.Ink.g, IMGUIStyles.Ink.b, IMGUIStyles.ModalOpacity);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var header = new Rect(panel.x, panel.y, panel.width, headerH);
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
            float y = header.yMax + 4f + (1f - ease) * 5f;
            if (!string.IsNullOrWhiteSpace(residue.Subtitle))
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

        // 同一语义永远同一张纸：工作=绿纸、中风险=黄纸、高风险=橙红纸、交涉=蓝纸、机遇=紫纸。
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
                case "中风险":
                    return (new Color(0.78f, 0.65f, 0.35f, 1f), new Color(0.25f, 0.16f, 0.02f, 1f));
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
