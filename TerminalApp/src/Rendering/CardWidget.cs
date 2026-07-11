using System;
using System.Collections.Generic;
using System.Linq;
using Raylib_cs;
using SSNoir.Core;

namespace SSNoir.Rendering
{
    public static class CardWidget
    {
        public struct CardInteraction
        {
            public bool CardClicked;
            public int ClickedSlotIndex; // mouse pressed on a slot; -1 if none
            public int DroppedSlotIndex; // mouse released on a compatible slot; -1 if none
            public bool ExecuteClicked;
        }

        public static CardInteraction DrawCard(
            Rectangle bounds,
            string name,
            string subtitle,
            string typeLabel,
            bool isHovered,
            List<GameClock> clocks,
            bool isFlipped = false,
            string backText = "",
            List<string>? tags = null,
            List<ActionCost>? requires = null,
            List<SlottedResource?>? slotted = null,
            SSNoir.TerminalApp.Rendering.UiInteractionContext ui = default,
            SelectedResource? heldResource = null,
            IReadOnlyList<bool>? canDropHeldResource = null,
            List<DifficultyModifierInfo>? modifiers = null,
            bool isExecuting = false,
            float executeProgress = 0f,
            string executingText = "执行中",
            ActionReport? localRoll = null,
            int localRollPhase = 0,
            int localRollDisplayDieValue = 1,
            float localRollDisplayScale = 1f,
            CardPresentationResidue? residue = null,
            bool disabled = false,
            string? rollSkill = null,
            IReadOnlyList<ActorSnapshot>? actors = null,
            bool ignoresStressPenalty = false)
        {
            var interaction = new CardInteraction
            {
                CardClicked = false,
                ClickedSlotIndex = -1,
                DroppedSlotIndex = -1,
                ExecuteClicked = false
            };
            List<DifficultyModifierInfo> effectiveModifiers = BuildEffectiveModifiers(
                modifiers, slotted, actors, ignoresStressPenalty);

            Color bgColor = disabled
                ? new Color(28, 28, 32, 255)
                : isHovered ? new Color(50, 50, 70, 255) : new Color(30, 30, 40, 255);
            Color outlineColor = disabled
                ? new Color(65, 65, 70, 255)
                : isHovered ? new Color(130, 130, 220, 255) : new Color(60, 60, 80, 255);
            Color titleColor = disabled
                ? new Color(125, 125, 130, 255)
                : isHovered ? Color.White : new Color(200, 200, 200, 255);
            Color typeColor = disabled
                ? new Color(100, 100, 105, 255)
                : isHovered ? new Color(150, 150, 230, 255) : new Color(110, 110, 130, 255);

            if (isFlipped)
            {
                Color backBg = isHovered ? new Color(65, 45, 55, 255) : new Color(45, 30, 38, 255);
                Color backOutline = isHovered ? new Color(220, 130, 150, 255) : new Color(120, 70, 80, 255);
                Color textColor = new Color(220, 200, 205, 255);

                Raylib.DrawRectangleRounded(bounds, 0.1f, 8, backBg);
                Raylib.DrawRectangleRoundedLinesEx(bounds, 0.1f, 8, 2f, backOutline);

                // Draw wrapped observation text
                int fontSize = 12;
                DrawWrappedText(backText, bounds.X + 8, bounds.Y + 8, bounds.Width - 16, fontSize, textColor);

                // Draw tip at the bottom-center
                int tipFontSize = 10;
                string tip = "点击返回正面";
                int tipWidth = FontManager.MeasureTextWidth(tip, tipFontSize);
                float tipX = bounds.X + (bounds.Width - tipWidth) / 2f;
                float tipY = bounds.Y + bounds.Height - 16;
                FontManager.DrawText(tip, tipX, tipY, tipFontSize, new Color(150, 120, 130, 255));

                interaction.CardClicked = !disabled && isHovered && Raylib.IsMouseButtonPressed(MouseButton.Left);
                return interaction;
            }

            // Draw Card Background
            Raylib.DrawRectangleRounded(bounds, 0.1f, 8, bgColor);
            Raylib.DrawRectangleRoundedLinesEx(bounds, 0.1f, 8, 2f, outlineColor);

            // Draw Clocks Badges
            if (clocks != null && clocks.Count > 0)
            {
                float rightX = bounds.X + bounds.Width - 6;
                float topY = bounds.Y;
                foreach (var clock in clocks)
                {
                    DrawClockBadge(ref rightX, topY, clock);
                }
            }

            // 如果是容器（container），抹除显示以提升卡牌视觉高级感
            if (string.Equals(typeLabel, "container", StringComparison.OrdinalIgnoreCase) || string.Equals(typeLabel, "容器", StringComparison.OrdinalIgnoreCase))
            {
                typeLabel = string.Empty;
            }

            bool hasRequires = requires != null && requires.Count > 0 && slotted != null && slotted.Count == requires.Count;
            bool showButton = hasRequires || typeLabel == "行动";

            // Draw Title text (centered, adjusted upwards if card has slots/button)
            bool hasSubtitle = !string.IsNullOrWhiteSpace(subtitle);
            int titleFontSize = 20;
            int titleWidth = FontManager.MeasureTextWidth(name, titleFontSize);
            float titleX = bounds.X + (bounds.Width - titleWidth) / 2f;
            float titleY = showButton 
                ? bounds.Y + 8
                : bounds.Y + (bounds.Height / 2f) - (hasSubtitle ? 28 : 15);
            FontManager.DrawText(name, titleX, titleY, titleFontSize, titleColor);

            int subtitleFontSize = 15;
            List<string> subtitleLines = new();
            float subtitleY = titleY + 26;
            if (hasSubtitle)
            {
                float subtitleWidth = bounds.Width - 24f;
                subtitleLines = WrapTextLines(subtitle, subtitleWidth, subtitleFontSize);
                while (subtitleLines.Count > 2 && subtitleFontSize > 11)
                {
                    subtitleFontSize--;
                    subtitleLines = WrapTextLines(subtitle, subtitleWidth, subtitleFontSize);
                }
                subtitleLines = ClampWrappedLines(subtitleLines, 2, subtitleWidth, subtitleFontSize);

                for (int i = 0; i < subtitleLines.Count; i++)
                {
                    string line = subtitleLines[i];
                    int lineWidth = FontManager.MeasureTextWidth(line, subtitleFontSize);
                    float lineX = bounds.X + (bounds.Width - lineWidth) / 2f;
                    FontManager.DrawText(line, lineX, subtitleY + i * (subtitleFontSize + 4), subtitleFontSize, new Color(170, 175, 205, 255));
                }
            }

            float subtitleBottom = hasSubtitle
                ? subtitleY + subtitleLines.Count * (subtitleFontSize + 4)
                : titleY + 26;

            // Draw skill badge (roll cards) or plain type text — the skill tells the player
            // which ability this action tests, replacing the meaningless "判定" label.
            bool hasSkill = !string.IsNullOrEmpty(rollSkill);
            if (hasSkill && !disabled)
            {
                string skillText = SkillInfo.DisplayName(rollSkill!);
                int skillFont = 14;
                int skillTextW = FontManager.MeasureTextWidth(skillText, skillFont);
                float badgeW = skillTextW + 22;
                float badgeH = 20;
                float badgeX = bounds.X + (bounds.Width - badgeW) / 2f;
                float badgeY = showButton
                    ? (hasSubtitle ? Math.Max(bounds.Y + 66, subtitleBottom + 2) : bounds.Y + 48)
                    : bounds.Y + bounds.Height - 26;
                var badgeRect = new Rectangle(badgeX, badgeY, badgeW, badgeH);
                Raylib.DrawRectangleRounded(badgeRect, 0.5f, 8, new Color(16, 29, 51, 255));
                Raylib.DrawRectangleRoundedLinesEx(badgeRect, 0.5f, 8, 1f, new Color(63, 109, 176, 255));
                FontManager.DrawText(skillText, badgeX + 11, badgeY + 3, skillFont, new Color(188, 216, 255, 255));
            }
            else if (!string.IsNullOrEmpty(typeLabel))
            {
                int typeFontSize = 14;
                int typeWidth = FontManager.MeasureTextWidth(typeLabel, typeFontSize);
                float typeX = bounds.X + (bounds.Width - typeWidth) / 2f;
                float typeY = showButton
                    ? (hasSubtitle ? Math.Max(bounds.Y + 44, subtitleBottom + 2) : bounds.Y + 32)
                    : bounds.Y + bounds.Height - 22;
                FontManager.DrawText(typeLabel, typeX, typeY, typeFontSize, typeColor);
            }

            // For roll cards, tags sit on their own row below the title (left side); the ability
            // chips mirror them on the right. Keeps a long centered title from colliding.
            float tagBottomY = DrawNodeTags(bounds, tags, hasRequires ? bounds.Y + 30 : (showButton ? bounds.Y + (hasSubtitle ? 62 : 52) : bounds.Y + 6));

            // Bottom of the execute button; used to gate the odds preview so it only shows
            // when the card is tall enough to leave room below the button.
            float executeBottomY = bounds.Y + bounds.Height;

            // Draw Slots & Execute Button if there are requirements
            if (hasRequires)
            {
                int M = requires!.Count;
                float slotH = 32;
                float spacing = 8;
                float totalWidth = 0f;
                for (int j = 0; j < M; j++)
                {
                    totalWidth += SlotWidth(requires[j]);
                    if (j < M - 1)
                        totalWidth += spacing;
                }
                float slotStartX = bounds.X + (bounds.Width - totalWidth) / 2f;
                float slotY = bounds.Y + (hasSubtitle ? 88 : 74);
                if (hasSubtitle)
                {
                    slotY = Math.Max(slotY, subtitleBottom + 22);
                }
                float slotX = slotStartX;

                for (int j = 0; j < M; j++)
                {
                    float slotW = SlotWidth(requires[j]);
                    var slotRect = new Rectangle(slotX, slotY, slotW, slotH);
                    slotX += slotW + spacing;

                    bool slotHover = !disabled && ui.CanHover(slotRect);
                    var res = slotted![j];
                    bool canMatchHeldResource = !disabled && heldResource != null && ResourceSlotRules.CanMatchRequirement(requires[j], heldResource);
                    bool canDropHeldHere = !disabled && heldResource != null
                        && canDropHeldResource != null
                        && j < canDropHeldResource.Count
                        && canDropHeldResource[j];
                    var visualState = GetSlotVisualState(res, heldResource, canMatchHeldResource, canDropHeldHere, slotHover);

                    if (res == null)
                    {
                        DrawEmptySlotFrame(slotRect, visualState);

                        string placeholder = FormatRequirementLabel(requires[j]);
                        Color placeholderColor = SlotTextColor(visualState);
                        DrawCenteredFittingText(placeholder, slotRect, requires[j].Type == "item" ? 12 : 14, placeholderColor);
                    }
                    else
                    {
                        DrawFilledSlotFrame(slotRect, visualState);

                        string valStr = FormatSlottedLabel(res);
                        DrawCenteredFittingText(valStr, slotRect, res.Type == "item" ? 12 : 14, Color.White);
                    }

                    if (!disabled && ui.WasClicked(slotRect))
                    {
                        interaction.ClickedSlotIndex = j;
                    }
                    else if (canDropHeldHere && ui.CanHover(slotRect) && Raylib.IsMouseButtonReleased(MouseButton.Left))
                    {
                        interaction.DroppedSlotIndex = j;
                    }
                }

                // Draw Execute Button
                float exeW = 84;
                float exeH = 20;
                float exeX = bounds.X + (bounds.Width - exeW) / 2f;
                float exeY = slotY + 36;
                var exeRect = new Rectangle(exeX, exeY, exeW, exeH);
                executeBottomY = exeY + exeH;

                bool allFilled = slotted != null && slotted.All(s => s != null);
                if (isExecuting)
                {
                    DrawExecuteProgress(exeRect, executeProgress, executingText);
                }
                else
                {
                    bool canExecute = allFilled && !disabled;
                    var exeBtn = SSNoir.TerminalApp.Rendering.UiButton.Draw(exeRect, disabled ? "不可用" : (allFilled ? "执行" : "待命"), ui, canExecute, 12,
                        new Color((byte)50, (byte)150, (byte)50, (byte)255), new Color((byte)100, (byte)200, (byte)100, (byte)255), new Color((byte)50, (byte)50, (byte)55, (byte)255),
                        new Color((byte)50, (byte)150, (byte)50, (byte)255), Color.White, new Color((byte)70, (byte)70, (byte)75, (byte)255),
                        Color.White, new Color((byte)100, (byte)100, (byte)110, (byte)255));
                    
                    if (exeBtn.Clicked)
                    {
                        interaction.ExecuteClicked = true;
                    }
                }
            }
            else if (showButton)
            {
                // For instant-action cards (no requirements, but show button)
                float exeW = 80;
                float exeH = 18;
                float exeX = bounds.X + (bounds.Width - exeW) / 2f;
                float exeY = bounds.Y + (hasSubtitle ? 84 : 70);
                if (hasSubtitle)
                {
                    exeY = Math.Max(exeY, subtitleBottom + 24);
                }
                var exeRect = new Rectangle(exeX, exeY, exeW, exeH);

                if (isExecuting)
                {
                    DrawExecuteProgress(exeRect, executeProgress, executingText);
                }
                else
                {
                    var exeBtn = SSNoir.TerminalApp.Rendering.UiButton.Draw(exeRect, disabled ? "不可用" : "执行", ui, !disabled, 12,
                        new Color((byte)50, (byte)150, (byte)50, (byte)255), new Color((byte)100, (byte)200, (byte)100, (byte)255), null,
                        new Color((byte)50, (byte)150, (byte)50, (byte)255), Color.White, null,
                        Color.White, null);
                    
                    if (exeBtn.Clicked)
                    {
                        interaction.ExecuteClicked = true;
                    }
                }
            }
            else
            {
                // Simple container or observer card click behavior
                interaction.CardClicked = !disabled && ui.WasClicked(bounds);
            }

            // Draw Difficulty Modifier Tags on the top-left of the card
            if (effectiveModifiers.Count > 0)
            {
                float tagStartX = bounds.X + 6;
                float tagStartY = Math.Max(bounds.Y + 6, tagBottomY + 4);
                for (int k = 0; k < effectiveModifiers.Count; k++)
                {
                    var mod = effectiveModifiers[k];
                    string modText = $"{mod.Reason} {(mod.Value > 0 ? "+" : "")}{mod.Value}";
                    int fontSize = 10;
                    int textWidth = FontManager.MeasureTextWidth(modText, fontSize);
                    float tagW = textWidth + 10;
                    float tagH = 16;
                    float tagX = tagStartX;
                    float tagY = tagStartY + k * 20;
                    var tagRect = new Rectangle(tagX, tagY, tagW, tagH);

                    Color tagBg = mod.Value < 0 ? new Color(120, 30, 30, 255)
                                 : (mod.Value > 0 ? new Color(30, 100, 30, 255) : new Color(60, 60, 60, 255));
                    Color tagBorder = mod.Value < 0 ? new Color(180, 60, 60, 255)
                                    : (mod.Value > 0 ? new Color(60, 160, 60, 255) : new Color(100, 100, 100, 255));

                    Raylib.DrawRectangleRounded(tagRect, 0.4f, 4, tagBg);
                    Raylib.DrawRectangleRoundedLinesEx(tagRect, 0.4f, 4, 1f, tagBorder);
                    FontManager.DrawText(modText, tagX + 5, tagY + 3, fontSize, Color.White);
                }
            }

            // Right rail: each active party member's level in this card's skill, framed in
            // that actor's theme color — an at-a-glance "who is good at this" comparison.
            if (hasSkill && actors != null && actors.Count > 0)
            {
                DrawActorAbilityRail(bounds, rollSkill!, actors);
            }

            if (localRoll != null)
            {
                DrawLocalRoll(bounds, localRoll, localRollPhase, localRollDisplayDieValue, localRollDisplayScale);
            }
            else if (residue != null)
            {
                DrawResidue(bounds, residue);
            }
            else if (hasSkill && !disabled && hasRequires && actors != null
                     && bounds.Y + bounds.Height - executeBottomY >= 32f)
            {
                // Once a die is placed, preview all six fate faces. Gated to cards with room below the button.
                TryDrawFatePreview(bounds, rollSkill!, slotted!, effectiveModifiers, actors, executeBottomY);
            }

            return interaction;
        }

        private static void DrawActorAbilityRail(Rectangle bounds, string skill, IReadOnlyList<ActorSnapshot> actors)
        {
            const float chipW = 52f;
            const float chipH = 18f;
            float x = bounds.X + bounds.Width - chipW - 6f;
            // Same row as the tags (below the title), mirrored to the right edge.
            float y = bounds.Y + 30f;
            int drawn = 0;
            foreach (var actor in actors)
            {
                if (actor.Status == "away")
                {
                    continue;
                }
                if (!actor.Stats.TryGetValue(skill, out int level))
                {
                    continue;
                }
                var (r, g, b) = ActorTheme.ColorFor(actors, actor.Id);
                var color = new Color(r, g, b, (byte)255);
                var chip = new Rectangle(x, y + drawn * (chipH + 4f), chipW, chipH);
                Raylib.DrawRectangleRounded(chip, 0.4f, 5, new Color((byte)(r / 5), (byte)(g / 5), (byte)(b / 5), (byte)235));
                Raylib.DrawRectangleRoundedLinesEx(chip, 0.4f, 5, 1.4f, color);

                string shortName = actor.Name.Length > 2 ? actor.Name.Substring(0, 2) : actor.Name;
                FontManager.DrawText(shortName, chip.X + 5f, chip.Y + 3f, 10, color);
                string lvl = level.ToString();
                int lvlW = FontManager.MeasureTextWidth(lvl, 13);
                FontManager.DrawText(lvl, chip.X + chip.Width - lvlW - 6f, chip.Y + 2f, 13, new Color(235, 240, 255, 255));
                drawn++;
            }
        }

        private static readonly Color OddsFailColor    = new Color(209, 58, 74, 255);
        private static readonly Color OddsNeutralColor  = new Color(214, 169, 78, 255);
        private static readonly Color OddsSuccessColor  = new Color(89, 180, 119, 255);

        private static List<DifficultyModifierInfo> BuildEffectiveModifiers(
            List<DifficultyModifierInfo>? baseModifiers,
            List<SlottedResource?>? slotted,
            IReadOnlyList<ActorSnapshot>? actors,
            bool ignoresStressPenalty)
        {
            var result = baseModifiers != null
                ? new List<DifficultyModifierInfo>(baseModifiers)
                : new List<DifficultyModifierInfo>();
            if (ignoresStressPenalty || slotted == null || actors == null) return result;

            SlottedResource? die = slotted.FirstOrDefault(s => s?.Type == "die");
            if (die == null) return result;
            ActorSnapshot? actor = actors.FirstOrDefault(a => a.Id == die.ActorId);
            if (actor == null)
                throw new InvalidOperationException($"Actor '{die.ActorId}' was not found for stress modifier preview.");

            int value = TeamState.GetStressRollModifier(actor.Stress);
            if (value != 0)
                result.Add(new DifficultyModifierInfo { Value = value, Reason = "心绪不宁" });
            return result;
        }

        private static void TryDrawFatePreview(Rectangle bounds, string skill, List<SlottedResource?> slotted,
            List<DifficultyModifierInfo>? modifiers, IReadOnlyList<ActorSnapshot> actors, float executeBottomY)
        {
            SlottedResource? dieSlot = null;
            foreach (var s in slotted)
            {
                if (s != null && s.Type == "die")
                {
                    dieSlot = s;
                    break;
                }
            }
            if (dieSlot == null)
            {
                return;
            }

            int? skillLevel = null;
            foreach (var actor in actors)
            {
                if (actor.Id == dieSlot.ActorId)
                {
                    if (!actor.Stats.TryGetValue(skill, out int lv))
                        throw new InvalidOperationException($"Actor '{actor.Id}' is missing required stat '{skill}'.");
                    skillLevel = lv;
                    break;
                }
            }
            if (!skillLevel.HasValue)
                throw new InvalidOperationException($"Actor '{dieSlot.ActorId}' was not found for fate preview.");
            int modSum = 0;
            if (modifiers != null)
            {
                foreach (var m in modifiers)
                {
                    modSum += m.Value;
                }
            }

            var strip = FateStrip.Compute(dieSlot.Value, skillLevel.Value, modSum);
            DrawFateStrip(bounds, strip, executeBottomY);
        }

        private static void DrawFateStrip(Rectangle bounds, RollOutcome[] strip, float executeBottomY)
        {
            float pad = 8f;
            float summaryY = executeBottomY + 1f;
            string summary = FateStrip.Describe(strip);
            int summaryW = FontManager.MeasureTextWidth(summary, 9);
            FontManager.DrawText(summary, bounds.X + (bounds.Width - summaryW) / 2f, summaryY, 9, new Color(190, 190, 205, 255));

            float rowY = summaryY + 12f;
            float gap = 3f;
            float cellW = Math.Min(24f, (bounds.Width - pad * 2f - gap * 5f) / 6f);
            float totalW = cellW * 6f + gap * 5f;
            float startX = bounds.X + (bounds.Width - totalW) / 2f;
            for (int i = 0; i < 6; i++)
            {
                Color color = OutcomeColor(strip[i]);
                var cell = new Rectangle(startX + i * (cellW + gap), rowY, cellW, 17f);
                Raylib.DrawRectangleRounded(cell, 0.12f, 3,
                    new Color((byte)(color.R / 5), (byte)(color.G / 5), (byte)(color.B / 5), (byte)245));
                Raylib.DrawRectangleRoundedLinesEx(cell, 0.12f, 3, 1f, color);
                string face = (i + 1).ToString();
                int faceW = FontManager.MeasureTextWidth(face, 10);
                FontManager.DrawText(face, cell.X + (cell.Width - faceW) / 2f, cell.Y + 3f, 10, color);
                if (i == 0 || i == 5)
                {
                    string mark = i == 0 ? "-1" : "+1";
                    FontManager.DrawText(mark, cell.X + 1f, cell.Y - 7f, 7, color);
                }
            }
        }

        private static void DrawLocalRoll(Rectangle bounds, ActionReport report, int phase, int displayDieValue, float displayScale)
        {
            var panel = new Rectangle(bounds.X + 8f, bounds.Y + bounds.Height - 58f, bounds.Width - 16f, 48f);
            Raylib.DrawRectangleRounded(panel, 0.16f, 6, new Color(20, 22, 30, 245));
            Raylib.DrawRectangleRoundedLinesEx(panel, 0.16f, 6, 1.4f, new Color(255, 182, 147, 255));

            int dieValue = phase == 0 ? displayDieValue : report.FateDieValue;
            int dieFont = (int)(26 * Math.Clamp(displayScale, 0.8f, 1.35f));
            string dieText = $"D{dieValue}";
            int dieW = FontManager.MeasureTextWidth(dieText, dieFont);
            FontManager.DrawText(dieText, panel.X + 13f + (42f - dieW) / 2f, panel.Y + (panel.Height - dieFont) / 2f, dieFont, new Color(255, 182, 147, 255));

            string label = phase >= 2 ? FormatOutcome(report.Outcome) : "判定中";
            Color color = phase >= 2 ? OutcomeColor(report.Outcome) : new Color(220, 220, 235, 255);
            FontManager.DrawText(label, panel.X + 66f, panel.Y + 9f, 14, color);

            string detail = phase >= 2
                ? FormatRollDetail(report)
                : "命运骰滚动...";
            FontManager.DrawText(detail, panel.X + 66f, panel.Y + 28f, 11, new Color(170, 170, 190, 255));
        }

        private static string FormatRollDetail(ActionReport report)
        {
            string natural = report.NaturalModifier == 0
                ? string.Empty
                : report.NaturalModifier > 0 ? " + 天然6" : " - 天然1";
            return $"准备 {report.PreparedValue} + 命运 {report.FateDieValue}{natural} = {report.FinalTotal}";
        }

        private static void DrawResidue(Rectangle bounds, CardPresentationResidue residue)
        {
            // 残影作为盖在卡片上的历史投影，使用半透明的背景以透露下方原卡，创造“幻影”质感
            Raylib.DrawRectangleRounded(bounds, 0.1f, 8, new Color(12, 14, 20, 210));
            // 边框带有明显的半透明呼吸感
            Raylib.DrawRectangleRoundedLinesEx(bounds, 0.1f, 8, 1.2f, new Color(105, 125, 180, 150));

            // 绘制淡雅电子扫描线效果，强化数字残影/全息投影感
            for (float sy = bounds.Y + 4f; sy < bounds.Y + bounds.Height - 4f; sy += 4f)
            {
                Raylib.DrawLineEx(
                    new System.Numerics.Vector2(bounds.X + 6f, sy),
                    new System.Numerics.Vector2(bounds.X + bounds.Width - 6f, sy),
                    1.0f,
                    new Color(255, 255, 255, 8)
                );
            }

            // 保留掷骰动画结束时的结果块，叙事与效果条在其下依次堆叠。
            var panel = new Rectangle(bounds.X + 7f, bounds.Y + 6f, bounds.Width - 14f, 42f);
            Raylib.DrawRectangleRounded(panel, 0.16f, 6, new Color(18, 20, 28, 230));
            Raylib.DrawRectangleRoundedLinesEx(panel, 0.16f, 6, 1.0f, new Color(255, 182, 147, 180));

            float textX = panel.X + 66f;
            if (residue.FateDieValue.HasValue)
            {
                string dieText = $"D{residue.FateDieValue.Value}";
                const int dieFont = 24;
                int dieW = FontManager.MeasureTextWidth(dieText, dieFont);
                FontManager.DrawText(dieText, panel.X + 13f + (42f - dieW) / 2f, panel.Y + (panel.Height - dieFont) / 2f, dieFont, new Color(255, 182, 147, 255));
            }

            string title = residue.RollOutcome.HasValue ? FormatOutcome(residue.RollOutcome.Value) : "行动结果";
            if (!string.IsNullOrWhiteSpace(residue.Title))
                title += " · " + residue.Title;
            Color titleColor = residue.RollOutcome.HasValue ? OutcomeColor(residue.RollOutcome.Value) : new Color(230, 230, 245, 255);
            FontManager.DrawText(title, textX, panel.Y + 6f, 12, titleColor);
            if (residue.FinalTotal.HasValue)
                FontManager.DrawText($"准备 {residue.PreparedValue} · 最终 {residue.FinalTotal.Value}", textX, panel.Y + 23f, 10, new Color(170, 170, 190, 255));

            float y = panel.Y + panel.Height + 3f;
            if (!string.IsNullOrWhiteSpace(residue.Subtitle))
            {
                var narrative = new Rectangle(bounds.X + 7f, y, bounds.Width - 14f, 22f);
                Raylib.DrawRectangleRounded(narrative, 0.18f, 5, new Color(28, 31, 43, 250));
                Raylib.DrawRectangleRoundedLinesEx(narrative, 0.18f, 5, 1f, new Color(75, 86, 118, 255));
                FontManager.DrawText(residue.Subtitle, narrative.X + 7f, narrative.Y + 5f, 9, new Color(205, 208, 222, 255));
                y += 25f;
            }

            DrawEffectRows(new Rectangle(bounds.X + 7f, y, bounds.Width - 14f, bounds.Y + bounds.Height - y - 5f), residue.Effects);
        }

        private static void DrawEffectRows(Rectangle area, IReadOnlyList<ActionEffectRecord> effects)
        {
            const float rowHeight = 12f;
            const float rowGap = 14f; // 间距调微密（16f -> 14f），使得能在有限区域内画更多行
            int maxRows = (int)(area.Height / rowGap);

            if (effects.Count <= maxRows)
            {
                // 全都能放得下
                for (int i = 0; i < effects.Count; i++)
                {
                    DrawSingleEffectRow(effects[i], area.X, area.Y + i * rowGap, area.Width, rowHeight);
                }
            }
            else
            {
                // 超出，留最后一行写 "+ 还有 N 项影响..."
                int visibleCount = Math.Max(0, maxRows - 1);
                for (int i = 0; i < visibleCount; i++)
                {
                    DrawSingleEffectRow(effects[i], area.X, area.Y + i * rowGap, area.Width, rowHeight);
                }

                if (maxRows > 0)
                {
                    float y = area.Y + visibleCount * rowGap;
                    var row = new Rectangle(area.X, y, area.Width, rowHeight);
                    Color accent = new Color(130, 145, 175, 255); // 暗灰蓝

                    Raylib.DrawRectangleRounded(row, 0.25f, 4, new Color(accent.R, accent.G, accent.B, (byte)25));
                    Raylib.DrawRectangle((int)row.X, (int)row.Y, 3, (int)row.Height, accent);

                    string moreText = $"+ 还有 {effects.Count - visibleCount} 项影响...";
                    FontManager.DrawText(moreText, row.X + 8f, row.Y + 2f, 10, new Color(175, 180, 200, 255));
                }
            }
        }

        private static void DrawSingleEffectRow(ActionEffectRecord effect, float x, float y, float width, float rowHeight)
        {
            Color accent = effect.Tone switch
            {
                ActionEffectTone.Positive => new Color(90, 190, 125, 255),
                ActionEffectTone.Negative => new Color(220, 105, 95, 255),
                _ => new Color(130, 150, 195, 255)
            };
            var row = new Rectangle(x, y, width, rowHeight);
            Raylib.DrawRectangleRounded(row, 0.25f, 4, new Color(accent.R, accent.G, accent.B, (byte)35));
            Raylib.DrawRectangle((int)row.X, (int)row.Y, 3, (int)row.Height, accent);

            if (effect.Kind == ActionEffectKind.Note)
            {
                FontManager.DrawText(effect.Text, row.X + 8f, row.Y + 2f, 10, new Color(210, 215, 232, 255));
                return;
            }

            FontManager.DrawText(effect.Label, row.X + 8f, row.Y + 2f, 10, new Color(210, 215, 232, 255));
            string value = effect.Delta.HasValue
                ? (effect.Delta.Value > 0 ? $"+{effect.Delta.Value}" : effect.Delta.Value.ToString())
                : string.Empty;
            int valueW = FontManager.MeasureTextWidth(value, 10);
            FontManager.DrawText(value, row.X + row.Width - valueW - 7f, row.Y + 2f, 10, accent);
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
                RollOutcome.Success => new Color(90, 190, 125, 255), // 薄荷绿
                RollOutcome.Neutral => new Color(255, 182, 147, 255), // 暖杏黄
                RollOutcome.Fail => new Color(220, 105, 95, 255),    // 珊瑚红
                _ => Color.White
            };
        }

        public static void DrawClockCard(
            Rectangle bounds,
            string name,
            string subtitle,
            GameClock? clock)
        {
            Color bgColor      = new Color(18, 22, 32, 255);
            Color outlineColor = new Color(65, 85, 130, 255);
            Color nameColor    = new Color(140, 170, 220, 255);
            Color subtitleColor = new Color(85, 105, 150, 255);
            Color clockColor   = new Color(120, 150, 230, 255);
            Color dimColor     = new Color(55, 65, 90, 255);

            Raylib.DrawRectangleRounded(bounds, 0.1f, 8, bgColor);
            Raylib.DrawRectangleRoundedLinesEx(bounds, 0.1f, 8, 1.5f, outlineColor);

            // Name
            int nameFontSize = 18;
            int nameWidth = FontManager.MeasureTextWidth(name, nameFontSize);
            FontManager.DrawText(name, bounds.X + (bounds.Width - nameWidth) / 2f, bounds.Y + 10, nameFontSize, nameColor);

            // Clock display — centered vertically between name and subtitle
            if (clock != null)
            {
                float clockY = bounds.Y + 36f;
                float clockCenterX = bounds.X + bounds.Width / 2f;

                if (clock.Style == ClockStyle.Countdown)
                {
                    string frac = $"{clock.Current}/{clock.Max}";
                    int fs = 28;
                    int tw = FontManager.MeasureTextWidth(frac, fs);
                    FontManager.DrawText(frac, clockCenterX - tw / 2f, clockY, fs, clockColor);

                    string label = clock.Label;
                    int lw = FontManager.MeasureTextWidth(label, 11);
                    FontManager.DrawText(label, clockCenterX - lw / 2f, clockY + 34, 11, dimColor);
                }
                else if (clock.Style == ClockStyle.Segments)
                {
                    int dotSize = 10;
                    int spacing = 4;
                    float totalW = clock.Max * (dotSize + spacing) - spacing;
                    float dotStartX = clockCenterX - totalW / 2f;

                    for (int i = 0; i < clock.Max; i++)
                    {
                        var dotRect = new Rectangle(dotStartX + i * (dotSize + spacing), clockY + 4, dotSize, dotSize);
                        if (i < clock.Current)
                            Raylib.DrawRectangleRounded(dotRect, 0.4f, 4, clockColor);
                        else
                        {
                            Raylib.DrawRectangleRounded(dotRect, 0.4f, 4, new Color(25, 30, 45, 255));
                            Raylib.DrawRectangleRoundedLinesEx(dotRect, 0.4f, 4, 1f, dimColor);
                        }
                    }

                    string label = clock.Label;
                    int lw = FontManager.MeasureTextWidth(label, 11);
                    FontManager.DrawText(label, clockCenterX - lw / 2f, clockY + 20, 11, dimColor);
                }
                else // Pie
                {
                    float radius = 18f;
                    var center = new System.Numerics.Vector2(clockCenterX, clockY + radius + 2);
                    Raylib.DrawCircleLines((int)center.X, (int)center.Y, radius, dimColor);
                    if (clock.Max > 0 && clock.Current > 0)
                    {
                        float pct = (float)clock.Current / clock.Max;
                        Raylib.DrawCircleSector(center, radius, -90f, -90f + 360f * pct, 36, clockColor);
                    }
                    string frac = $"{clock.Current}/{clock.Max}";
                    int fw = FontManager.MeasureTextWidth(frac, 11);
                    FontManager.DrawText(frac, clockCenterX - fw / 2f, clockY + radius * 2 + 6, 11, dimColor);

                    string label = clock.Label;
                    int lw = FontManager.MeasureTextWidth(label, 11);
                    FontManager.DrawText(label, clockCenterX - lw / 2f, clockY + radius * 2 + 20, 11, dimColor);
                }
            }

            // Subtitle at bottom
            if (!string.IsNullOrEmpty(subtitle))
            {
                DrawWrappedText(subtitle, bounds.X + 8, bounds.Y + bounds.Height - 24, bounds.Width - 16, 10, subtitleColor);
            }
        }

        public static void DrawResidueCard(Rectangle bounds, CardPresentationResidue residue)
        {
            Color bgColor = new Color(12, 14, 20, 210); // 半透明幽灵黑
            Color outlineColor = new Color(105, 125, 180, 150); // 半透明灰蓝
            Color typeColor = new Color(120, 135, 175, 220);

            Raylib.DrawRectangleRounded(bounds, 0.1f, 8, bgColor);
            Raylib.DrawRectangleRoundedLinesEx(bounds, 0.1f, 8, 1.2f, outlineColor);

            // 绘制电子扫描线效果
            for (float sy = bounds.Y + 4f; sy < bounds.Y + bounds.Height - 4f; sy += 4f)
            {
                Raylib.DrawLineEx(
                    new System.Numerics.Vector2(bounds.X + 6f, sy),
                    new System.Numerics.Vector2(bounds.X + bounds.Width - 6f, sy),
                    1.0f,
                    new Color(255, 255, 255, 8)
                );
            }

            string label = residue.RollOutcome.HasValue ? FormatOutcome(residue.RollOutcome.Value) : "行动结果";
            int labelW = FontManager.MeasureTextWidth(label, 13);
            FontManager.DrawText(label, bounds.X + (bounds.Width - labelW) / 2f, bounds.Y + 12f, 13, typeColor);

            float titleY = bounds.Y + 38f;
            if (residue.FateDieValue.HasValue)
            {
                string dieText = $"D{residue.FateDieValue.Value}";
                const int dieFont = 26;
                int dieW = FontManager.MeasureTextWidth(dieText, dieFont);
                FontManager.DrawText(dieText, bounds.X + (bounds.Width - dieW) / 2f, bounds.Y + 34f, dieFont, new Color(255, 182, 147, 255));
                titleY = bounds.Y + 66f;
            }

            string title = residue.Title;
            if (residue.RollOutcome.HasValue)
            {
                title = $"{FormatOutcome(residue.RollOutcome.Value)}：{residue.Title}";
            }
            Color titleColor = residue.RollOutcome.HasValue ? OutcomeColor(residue.RollOutcome.Value) : new Color(235, 235, 248, 255);
            DrawWrappedText(title, bounds.X + 14f, titleY, bounds.Width - 28f, 14, titleColor);

            string subtitle = residue.Subtitle;
            if (string.IsNullOrWhiteSpace(subtitle) && residue.FinalTotal.HasValue)
                subtitle = $"准备 {residue.PreparedValue} · 最终 {residue.FinalTotal.Value}";
            if (!string.IsNullOrWhiteSpace(subtitle))
                DrawWrappedText(subtitle, bounds.X + 14f, titleY + 30f, bounds.Width - 28f, 11, new Color(185, 190, 210, 255));

            float effectsY = string.IsNullOrWhiteSpace(subtitle) ? titleY + 30f : titleY + 48f;
            DrawEffectRows(new Rectangle(bounds.X + 10f, effectsY, bounds.Width - 20f, bounds.Y + bounds.Height - effectsY - 8f), residue.Effects);
        }

        // 标签配色：工作/风险标签全局一致，玩家一眼判断类型与风险。
        private static (Color bg, Color border, Color text) TagColors(string label)
        {
            switch (label)
            {
                case "交锋":
                    return (new Color(95, 34, 34, 230), new Color(210, 86, 76, 255), new Color(255, 215, 205, 255));
                case "工作": // 能赚钱：青绿
                    return (new Color(28, 58, 64, 220), new Color(90, 180, 190, 255), new Color(210, 240, 245, 255));
                case "低风险": // 绿
                    return (new Color(34, 66, 44, 220), new Color(96, 190, 120, 255), new Color(215, 245, 220, 255));
                case "中风险": // 琥珀
                    return (new Color(80, 62, 26, 225), new Color(214, 168, 70, 255), new Color(255, 238, 200, 255));
                case "高风险": // 红
                    return (new Color(90, 40, 34, 225), new Color(214, 96, 74, 255), new Color(255, 220, 205, 255));
                case "非法": // 深红：非法工作/掉关系
                    return (new Color(70, 26, 44, 230), new Color(200, 70, 110, 255), new Color(255, 210, 225, 255));
                default:
                    return (new Color(35, 48, 78, 220), new Color(105, 145, 220, 255), new Color(220, 235, 255, 255));
            }
        }

        private static float DrawNodeTags(Rectangle bounds, List<string>? tags, float startY)
        {
            if (tags == null || tags.Count == 0)
            {
                return bounds.Y + 2;
            }

            float x = bounds.X + 6;
            float y = startY;
            float maxRight = bounds.X + bounds.Width - 8;
            float lineH = 18;

            for (int i = 0; i < tags.Count; i++)
            {
                string label = tags[i];
                if (string.IsNullOrWhiteSpace(label))
                {
                    continue;
                }

                int fontSize = 10;
                int textW = FontManager.MeasureTextWidth(label, fontSize);
                float tagW = Math.Min(textW + 12, bounds.Width - 16);
                if (x + tagW > maxRight)
                {
                    x = bounds.X + 6;
                    y += lineH + 3;
                }

                var rect = new Rectangle(x, y, tagW, lineH);
                var (bg, border, text) = TagColors(label);

                Raylib.DrawRectangleRounded(rect, 0.35f, 4, bg);
                Raylib.DrawRectangleRoundedLinesEx(rect, 0.35f, 4, 1f, border);

                int drawW = FontManager.MeasureTextWidth(label, fontSize);
                while (fontSize > 8 && drawW > rect.Width - 8)
                {
                    fontSize--;
                    drawW = FontManager.MeasureTextWidth(label, fontSize);
                }
                FontManager.DrawText(label, rect.X + (rect.Width - drawW) / 2f, rect.Y + 4f, fontSize, text);

                x += tagW + 5;
            }

            return y + lineH;
        }

        private static List<string> WrapTextLines(string text, float width, int fontSize)
        {
            var lines = new List<string>();
            string currentLine = "";

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\n')
                {
                    lines.Add(currentLine);
                    currentLine = "";
                    continue;
                }

                string testLine = currentLine + c;
                int testW = FontManager.MeasureTextWidth(testLine, fontSize);
                if (testW > width)
                {
                    if (currentLine.Length > 0)
                    {
                        lines.Add(currentLine);
                        currentLine = c.ToString();
                    }
                    else
                    {
                        lines.Add(testLine);
                        currentLine = string.Empty;
                    }
                }
                else
                {
                    currentLine = testLine;
                }
            }

            if (currentLine.Length > 0)
            {
                lines.Add(currentLine);
            }

            return lines;
        }

        private static List<string> ClampWrappedLines(List<string> lines, int maxLines, float width, int fontSize)
        {
            if (lines.Count <= maxLines)
            {
                return lines;
            }

            var visible = lines.Take(maxLines).ToList();
            string last = visible[maxLines - 1];
            while (last.Length > 0 && FontManager.MeasureTextWidth(last + "…", fontSize) > width)
            {
                last = last.Substring(0, last.Length - 1);
            }
            visible[maxLines - 1] = last + "…";
            return visible;
        }

        private static void DrawWrappedText(string text, float x, float y, float width, int fontSize, Color color)
        {
            var lines = WrapTextLines(text, width, fontSize);
            for (int i = 0; i < lines.Count; i++)
            {
                FontManager.DrawText(lines[i], x, y + i * (fontSize + 4), fontSize, color);
            }
        }

        private static void DrawExecuteProgress(Rectangle rect, float progress, string text)
        {
            progress = Math.Clamp(progress, 0f, 1f);
            Raylib.DrawRectangleRounded(rect, 0.2f, 4, new Color(36, 38, 48, 255));
            var fill = new Rectangle(rect.X + 2f, rect.Y + 2f, (rect.Width - 4f) * progress, rect.Height - 4f);
            Raylib.DrawRectangleRounded(fill, 0.2f, 4, new Color(90, 145, 205, 255));
            Raylib.DrawRectangleRoundedLinesEx(rect, 0.2f, 4, 1f, new Color(115, 150, 205, 255));

            string label = string.IsNullOrEmpty(text) ? "执行中" : text;
            if (label.Length > 5)
            {
                label = "执行中";
            }
            int w = FontManager.MeasureTextWidth(label, 11);
            FontManager.DrawText(label, rect.X + (rect.Width - w) / 2f, rect.Y + 3f, 11, Color.White);
        }

        private static float SlotWidth(ActionCost requirement)
        {
            return requirement.Type == "item" ? 72f : 34f;
        }

        private static string FormatRequirementLabel(ActionCost requirement)
        {
            if (requirement.Type == "die")
                return "D";

            if (string.IsNullOrEmpty(requirement.ItemId))
                return "?";

            return requirement.Qty > 1
                ? $"{requirement.ItemId}x{requirement.Qty}"
                : requirement.ItemId;
        }

        private static string FormatSlottedLabel(SlottedResource resource)
        {
            if (resource.Type == "die")
                return resource.Value.ToString();

            string itemName = string.IsNullOrEmpty(resource.ItemId) ? "?" : resource.ItemId;
            int qty = resource.Qty > 0 ? resource.Qty : resource.Value;
            return qty > 1 ? $"{itemName}x{qty}" : itemName;
        }

        private static void DrawCenteredFittingText(string text, Rectangle rect, int preferredFontSize, Color color)
        {
            int fontSize = preferredFontSize;
            int textWidth = FontManager.MeasureTextWidth(text, fontSize);
            while (fontSize > 9 && textWidth > rect.Width - 8f)
            {
                fontSize--;
                textWidth = FontManager.MeasureTextWidth(text, fontSize);
            }

            FontManager.DrawText(
                text,
                rect.X + (rect.Width - textWidth) / 2f,
                rect.Y + (rect.Height - fontSize) / 2f,
                fontSize,
                color);
        }

        private enum SlotVisualState
        {
            Normal,
            Hovered,
            AvailableDrop,
            HoveredDrop,
            InvalidDrop,
            Filled
        }

        private static SlotVisualState GetSlotVisualState(
            SlottedResource? slotted,
            SelectedResource? heldResource,
            bool canMatchHeldResource,
            bool canDropHeldHere,
            bool slotHover)
        {
            if (heldResource == null)
            {
                return slotted == null
                    ? (slotHover ? SlotVisualState.Hovered : SlotVisualState.Normal)
                    : SlotVisualState.Filled;
            }

            if (canDropHeldHere)
            {
                return slotHover ? SlotVisualState.HoveredDrop : SlotVisualState.AvailableDrop;
            }

            if (canMatchHeldResource)
            {
                return SlotVisualState.InvalidDrop;
            }

            return slotted == null ? SlotVisualState.Normal : SlotVisualState.Filled;
        }

        private static void DrawEmptySlotFrame(Rectangle slotRect, SlotVisualState state)
        {
            if (state == SlotVisualState.AvailableDrop || state == SlotVisualState.HoveredDrop)
            {
                DrawSlotGlow(slotRect, state == SlotVisualState.HoveredDrop);
            }

            Color fill = state switch
            {
                SlotVisualState.HoveredDrop => new Color(34, 62, 58, 185),
                SlotVisualState.AvailableDrop => new Color(28, 36, 58, 135),
                SlotVisualState.InvalidDrop => new Color(48, 25, 30, 120),
                _ => new Color(0, 0, 0, 0)
            };

            if (fill.A > 0)
            {
                Raylib.DrawRectangleRounded(slotRect, 0.2f, 4, fill);
            }

            Color border = state switch
            {
                SlotVisualState.HoveredDrop => new Color(110, 235, 200, 255),
                SlotVisualState.AvailableDrop => new Color(160, 190, 255, 255),
                SlotVisualState.InvalidDrop => new Color(135, 70, 85, 255),
                SlotVisualState.Hovered => new Color(130, 130, 220, 255),
                _ => new Color(80, 80, 100, 255)
            };

            float thick = state == SlotVisualState.HoveredDrop ? 2.8f
                : (state == SlotVisualState.AvailableDrop || state == SlotVisualState.InvalidDrop ? 2f : 1.5f);
            Raylib.DrawRectangleRoundedLinesEx(slotRect, 0.2f, 4, thick, border);
        }

        private static void DrawFilledSlotFrame(Rectangle slotRect, SlotVisualState state)
        {
            if (state == SlotVisualState.AvailableDrop || state == SlotVisualState.HoveredDrop)
            {
                DrawSlotGlow(slotRect, state == SlotVisualState.HoveredDrop);
            }

            Color fill = state switch
            {
                SlotVisualState.HoveredDrop => new Color(38, 70, 65, 255),
                SlotVisualState.AvailableDrop => new Color(45, 55, 88, 255),
                SlotVisualState.InvalidDrop => new Color(58, 38, 48, 255),
                _ => new Color(50, 50, 75, 255)
            };
            Color border = state switch
            {
                SlotVisualState.HoveredDrop => new Color(120, 245, 210, 255),
                SlotVisualState.AvailableDrop => new Color(190, 210, 255, 255),
                SlotVisualState.InvalidDrop => new Color(145, 80, 95, 255),
                _ => new Color(130, 130, 250, 255)
            };
            float thick = state == SlotVisualState.HoveredDrop ? 2.8f
                : (state == SlotVisualState.AvailableDrop || state == SlotVisualState.InvalidDrop ? 2f : 1.5f);

            Raylib.DrawRectangleRounded(slotRect, 0.2f, 4, fill);
            Raylib.DrawRectangleRoundedLinesEx(slotRect, 0.2f, 4, thick, border);
        }

        private static Color SlotTextColor(SlotVisualState state)
        {
            return state switch
            {
                SlotVisualState.HoveredDrop => new Color(210, 255, 240, 255),
                SlotVisualState.AvailableDrop => new Color(185, 205, 255, 255),
                SlotVisualState.InvalidDrop => new Color(190, 115, 130, 255),
                _ => new Color(80, 80, 100, 255)
            };
        }

        private static void DrawSlotGlow(Rectangle slotRect, bool strong)
        {
            float pulse = 0.5f + 0.5f * (float)Math.Sin(Raylib.GetTime() * 7.0);
            byte alpha = (byte)((strong ? 150 : 95) + pulse * (strong ? 90 : 85));
            var glowRect = new Rectangle(slotRect.X - 4f, slotRect.Y - 4f, slotRect.Width + 8f, slotRect.Height + 8f);

            Color glowFill = strong
                ? new Color((byte)50, (byte)180, (byte)150, (byte)(36 + pulse * 36))
                : new Color((byte)90, (byte)120, (byte)255, (byte)(28 + pulse * 24));
            Color glowBorder = strong
                ? new Color((byte)105, (byte)245, (byte)205, alpha)
                : new Color((byte)135, (byte)170, (byte)255, alpha);

            Raylib.DrawRectangleRounded(glowRect, 0.22f, 4, glowFill);
            Raylib.DrawRectangleRoundedLinesEx(glowRect, 0.22f, 4, strong ? 2.8f : 2.2f, glowBorder);
        }

        private static void DrawClockBadge(ref float rightX, float topY, GameClock clock)
        {
            Color activeColor = new Color(130, 130, 250, 255);
            Color inactiveColor = new Color(50, 50, 60, 255);
            Color textColor = new Color(220, 220, 240, 255);

            if (clock.Style == ClockStyle.Countdown)
            {
                string text = $"{clock.Label} {clock.Current}/{clock.Max}";
                int fontSize = 12;
                int textWidth = FontManager.MeasureTextWidth(text, fontSize);
                
                float badgeW = textWidth + 8;
                float badgeH = 16;
                float badgeX = rightX - badgeW;
                float badgeY = topY + 6;

                var rect = new Rectangle(badgeX, badgeY, badgeW, badgeH);
                Raylib.DrawRectangleRounded(rect, 0.4f, 4, new Color(20, 20, 25, 180));
                Raylib.DrawRectangleRoundedLinesEx(rect, 0.4f, 4, 1f, new Color(80, 80, 100, 255));
                FontManager.DrawText(text, badgeX + 4, badgeY + 2, fontSize, textColor);

                rightX -= (badgeW + 4);
            }
            else if (clock.Style == ClockStyle.Segments)
            {
                string labelText = clock.Label;
                int fontSize = 12;
                int labelWidth = FontManager.MeasureTextWidth(labelText, fontSize);

                int dotSize = 6;
                int spacing = 2;
                float dotsW = clock.Max * (dotSize + spacing) - spacing;
                float badgeW = labelWidth + 6 + dotsW + 6;
                float badgeH = 16;
                float badgeX = rightX - badgeW;
                float badgeY = topY + 6;

                var rect = new Rectangle(badgeX, badgeY, badgeW, badgeH);
                Raylib.DrawRectangleRounded(rect, 0.4f, 4, new Color(20, 20, 25, 180));
                Raylib.DrawRectangleRoundedLinesEx(rect, 0.4f, 4, 1f, new Color(80, 80, 100, 255));

                FontManager.DrawText(labelText, badgeX + 4, badgeY + 2, fontSize, textColor);

                float dotStartX = badgeX + 4 + labelWidth + 4;
                for (int i = 0; i < clock.Max; i++)
                {
                    var dotRect = new Rectangle(dotStartX + i * (dotSize + spacing), badgeY + (badgeH - dotSize) / 2f, dotSize, dotSize);
                    if (i < clock.Current)
                    {
                        Raylib.DrawRectangleRounded(dotRect, 0.5f, 4, activeColor);
                    }
                    else
                    {
                        Raylib.DrawRectangleRounded(dotRect, 0.5f, 4, inactiveColor);
                        Raylib.DrawRectangleRoundedLinesEx(dotRect, 0.5f, 4, 1f, new Color(80, 80, 90, 255));
                    }
                }

                rightX -= (badgeW + 4);
            }
            else if (clock.Style == ClockStyle.Pie)
            {
                string labelText = clock.Label;
                int fontSize = 12;
                int labelWidth = FontManager.MeasureTextWidth(labelText, fontSize);

                float radius = 7f;
                float badgeW = labelWidth + 6 + radius * 2 + 6;
                float badgeH = 16;
                float badgeX = rightX - badgeW;
                float badgeY = topY + 6;

                var rect = new Rectangle(badgeX, badgeY, badgeW, badgeH);
                Raylib.DrawRectangleRounded(rect, 0.4f, 4, new Color(20, 20, 25, 180));
                Raylib.DrawRectangleRoundedLinesEx(rect, 0.4f, 4, 1f, new Color(80, 80, 100, 255));

                // Draw label first
                FontManager.DrawText(labelText, badgeX + 4, badgeY + 2, fontSize, textColor);

                // Draw pie sector next
                var center = new System.Numerics.Vector2(badgeX + 4 + labelWidth + 4 + radius, badgeY + badgeH / 2f);
                // Empty ring
                Raylib.DrawCircleLines((int)center.X, (int)center.Y, radius, new Color(70, 70, 90, 255));
                // Filled sector
                if (clock.Max > 0 && clock.Current > 0)
                {
                    float pct = (float)clock.Current / clock.Max;
                    Raylib.DrawCircleSector(center, radius, -90f, -90f + 360f * pct, 36, activeColor);
                }

                rightX -= (badgeW + 4);
            }
        }
    }
}
