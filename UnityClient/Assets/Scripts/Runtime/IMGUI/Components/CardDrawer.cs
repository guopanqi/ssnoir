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

            // Determine node type and colors
            string typeLabel = "地点";
            Color normalColor = IMGUIStyles.CardBg;
            Color hoverColor = IMGUIStyles.CardHoverBg;
            Color outlineNormal = IMGUIStyles.CardOutline;
            Color outlineHover = IMGUIStyles.CardHoverOutline;

            if (node.IsContainer)
            {
                typeLabel = "地点";
            }
            else if (node.Resolve != null)
            {
                if (node.Resolve.Type == ResolveType.Instant)
                {
                    typeLabel = "行动";
                    outlineNormal = IMGUIStyles.OutlineVariantColor;
                    outlineHover = IMGUIStyles.SecondaryColor;
                }
                else if (node.Resolve.Type == ResolveType.Roll)
                {
                    typeLabel = "判定";
                    outlineNormal = IMGUIStyles.OutlineColor;
                    outlineHover = IMGUIStyles.TertiaryColor; // Burnt Amber alert/POIs
                }
                else if (node.Resolve.Type == ResolveType.Observe)
                {
                    typeLabel = "观察";
                    outlineNormal = IMGUIStyles.OutlineColor;
                    outlineHover = IMGUIStyles.SecondaryColor;
                }
            }

            if (disabled)
            {
                normalColor = new Color(0.10f, 0.10f, 0.11f, 0.96f);
                hoverColor = normalColor;
                outlineNormal = new Color(0.28f, 0.28f, 0.30f, 1f);
                outlineHover = outlineNormal;
            }

            Color bgColor = isHovered ? hoverColor : (isFocused ? hoverColor : normalColor);
            Color outlineColor = disabled
                ? outlineNormal
                : isHovered ? outlineHover : (isFocused ? IMGUIStyles.PrimaryColor : outlineNormal);
            float outlineThickness = (isHovered || isFocused) ? 2f : 1f;

            // Draw card background
            GUI.color = bgColor;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(rect, outlineThickness, outlineColor);

            // Draw subtle horizontal scanline animation over active focused panels
            if (isFocused && !disabled)
            {
                IMGUIStyles.DrawScanLine(rect, new Color(0.671f, 0.780f, 1.0f, 0.15f), 100f, 1.5f);
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
            if (disabled) titleStyle.normal.textColor = IMGUIStyles.OnSurfaceVariant;
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
                if (disabled) subtitleStyle.normal.textColor = IMGUIStyles.OnSurfaceVariant;
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

                    if (res == null)
                    {
                        Color fill = canDropHeld
                            ? (slotHover ? new Color(0.12f, 0.30f, 0.26f, 0.85f) : new Color(0.11f, 0.15f, 0.28f, 0.75f))
                            : (canMatchHeld ? new Color(0.30f, 0.12f, 0.15f, 0.65f) : IMGUIStyles.SlotEmpty);
                        Color border = canDropHeld
                            ? (slotHover ? new Color(0.43f, 0.95f, 0.80f, 1f) : new Color(0.63f, 0.74f, 1f, 1f))
                            : (canMatchHeld ? new Color(0.55f, 0.28f, 0.34f, 1f) : (slotHover ? IMGUIStyles.PrimaryColor : IMGUIStyles.SlotEmptyBorder));

                        GUI.color = fill;
                        GUI.DrawTexture(slotRect, Texture2D.whiteTexture);
                        GUI.color = Color.white;
                        IMGUIStyles.DrawOutline(slotRect, canDropHeld && slotHover ? 2f : 1f, border);

                        string placeholder = FormatRequirementLabel(requires[j]);
                        int fontSize = placeholder.Length > 2 ? 12 : (placeholder.Length > 1 ? 15 : 19);
                        var pStyle = new GUIStyle(IMGUIStyles.SlotLabel);
                        pStyle.fontSize = fontSize;
                        pStyle.normal.textColor = canDropHeld ? Color.white : (canMatchHeld ? IMGUIStyles.ErrorColor : IMGUIStyles.OnSurfaceVariant);
                        GUI.Label(slotRect, placeholder, pStyle);
                    }
                    else
                    {
                        GUI.color = canMatchHeld ? new Color(0.32f, 0.36f, 0.56f, 0.9f) : IMGUIStyles.SlotFilled;
                        GUI.DrawTexture(slotRect, Texture2D.whiteTexture);
                        GUI.color = Color.white;
                        IMGUIStyles.DrawOutline(slotRect, canMatchHeld ? 2f : 1f, canMatchHeld ? new Color(0.75f, 0.84f, 1f, 1f) : IMGUIStyles.SlotFilledBorder);

                        string valStr = FormatSlottedLabel(res);
                        int fontSize = valStr.Length > 2 ? 12 : (valStr.Length > 1 ? 15 : 19);
                        var vStyle = new GUIStyle(IMGUIStyles.SlotLabel);
                        vStyle.fontSize = fontSize;
                        vStyle.normal.textColor = Color.white;
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
                    if (IMGUIButton.Draw(exeRect, "执行", ui, IMGUIStyles.PrimaryColor, IMGUIStyles.ExecuteBtnHover, IMGUIStyles.ExecuteLabel))
                    {
                        interaction.ExecuteClicked = true;
                    }
                }
                else
                {
                    IMGUIButton.Draw(exeRect, disabled ? "不可用" : "待命", ui, IMGUIStyles.OutlineVariantColor, Color.clear, IMGUIStyles.ExecuteLabel, false);
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
                else if (IMGUIButton.Draw(exeRect, disabled ? "不可用" : "执行", ui, IMGUIStyles.PrimaryColor, IMGUIStyles.ExecuteBtnHover, IMGUIStyles.ExecuteLabel, !disabled))
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

            // Draw Modifier Tags on the left side of the card
            var modifiers = node.Resolve?.DifficultyModifiers.Count > 0 ? node.Resolve.DifficultyModifiers : null;
            if (modifiers != null && modifiers.Count > 0)
            {
                for (int k = 0; k < modifiers.Count; k++)
                {
                    var mod = modifiers[k];
                    string modText = $"{mod.Reason} {(mod.Value > 0 ? "+" : "")}{mod.Value}";
                    
                    var tagStyle = new GUIStyle(GUI.skin.label);
                    tagStyle.fontSize = 10;
                    tagStyle.alignment = TextAnchor.MiddleCenter;
                    tagStyle.normal.textColor = Color.white;

                    Vector2 textSize = tagStyle.CalcSize(new GUIContent(modText));
                    float tagW = textSize.x + 12;
                    float tagH = 18;
                    float tagX = rect.x - tagW + 6;
                    float tagY = rect.y + 8 + k * 22;
                    var tagRect = new Rect(tagX, tagY, tagW, tagH);

                    Color tagBg = mod.Value < 0 ? new Color(0.412f, 0.0f, 0.020f, 0.85f) 
                                 : (mod.Value > 0 ? new Color(0.0f, 0.184f, 0.40f, 0.85f) : IMGUIStyles.SlotEmpty);
                    Color tagBorder = mod.Value < 0 ? IMGUIStyles.ErrorColor 
                                     : (mod.Value > 0 ? IMGUIStyles.PrimaryColor : IMGUIStyles.OutlineColor);

                    GUI.color = tagBg;
                    GUI.DrawTexture(tagRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    IMGUIStyles.DrawOutline(tagRect, 1f, tagBorder);

                    GUI.Label(tagRect, modText, tagStyle);
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

        private static void DrawSkillBadge(Rect rect, float y, string skillText)
        {
            var style = new GUIStyle(IMGUIStyles.CardSubtitle)
            {
                font = IMGUIStyles.ChineseFont,
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.737f, 0.847f, 1f, 1f) }
            };
            Vector2 size = style.CalcSize(new GUIContent(skillText));
            float badgeW = size.x + 24f;
            float badgeH = 22f;
            var badge = new Rect(rect.x + (rect.width - badgeW) / 2f, y, badgeW, badgeH);
            GUI.color = new Color(0.063f, 0.114f, 0.20f, 1f);
            GUI.DrawTexture(badge, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(badge, 1f, new Color(0.247f, 0.427f, 0.69f, 1f));
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
                    normal = { textColor = new Color(0.92f, 0.94f, 1f, 1f) }
                };
                GUI.Label(new Rect(chip.x, chip.y, chip.width - 6f, chipH), level.ToString(), lvlStyle);
                drawn++;
            }
        }

        private static readonly Color OddsFailColor    = new Color(0.82f, 0.227f, 0.29f, 1f);
        private static readonly Color OddsNeutralColor  = new Color(0.839f, 0.663f, 0.306f, 1f);
        private static readonly Color OddsSuccessColor  = new Color(0.349f, 0.706f, 0.467f, 1f);

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
                (odds.Fail,    OddsFailColor),
                (odds.Neutral, OddsNeutralColor),
                (odds.Success, OddsSuccessColor),
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
            GUI.color = new Color(0.07f, 0.08f, 0.12f, 0.96f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(panel, 1.5f, IMGUIStyles.ClockActive);

            int dieValue = phase == 0 ? displayDieValue : report.FinalRollValue;
            var dieStyle = new GUIStyle(IMGUIStyles.CardTitle)
            {
                fontSize = Mathf.RoundToInt(26 * Mathf.Clamp(displayScale, 0.8f, 1.35f)),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = IMGUIStyles.ClockActive }
            };
            GUI.Label(new Rect(panel.x + 10f, panel.y + 4f, 52f, panel.height - 8f), $"D{dieValue}", dieStyle);

            string label = phase >= 2 ? FormatOutcome(report.Outcome) : "判定中";
            var labelStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                fontSize = 15,
                normal = { textColor = phase >= 2 ? OutcomeColor(report.Outcome) : IMGUIStyles.OnSurface }
            };
            GUI.Label(new Rect(panel.x + 72f, panel.y + 9f, panel.width - 82f, 22f), label, labelStyle);

            string detail = phase >= 2 ? $"最终值 {report.ModifiedRollValue}" : "骰子滚动...";
            GUI.Label(new Rect(panel.x + 72f, panel.y + 30f, panel.width - 82f, 18f), detail, IMGUIStyles.ModalBody);
        }

        private static void DrawResidue(Rect rect, CardPresentationResidue residue)
        {
            var panel = new Rect(rect.x + 12f, rect.yMax - 68f, rect.width - 24f, 56f);
            GUI.color = new Color(0.08f, 0.09f, 0.14f, 0.96f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(panel, 1f, IMGUIStyles.PrimaryColor);

            string title = residue.RollOutcome.HasValue
                ? $"{FormatOutcome(residue.RollOutcome.Value)}：{residue.Title}"
                : residue.Title;
            var titleStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = residue.RollOutcome.HasValue ? OutcomeColor(residue.RollOutcome.Value) : IMGUIStyles.OnSurface }
            };
            GUI.Label(new Rect(panel.x + 8f, panel.y + 6f, panel.width - 16f, 22f), title, titleStyle);

            var subtitleStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                fontSize = 11,
                wordWrap = true,
                normal = { textColor = IMGUIStyles.OnSurfaceVariant }
            };
            GUI.Label(new Rect(panel.x + 8f, panel.y + 28f, panel.width - 16f, 24f), residue.Subtitle, subtitleStyle);
        }

        public static void DrawResidueCard(Rect rect, CardPresentationResidue residue)
        {
            GUI.color = new Color(0.08f, 0.09f, 0.14f, 0.98f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(rect, 1.5f, IMGUIStyles.PrimaryColor);

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
                normal = { textColor = residue.RollOutcome.HasValue ? OutcomeColor(residue.RollOutcome.Value) : IMGUIStyles.OnSurface }
            };
            GUI.Label(new Rect(rect.x + 18f, rect.y + 46f, rect.width - 36f, 42f), title, titleStyle);

            var subtitleStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                fontSize = 12,
                wordWrap = true,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = IMGUIStyles.OnSurfaceVariant }
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
                _ => IMGUIStyles.OnSurface
            };
        }

        // 标签配色：工作/风险标签全局一致，与终端 CardWidget.TagColors 保持一致。
        private static (Color bg, Color border, Color text) TagColors(string label)
        {
            switch (label)
            {
                case "交锋":
                    return (new Color(0.37f, 0.13f, 0.13f, 0.90f), new Color(0.82f, 0.34f, 0.30f, 1f), new Color(1f, 0.84f, 0.80f, 1f));
                case "工作": // 能赚钱：青绿
                    return (new Color(0.11f, 0.23f, 0.25f, 0.86f), new Color(0.35f, 0.71f, 0.75f, 1f), new Color(0.82f, 0.94f, 0.96f, 1f));
                case "低风险": // 绿
                    return (new Color(0.13f, 0.26f, 0.17f, 0.86f), new Color(0.38f, 0.75f, 0.47f, 1f), new Color(0.84f, 0.96f, 0.86f, 1f));
                case "中风险": // 琥珀
                    return (new Color(0.31f, 0.24f, 0.10f, 0.88f), new Color(0.84f, 0.66f, 0.27f, 1f), new Color(1f, 0.93f, 0.78f, 1f));
                case "高风险": // 红
                    return (new Color(0.35f, 0.16f, 0.13f, 0.88f), new Color(0.84f, 0.38f, 0.29f, 1f), new Color(1f, 0.86f, 0.80f, 1f));
                case "越界": // 深红：越界/掉关系
                    return (new Color(0.27f, 0.10f, 0.17f, 0.90f), new Color(0.78f, 0.27f, 0.43f, 1f), new Color(1f, 0.82f, 0.88f, 1f));
                default:
                    return (new Color(0.14f, 0.19f, 0.31f, 0.85f), new Color(0.42f, 0.57f, 0.86f, 1f), new Color(0.86f, 0.92f, 1f, 1f));
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

                var (bg, border, text) = TagColors(label);
                var style = new GUIStyle(GUI.skin.label)
                {
                    font = IMGUIStyles.ChineseFont,
                    fontSize = 11,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = text }
                };

                Vector2 size = style.CalcSize(new GUIContent(label));
                float tagW = Mathf.Min(size.x + 14f, rect.width - 16f);
                if (x + tagW > maxRight)
                {
                    x = rect.x + 8f;
                    y += 21f;
                }

                var tagRect = new Rect(x, y, tagW, 18f);

                GUI.color = bg;
                GUI.DrawTexture(tagRect, Texture2D.whiteTexture);
                GUI.color = Color.white;
                IMGUIStyles.DrawOutline(tagRect, 1f, border);
                GUI.Label(tagRect, label, style);

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
            Color bg = isHovered ? IMGUIStyles.CardHoverBg : IMGUIStyles.FlippedBg;
            Color outline = isHovered ? IMGUIStyles.PrimaryColor : IMGUIStyles.FlippedOutline;
            float thickness = isHovered ? 2f : 1f;

            GUI.color = bg;
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
            GUI.color = IMGUIStyles.ModalBg;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            var fillRect = new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * progress, rect.height - 4f);
            GUI.color = IMGUIStyles.PrimaryColor;
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(rect, 1f, IMGUIStyles.PrimaryColor);

            string label = string.IsNullOrEmpty(text) ? "执行中" : text;
            if (label.Length > 5)
            {
                label = "执行中";
            }
            GUI.Label(rect, label, IMGUIStyles.ExecuteLabel);
        }

        private static void DrawClockBadge(ref float rightX, float topY, GameClock clock)
        {
            Color activeColor = IMGUIStyles.ClockActive;
            Color inactiveColor = IMGUIStyles.ClockInactive;

            if (clock.Style == ClockStyle.Countdown)
            {
                string text = $"{clock.Label} {clock.Current}/{clock.Max}";
                int fontSize = 10;
                float textWidth = 80;
                float badgeW = textWidth + 8;
                float badgeH = 14;
                float badgeX = rightX - badgeW;
                float badgeY = topY;

                GUI.color = IMGUIStyles.SlotEmpty;
                GUI.DrawTexture(new Rect(badgeX, badgeY, badgeW, badgeH), Texture2D.whiteTexture);
                GUI.color = Color.white;
                IMGUIStyles.DrawOutline(new Rect(badgeX, badgeY, badgeW, badgeH), 1f, IMGUIStyles.OutlineColor);

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

                GUI.color = IMGUIStyles.SlotEmpty;
                GUI.DrawTexture(new Rect(badgeX, badgeY, badgeW, badgeH), Texture2D.whiteTexture);
                GUI.color = Color.white;
                IMGUIStyles.DrawOutline(new Rect(badgeX, badgeY, badgeW, badgeH), 1f, IMGUIStyles.OutlineColor);

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
                        IMGUIStyles.DrawOutline(dotRect, 1f, IMGUIStyles.OutlineColor);
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

                GUI.color = IMGUIStyles.SlotEmpty;
                GUI.DrawTexture(new Rect(badgeX, badgeY, badgeW, badgeH), Texture2D.whiteTexture);
                GUI.color = Color.white;
                IMGUIStyles.DrawOutline(new Rect(badgeX, badgeY, badgeW, badgeH), 1f, IMGUIStyles.OutlineColor);

                var style = new GUIStyle(IMGUIStyles.ClockLabel);
                style.fontSize = fontSize;
                GUI.Label(new Rect(badgeX + 4, badgeY, labelWidth, badgeH), labelText, style);

                // Real pie sector
                float pieX = badgeX + 4 + labelWidth + 4;
                float pieY = badgeY + (badgeH - pieRadius * 2) / 2f;
                var pieRect = new Rect(pieX, pieY, pieRadius * 2, pieRadius * 2);
                float fillPct = clock.Max > 0 ? Mathf.Clamp01((float)clock.Current / clock.Max) : 0f;
                PieDrawer.DrawPieBadge(pieRect, fillPct, activeColor, IMGUIStyles.OutlineColor);

                rightX -= (badgeW + 4);
            }
        }

        private static void DrawLocationLabel(Rect rect, string name, bool isHovered)
        {
            Color bgColor = isHovered ? IMGUIStyles.CardHoverBg : IMGUIStyles.CardBg;
            Color border = isHovered ? IMGUIStyles.PrimaryColor : IMGUIStyles.CardOutline;

            GUI.color = bgColor;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(rect, 1f, border);

            var labelStyle = new GUIStyle(IMGUIStyles.CardTitle)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
            labelStyle.normal.textColor = isHovered ? Color.white : new Color(0.8f, 0.85f, 0.95f, 1f);
            GUI.Label(rect, name, labelStyle);
        }
    }
}
