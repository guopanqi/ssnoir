#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    public static class CardDrawer
    {
        public struct CardInteraction
        {
            public bool CardClicked;
            public int ClickedSlotIndex;
            public int DroppedSlotIndex;
            public bool ExecuteClicked;
        }

        public static CardInteraction DrawCard(Rect rect, GameNode node, bool isHovered, bool isFlipped, bool isFocused,
            List<SlottedResource?>? slotted, List<GameClock> clocks, string backText,
            IMGUIInteractionContext ui, SSNoirGameManager gameManager, bool isExecuting = false, float executeProgress = 0f, string executingText = "执行中",
            ActionReport? localRoll = null, int localRollPhase = 0, int localRollDisplayDieValue = 1, float localRollDisplayScale = 1f,
            CardPresentationResidue? residue = null)
        {
            var interaction = new CardInteraction { CardClicked = false, ClickedSlotIndex = -1, DroppedSlotIndex = -1, ExecuteClicked = false };
            bool disabled = node.Disabled;

            if (isFlipped)
            {
                DrawFlippedCard(rect, node, backText, isHovered && !disabled, ui, ref interaction, gameManager);
                if (disabled) interaction.CardClicked = false;
                return interaction;
            }

            // ── Location Capsule Dispatch (when not focused) ──
            bool isLocation = node.IsContainer;
            if (isLocation && !isFocused)
            {
                DrawLocationLabel(rect, node.Name, isHovered);
                if (!disabled && isHovered && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    interaction.CardClicked = true;
                    Event.current.Use();
                }
                return interaction;
            }

            // Determine node type label (colors are now uniform Ink/Paper per DESIGN.md;
            // type no longer changes the outline hue — only the sticky-note tag does).
            string typeLabel = "地点";
            if (node.IsContainer)
            {
                typeLabel = "地点";
            }
            else if (node.Resolve != null)
            {
                if (node.Resolve.Type == ResolveType.Instant) typeLabel = "行动";
                else if (node.Resolve.Type == ResolveType.Roll) typeLabel = "判定";
                else if (node.Resolve.Type == ResolveType.Observe) typeLabel = "观察";
            }

            // Ink fill + 纸白描边（70%默认/100%悬停）+ 内侧 20% 细线 + 硬投影 + 金选中框
            Color outlineColor = disabled
                ? new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.35f)
                : isFocused
                    ? IMGUIStyles.Gold
                    : isHovered
                        ? new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 1f)
                        : new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.72f);
            float outlineThickness = (isHovered || isFocused) ? 2f : 1f;

            IMGUIStyles.DrawShadow(rect, new Vector2(5f, 6f), 0.48f);

            GUI.color = IMGUIStyles.Ink;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(rect, outlineThickness, outlineColor);
            var innerLineRect = new Rect(rect.x + 3f, rect.y + 3f, rect.width - 6f, rect.height - 6f);
            if (innerLineRect.width > 0 && innerLineRect.height > 0)
            {
                IMGUIStyles.DrawOutline(innerLineRect, 1f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.20f));
            }

            // "正在发生"用金光呼吸而非稳定描边高亮
            if (isFocused && !disabled)
            {
                IMGUIStyles.DrawGoldPulse(rect);
            }

            // Draw clock badges (top-right)
            if (clocks != null && clocks.Count > 0)
            {
                float badgeX = rect.x + rect.width - 6;
                float badgeY = rect.y + 4;
                foreach (var clock in clocks)
                {
                    DrawClockBadge(ref badgeX, badgeY, clock);
                }
            }

            bool hasRequires = node.Requires != null && node.Requires.Count > 0 && slotted != null && slotted.Count == node.Requires.Count;
            bool showButton = hasRequires || (node.Resolve != null && node.Resolve.Type == ResolveType.Instant);
            bool hasSubtitle = !string.IsNullOrWhiteSpace(node.Subtitle);

            // Bottom of the execute button; used to gate the odds preview so it only shows
            // when the card is tall enough to leave room below the button.
            float executeBottomY = rect.yMax;

            // Title
            float titleY = showButton ? rect.y + 12 : rect.y + rect.height / 2f - (hasSubtitle ? 34 : 24);
            var titleStyle = new GUIStyle(IMGUIStyles.CardTitle);
            if (disabled) titleStyle.normal.textColor = IMGUIStyles.TextSecondary;
            GUI.Label(new Rect(rect.x + 10f, titleY, rect.width - 20f, 28), node.Name, titleStyle);

            float subtitleBottomY = titleY + 28f;
            if (hasSubtitle)
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
                float subtitleHeight = Mathf.Clamp(measuredHeight, 20f, 40f);
                float subtitleY = titleY + 28f;
                GUI.Label(new Rect(rect.x + 12f, subtitleY, subtitleWidth, subtitleHeight), node.Subtitle, subtitleStyle);
                subtitleBottomY = subtitleY + subtitleHeight;
            }

            // Skill badge (roll cards) or plain type label. The skill name tells the player
            // which ability this action tests, replacing the meaningless "判定" label.
            bool isRoll = node.Resolve != null && node.Resolve.Type == ResolveType.Roll && !string.IsNullOrEmpty(node.Resolve.SkillName);
            float typeY = showButton
                ? (hasSubtitle ? Mathf.Max(rect.y + 64f, subtitleBottomY + 2f) : rect.y + 48f)
                : rect.y + rect.height / 2f + 6f;
            if (isRoll && !disabled)
            {
                DrawSkillBadge(rect, typeY, SkillInfo.DisplayName(node.Resolve!.SkillName));
            }
            else
            {
                GUI.Label(new Rect(rect.x + 10f, typeY, rect.width - 20f, 22), $"— {typeLabel} —", IMGUIStyles.CardSubtitle);
            }

            DrawNodeTags(rect, node.Tags, hasRequires ? rect.y + 8f : (showButton ? rect.y + (hasSubtitle ? 88f : 72f) : rect.y + 8f));

            if (hasRequires)
            {
                var requires = node.Requires!;
                var slots = slotted!;
                int M = requires.Count;
                float slotW = 46;
                float slotH = 46;
                float spacing = 10;
                float totalWidth = M * slotW + (M - 1) * spacing;
                float slotStartX = rect.x + (rect.width - totalWidth) / 2f;
                float slotY = rect.y + (hasSubtitle ? 92 : 78);
                if (hasSubtitle)
                {
                    slotY = Mathf.Max(slotY, typeY + 24f);
                }

                for (int j = 0; j < M; j++)
                {
                    var slotRect = new Rect(slotStartX + j * (slotW + spacing), slotY, slotW, slotH);
                    bool slotHover = !disabled && ui.CanHover(slotRect);
                    var res = slots[j];
                    bool canMatchHeld = !disabled && gameManager.CanMatchRequirement(requires[j]);
                    bool canDropHeld = !disabled && gameManager.CanPlaceSelectedResource(node, j);

                    // 骰位/数值格在暗卡上反转为白底黑字。空槽用 Ink 底 + 白线；可放/可匹配态用金/印章红描边提示。
                    if (res == null)
                    {
                        Color border = canDropHeld
                            ? IMGUIStyles.Gold
                            : (canMatchHeld ? IMGUIStyles.SealRed : (slotHover ? new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 1f) : new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.35f)));

                        GUI.color = IMGUIStyles.Ink;
                        GUI.DrawTexture(slotRect, Texture2D.whiteTexture);
                        GUI.color = Color.white;
                        IMGUIStyles.DrawOutline(slotRect, canDropHeld && slotHover ? 2f : 1f, border);

                        string placeholder = FormatRequirementLabel(requires[j]);
                        int fontSize = placeholder.Length > 2 ? 12 : (placeholder.Length > 1 ? 15 : 19);
                        var pStyle = new GUIStyle(IMGUIStyles.SlotLabel);
                        pStyle.fontSize = fontSize;
                        pStyle.normal.textColor = canDropHeld ? IMGUIStyles.Gold : (canMatchHeld ? IMGUIStyles.SealRed : new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.6f));
                        GUI.Label(slotRect, placeholder, pStyle);
                    }
                    else
                    {
                        Color border = canMatchHeld ? IMGUIStyles.SealRed : (slotHover ? IMGUIStyles.Gold : new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.4f));

                        GUI.color = IMGUIStyles.Paper;
                        GUI.DrawTexture(slotRect, Texture2D.whiteTexture);
                        GUI.color = Color.white;
                        IMGUIStyles.DrawOutline(slotRect, canMatchHeld ? 2f : 1f, border);

                        string valStr = FormatSlottedLabel(res);
                        int fontSize = valStr.Length > 2 ? 12 : (valStr.Length > 1 ? 15 : 19);
                        var vStyle = new GUIStyle(IMGUIStyles.SlotLabel);
                        vStyle.fontSize = fontSize;
                        vStyle.normal.textColor = IMGUIStyles.PaperInk;
                        GUI.Label(slotRect, valStr, vStyle);
                    }

                    if (!disabled && ui.WasClicked(slotRect))
                    {
                        interaction.ClickedSlotIndex = j;
                        Event.current.Use();
                    }
                    else if (canDropHeld
                        && slotHover
                        && Event.current.type == EventType.MouseUp
                        && Event.current.button == 0)
                    {
                        interaction.DroppedSlotIndex = j;
                        Event.current.Use();
                    }
                }

                // Execute button
                float exeW = 112;
                float exeH = 28;
                float exeX = rect.x + (rect.width - exeW) / 2f;
                float exeY = slotY + slotH + 10;
                var exeRect = new Rect(exeX, exeY, exeW, exeH);
                executeBottomY = exeY + exeH;

                bool allFilled = slotted != null && slotted.All(s => s != null);
                if (isExecuting)
                {
                    DrawExecuteProgress(exeRect, executeProgress, executingText);
                }
                else if (allFilled && !disabled)
                {
                    if (DrawExecuteButton(exeRect, "执 行", ui, true))
                    {
                        interaction.ExecuteClicked = true;
                    }
                }
                else
                {
                    DrawExecuteButton(exeRect, disabled ? "不可用" : "待 命", ui, false);
                }
            }
            else if (showButton)
            {
                // For instant-action cards (no requirements, but show button)
                float exeW = 112;
                float exeH = 30;
                float exeX = rect.x + (rect.width - exeW) / 2f;
                float exeY = rect.y + (hasSubtitle ? 124 : 100);
                if (hasSubtitle)
                {
                    exeY = Mathf.Max(exeY, typeY + 26f);
                }
                var exeRect = new Rect(exeX, exeY, exeW, exeH);

                if (isExecuting)
                {
                    DrawExecuteProgress(exeRect, executeProgress, executingText);
                }
                else if (DrawExecuteButton(exeRect, disabled ? "不可用" : "执 行", ui, !disabled))
                {
                    interaction.ExecuteClicked = true;
                }
            }
            else
            {
                // Simple card click
                if (!disabled && ui.WasClicked(rect))
                {
                    interaction.CardClicked = true;
                    Event.current.Use();
                }
            }

            // 修正标签（卡左侧）：便签形态。负修正 = 高风险纸，正修正 = 工作纸，中性 = 交涉纸。
            var modifiers = node.Resolve?.DifficultyModifiers.Count > 0 ? node.Resolve.DifficultyModifiers : null;
            if (modifiers != null && modifiers.Count > 0)
            {
                for (int k = 0; k < modifiers.Count; k++)
                {
                    var mod = modifiers[k];
                    string modText = $"{mod.Reason} {(mod.Value > 0 ? "+" : "")}{mod.Value}";

                    var tagStyle = new GUIStyle(GUI.skin.label) { fontSize = 10 };

                    Vector2 textSize = tagStyle.CalcSize(new GUIContent(modText));
                    float tagW = textSize.x + 12;
                    float tagH = 18;
                    float tagX = rect.x - tagW + 6;
                    float tagY = rect.y + 8 + k * 22;
                    var tagRect = new Rect(tagX, tagY, tagW, tagH);

                    Color noteBg = mod.Value < 0 ? IMGUIStyles.StickyHighRiskBg
                                  : (mod.Value > 0 ? IMGUIStyles.StickyWorkBg : IMGUIStyles.StickyNegotiateBg);
                    Color noteText = mod.Value < 0 ? IMGUIStyles.StickyHighRiskText
                                    : (mod.Value > 0 ? IMGUIStyles.StickyWorkText : IMGUIStyles.StickyNegotiateText);
                    // 微旋转 ±1.5°，按行号交替方向，避免整列同角度显得机械。
                    float noteRot = (k % 2 == 0) ? -1.5f : 1.5f;
                    IMGUIStyles.DrawStickyNote(tagRect, modText, noteBg, noteText, noteRot, tagStyle);
                }
            }

            // Right rail: each active party member's level in this card's skill, framed in
            // that actor's theme color — an at-a-glance "who is good at this" comparison.
            var actors = gameManager.DisplayedSnapshot.Actors;
            if (isRoll && actors != null && actors.Count > 0)
            {
                DrawActorAbilityRail(rect, node.Resolve!.SkillName, actors, clocks != null && clocks.Count > 0);
            }

            if (localRoll != null)
            {
                DrawLocalRoll(rect, localRoll, localRollPhase, localRollDisplayDieValue, localRollDisplayScale);
            }
            else if (residue != null)
            {
                DrawResidue(rect, residue);
            }
            else if (isRoll && !disabled && hasRequires && actors != null
                     && rect.yMax - executeBottomY >= 16f)
            {
                // Once a die is placed, preview the three outcome bands — exact, since only
                // the advantage dice are random. Gated to cards with room below the button.
                TryDrawOddsPreview(rect, node.Resolve!.SkillName, slotted!, modifiers, actors, executeBottomY);
            }

            return interaction;
        }

        // 执行/主行动按钮：实心金底 + 深字（DESIGN.md 强调色岗位一：实心金 = 执行）。
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

        private static void DrawSkillBadge(Rect rect, float y, string skillText)
        {
            var style = new GUIStyle(IMGUIStyles.CardSubtitle)
            {
                font = IMGUIStyles.ChineseFont,
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = IMGUIStyles.TextSecondary }
            };
            Vector2 size = style.CalcSize(new GUIContent(skillText));
            float badgeW = size.x + 24f;
            float badgeH = 22f;
            var badge = new Rect(rect.x + (rect.width - badgeW) / 2f, y, badgeW, badgeH);
            GUI.color = IMGUIStyles.Ink;
            GUI.DrawTexture(badge, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(badge, 1f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f));
            GUI.Label(badge, skillText, style);
        }

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
                {
                    continue;
                }
                var (r, g, b) = ActorTheme.ColorFor(actors, actor.Id);
                var color = new Color(r / 255f, g / 255f, b / 255f, 1f);
                var chip = new Rect(x, y + drawn * (chipH + 4f), chipW, chipH);
                GUI.color = new Color(r / 255f * 0.2f, g / 255f * 0.2f, b / 255f * 0.2f, 0.92f);
                GUI.DrawTexture(chip, Texture2D.whiteTexture);
                GUI.color = Color.white;
                IMGUIStyles.DrawOutline(chip, 1.4f, color);

                string shortName = actor.Name.Length > 2 ? actor.Name.Substring(0, 2) : actor.Name;
                var nameStyle = new GUIStyle(GUI.skin.label)
                {
                    font = IMGUIStyles.ChineseFont,
                    fontSize = 10,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = color }
                };
                GUI.Label(new Rect(chip.x + 5f, chip.y, 28f, chipH), shortName, nameStyle);
                var lvlStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleRight,
                    normal = { textColor = IMGUIStyles.TextPrimary }
                };
                GUI.Label(new Rect(chip.x, chip.y, chip.width - 6f, chipH), level.ToString(), lvlStyle);
                drawn++;
            }
        }

        private static void TryDrawOddsPreview(Rect rect, string skill, List<SlottedResource?> slotted,
            List<DifficultyModifierInfo>? modifiers, IReadOnlyList<ActorSnapshot> actors, float executeBottomY)
        {
            SlottedResource? dieSlot = null;
            foreach (var s in slotted)
            {
                if (s != null && s.Type == "die") { dieSlot = s; break; }
            }
            if (dieSlot == null)
            {
                return;
            }

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

        // Compact tri-color proportion bar: the widths of 失败/中性/成功 show the odds split
        // at a glance, with each percentage labeled beneath. Sits in the strip below the button.
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

        private static void DrawLocalRoll(Rect rect, ActionReport report, int phase, int displayDieValue, float displayScale)
        {
            var panel = new Rect(rect.x + 12f, rect.yMax - 66f, rect.width - 24f, 54f);
            GUI.color = new Color(IMGUIStyles.Ink.r, IMGUIStyles.Ink.g, IMGUIStyles.Ink.b, IMGUIStyles.ModalOpacity);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(panel, 1.5f, IMGUIStyles.Gold);

            // 掷骰瞬间是"正在发生"——金光呼吸只在滚动阶段亮起。
            if (phase < 2)
            {
                IMGUIStyles.DrawGoldPulse(panel);
            }

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

        private static void DrawResidue(Rect rect, CardPresentationResidue residue)
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

        public static void DrawResidueCard(Rect rect, CardPresentationResidue residue)
        {
            IMGUIStyles.DrawShadow(rect, new Vector2(5f, 6f), 0.48f);
            GUI.color = IMGUIStyles.Ink;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawDoubleOutline(rect, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.72f));

            // 结果盖印（右上角）：成=金、败=印章红。
            if (residue.RollOutcome == RollOutcome.Success || residue.RollOutcome == RollOutcome.Fail)
            {
                var sealRect = new Rect(rect.xMax - 60f, rect.y + 8f, 48f, 48f);
                Color sealColor = residue.RollOutcome == RollOutcome.Success ? IMGUIStyles.Gold : IMGUIStyles.SealRed;
                string sealText = residue.RollOutcome == RollOutcome.Success ? "成" : "败";
                IMGUIStyles.DrawStampSeal(sealRect, sealText, sealColor);
            }

            string label = residue.RollOutcome.HasValue ? FormatOutcome(residue.RollOutcome.Value) : "行动结果";
            var labelStyle = new GUIStyle(IMGUIStyles.CardSubtitle)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(new Rect(rect.x + 12f, rect.y + 14f, rect.width - 24f, 22f), label, labelStyle);

            string title = residue.RollOutcome.HasValue
                ? $"{FormatOutcome(residue.RollOutcome.Value)}：{residue.Title}"
                : residue.Title;
            var titleStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                fontSize = 15,
                wordWrap = true,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = residue.RollOutcome.HasValue ? OutcomeColor(residue.RollOutcome.Value) : IMGUIStyles.TextPrimary }
            };
            GUI.Label(new Rect(rect.x + 18f, rect.y + 46f, rect.width - 36f, 42f), title, titleStyle);

            var subtitleStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                fontSize = 12,
                wordWrap = true,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = IMGUIStyles.TextSecondary }
            };
            GUI.Label(new Rect(rect.x + 18f, rect.y + 94f, rect.width - 36f, rect.height - 106f), residue.Subtitle, subtitleStyle);
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

        // 标签配色：便签色板（DESIGN.md）。同一语义永远同一张纸。
        private static (Color bg, Color text) TagColors(string label)
        {
            switch (label)
            {
                case "交锋": // 高危：高风险纸
                    return (IMGUIStyles.StickyHighRiskBg, IMGUIStyles.StickyHighRiskText);
                case "工作":
                case "低风险": // 安全/能赚钱：工作纸
                    return (IMGUIStyles.StickyWorkBg, IMGUIStyles.StickyWorkText);
                case "中风险":
                    return (IMGUIStyles.StickyMidRiskBg, IMGUIStyles.StickyMidRiskText);
                case "高风险":
                case "越界":
                    return (IMGUIStyles.StickyHighRiskBg, IMGUIStyles.StickyHighRiskText);
                case "机遇":
                    return (IMGUIStyles.StickyOpportunityBg, IMGUIStyles.StickyOpportunityText);
                default: // 交涉及其他
                    return (IMGUIStyles.StickyNegotiateBg, IMGUIStyles.StickyNegotiateText);
            }
        }

        private static void DrawNodeTags(Rect rect, List<string>? tags, float startY)
        {
            if (tags == null || tags.Count == 0)
            {
                return;
            }

            float x = rect.x + 8f;
            float y = startY;
            float maxRight = rect.xMax - 8f;

            for (int i = 0; i < tags.Count; i++)
            {
                string label = tags[i];
                if (string.IsNullOrWhiteSpace(label))
                {
                    continue;
                }

                var (bg, text) = TagColors(label);
                var style = new GUIStyle(GUI.skin.label)
                {
                    font = IMGUIStyles.ChineseFont,
                    fontSize = 11,
                };

                Vector2 size = style.CalcSize(new GUIContent(label));
                float tagW = Mathf.Min(size.x + 14f, rect.width - 16f);
                if (x + tagW > maxRight)
                {
                    x = rect.x + 8f;
                    y += 21f;
                }

                var tagRect = new Rect(x, y, tagW, 18f);

                // 便签形态：微旋转 ±1.5° + 1px 硬投影 + 彩纸底 + 深色字。
                float noteRot = (i % 2 == 0) ? -1.5f : 1.5f;
                IMGUIStyles.DrawStickyNote(tagRect, label, bg, text, noteRot, style);

                x += tagW + 6f;
            }
        }

        private static string FormatRequirementLabel(ActionCost requirement)
        {
            if (requirement.Type == "die")
            {
                return "D";
            }

            if (string.IsNullOrEmpty(requirement.ItemId))
            {
                return "?";
            }

            return requirement.Qty > 1
                ? $"{requirement.ItemId}x{requirement.Qty}"
                : requirement.ItemId;
        }

        private static string FormatSlottedLabel(SlottedResource resource)
        {
            if (resource.Type == "die")
            {
                return resource.Value.ToString();
            }

            string itemName = string.IsNullOrEmpty(resource.ItemId) ? "?" : resource.ItemId;
            int qty = resource.Qty > 0 ? resource.Qty : resource.Value;
            return qty > 1 ? $"{itemName}x{qty}" : itemName;
        }

        private static void DrawFlippedCard(Rect rect, GameNode node, string backText, bool isHovered, IMGUIInteractionContext ui,
            ref CardInteraction interaction, SSNoirGameManager gameManager)
        {
            // 悬停变亮不变色：白线 70% → 100%。
            Color outline = isHovered
                ? new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 1f)
                : new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.70f);
            float thickness = isHovered ? 2f : 1f;

            IMGUIStyles.DrawShadow(rect, new Vector2(4f, 4f), 0.45f);
            GUI.color = IMGUIStyles.Ink;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(rect, thickness, outline);

            // Title
            GUI.Label(new Rect(rect.x + 8, rect.y + 8, rect.width - 16, 20), node.Name, IMGUIStyles.FlippedTitle);

            // Subtitle
            GUI.Label(new Rect(rect.x + 8, rect.y + 28, rect.width - 16, 16), "— 已解读线索 —", IMGUIStyles.FlippedTip);

            // Content
            var contentRect = new Rect(rect.x + 8, rect.y + 48, rect.width - 16, rect.height - 68);
            string clueText = node.Resolve != null ? node.Resolve.ObserveText : "";
            GUI.Label(contentRect, clueText, IMGUIStyles.FlippedContent);

            // Tip
            GUI.Label(new Rect(rect.x + 8, rect.y + rect.height - 18, rect.width - 16, 14), "点击返回", IMGUIStyles.FlippedTip);

            if (ui.WasClicked(rect))
            {
                interaction.CardClicked = true;
                Event.current.Use();
            }
        }

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

            // 进度中的按钮是"正在发生"的东西。
            IMGUIStyles.DrawGoldPulse(rect);

            string label = string.IsNullOrEmpty(text) ? "执行中" : text;
            if (label.Length > 5)
            {
                label = "执行中";
            }
            var progressStyle = new GUIStyle(IMGUIStyles.ExecuteLabel);
            progressStyle.normal.textColor = progress > 0.5f ? IMGUIStyles.GoldOnDark : IMGUIStyles.Paper;
            GUI.Label(rect, label, progressStyle);
        }

        private static void DrawClockBadge(ref float rightX, float topY, GameClock clock)
        {
            Color activeColor = IMGUIStyles.Gold;
            Color inactiveColor = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.25f);

            if (clock.Style == ClockStyle.Countdown)
            {
                string text = $"{clock.Label} {clock.Current}/{clock.Max}";
                int fontSize = 10;
                float textWidth = 80;
                float badgeW = textWidth + 8;
                float badgeH = 14;
                float badgeX = rightX - badgeW;
                float badgeY = topY;

                GUI.color = IMGUIStyles.Ink;
                GUI.DrawTexture(new Rect(badgeX, badgeY, badgeW, badgeH), Texture2D.whiteTexture);
                GUI.color = Color.white;
                IMGUIStyles.DrawOutline(new Rect(badgeX, badgeY, badgeW, badgeH), 1f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f));

                var style = new GUIStyle(IMGUIStyles.ClockLabel);
                style.fontSize = fontSize;
                style.alignment = TextAnchor.MiddleCenter;
                GUI.Label(new Rect(badgeX, badgeY, badgeW, badgeH), text, style);

                rightX -= (badgeW + 4);
            }
            else if (clock.Style == ClockStyle.Segments)
            {
                string labelText = clock.Label;
                int fontSize = 10;
                float labelWidth = 40;
                int dotSize = 5;
                int spacing = 2;
                float dotsW = clock.Max * (dotSize + spacing) - spacing;
                float badgeW = labelWidth + 6 + dotsW + 6;
                float badgeH = 14;
                float badgeX = rightX - badgeW;
                float badgeY = topY;

                GUI.color = IMGUIStyles.Ink;
                GUI.DrawTexture(new Rect(badgeX, badgeY, badgeW, badgeH), Texture2D.whiteTexture);
                GUI.color = Color.white;
                IMGUIStyles.DrawOutline(new Rect(badgeX, badgeY, badgeW, badgeH), 1f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f));

                var style = new GUIStyle(IMGUIStyles.ClockLabel);
                style.fontSize = fontSize;
                GUI.Label(new Rect(badgeX + 4, badgeY, labelWidth, badgeH), labelText, style);

                float dotStartX = badgeX + 4 + labelWidth + 4;
                for (int i = 0; i < clock.Max; i++)
                {
                    var dotRect = new Rect(dotStartX + i * (dotSize + spacing), badgeY + (badgeH - dotSize) / 2f, dotSize, dotSize);
                    if (i < clock.Current)
                    {
                        GUI.color = activeColor;
                        GUI.DrawTexture(dotRect, Texture2D.whiteTexture);
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

                rightX -= (badgeW + 4);
            }
            else // Pie
            {
                string labelText = clock.Label;
                int fontSize = 10;
                float labelWidth = 40;
                float pieRadius = 10f;
                float badgeW = labelWidth + 6 + pieRadius * 2 + 6;
                float badgeH = 14;
                float badgeX = rightX - badgeW;
                float badgeY = topY;

                GUI.color = IMGUIStyles.Ink;
                GUI.DrawTexture(new Rect(badgeX, badgeY, badgeW, badgeH), Texture2D.whiteTexture);
                GUI.color = Color.white;
                IMGUIStyles.DrawOutline(new Rect(badgeX, badgeY, badgeW, badgeH), 1f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f));

                var style = new GUIStyle(IMGUIStyles.ClockLabel);
                style.fontSize = fontSize;
                GUI.Label(new Rect(badgeX + 4, badgeY, labelWidth, badgeH), labelText, style);

                // Real pie sector
                float pieX = badgeX + 4 + labelWidth + 4;
                float pieY = badgeY + (badgeH - pieRadius * 2) / 2f;
                var pieRect = new Rect(pieX, pieY, pieRadius * 2, pieRadius * 2);
                float fillPct = clock.Max > 0 ? Mathf.Clamp01((float)clock.Current / clock.Max) : 0f;
                PieDrawer.DrawPieBadge(pieRect, fillPct, activeColor, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.70f));

                rightX -= (badgeW + 4);
            }
        }

        private static void DrawLocationLabel(Rect rect, string name, bool isHovered)
        {
            // 地点小标签：Ink 底 + 1px 纸白描边（70% 默认 / 100% 悬停，变亮不变色）。
            Color border = isHovered
                ? new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 1f)
                : new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.70f);

            IMGUIStyles.DrawShadow(rect, new Vector2(3f, 3f), 0.45f);
            GUI.color = IMGUIStyles.Ink;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(rect, 1f, border);

            var labelStyle = new GUIStyle(IMGUIStyles.CardTitle)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
            labelStyle.normal.textColor = isHovered
                ? IMGUIStyles.Paper
                : new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.85f);
            GUI.Label(rect, name, labelStyle);
        }
    }
}
