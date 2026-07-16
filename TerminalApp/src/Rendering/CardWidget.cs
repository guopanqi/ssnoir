using System;
using System.Collections.Generic;
using System.Linq;
using Raylib_cs;
using SSNoir.Core;

namespace SSNoir.Rendering
{
    public static class CardWidget
    {
        private const float DefaultCardHeight = 150f;
        private const float ActorAbilityChipHeight = 20f;
        private const float ActorAbilityChipGap = 5f;

        // 下面这组常量在 DrawCard 的实际绘制流程和 GetMinimumHeight/GetTagPushDown/GetClockPushDown
        // 的"预测"流程里各用一份——两边算的是同一件事（标题往下依次排开多少），数字必须对得上，
        // 否则又会出现内容画出来了、卡片高度却没跟上的那类 bug。改这些数字时两边一起改。
        private const int TitleFontSize = 18;
        private const float TitleTopY = 8f;              // 贴顶布局标题的 Y（showButton == true 时）
        private const float TitleToSubtitleGap = TitleFontSize + 8f;
        private const int SubtitleFontSize = 13;
        private const float SubtitleHorizontalMargin = 24f; // 副标题换行宽度 = 卡宽 - 这个值
        private const float ControlYBaseWithSubtitle = 78f; // 判定卡（有槽位）控制行的默认起点
        private const float ControlYBaseNoSubtitle = 64f;
        private const float SimpleButtonYBaseWithSubtitle = 84f; // 纯按钮卡（无槽位）按钮的默认起点
        private const float SimpleButtonYBaseNoSubtitle = 70f;
        private const float SlotHeight = 32f;
        private const float SlotButtonHeight = 20f;
        private const float SimpleButtonHeight = 18f;
        private const float ControlToButtonGap = 8f;
        private const float CardBottomPadding = 12f;
        private const float CenteredTitleOffsetWithSubtitle = 28f; // 居中布局：标题 Y = 卡高/2 - 这个偏移
        private const float CenteredTitleOffsetNoSubtitle = 15f;
        private const int MaxSubtitleLines = 2;
        private const int MinSubtitleFontSize = 10;

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
            IReadOnlyList<ActorSnapshot>? actors = null)
        {
            var interaction = new CardInteraction
            {
                CardClicked = false,
                ClickedSlotIndex = -1,
                DroppedSlotIndex = -1,
                ExecuteClicked = false
            };
            List<DifficultyModifierInfo> effectiveModifiers = modifiers ?? new List<DifficultyModifierInfo>();

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
            float clocksBottomY = bounds.Y;
            if (clocks != null && clocks.Count > 0)
            {
                float badgeX = bounds.X + 6f;
                float badgeY = bounds.Y + 6f;
                float rightEdge = bounds.X + bounds.Width - 6f;
                foreach (var clock in clocks)
                {
                    float badgeW = Math.Min(MeasureClockBadgeWidth(clock), bounds.Width - 12f);
                    if (badgeX > bounds.X + 6f && badgeX + badgeW > rightEdge)
                    {
                        badgeX = bounds.X + 6f;
                        badgeY += 20f;
                    }
                    DrawClockBadge(new Rectangle(badgeX, badgeY, badgeW, 16f), clock);
                    badgeX += badgeW + 4f;
                }
                clocksBottomY = badgeY + 16f;
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
            int titleWidth = FontManager.MeasureTextWidth(name, TitleFontSize);
            float titleX = bounds.X + (bounds.Width - titleWidth) / 2f;
            float titleY = showButton
                ? bounds.Y + TitleTopY
                : bounds.Y + (bounds.Height / 2f) - (hasSubtitle ? CenteredTitleOffsetWithSubtitle : CenteredTitleOffsetNoSubtitle);
            if (clocks != null && clocks.Count > 0)
            {
                titleY = Math.Max(titleY, clocksBottomY + 4f);
            }
            FontManager.DrawText(name, titleX, titleY, TitleFontSize, titleColor);

            int subtitleFontSize = SubtitleFontSize;
            List<string> subtitleLines = new();
            float subtitleY = titleY + TitleToSubtitleGap;
            if (hasSubtitle)
            {
                float subtitleWidth = bounds.Width - SubtitleHorizontalMargin;
                subtitleLines = WrapTextLines(subtitle, subtitleWidth, subtitleFontSize);
                while (subtitleLines.Count > MaxSubtitleLines && subtitleFontSize > MinSubtitleFontSize)
                {
                    subtitleFontSize--;
                    subtitleLines = WrapTextLines(subtitle, subtitleWidth, subtitleFontSize);
                }
                subtitleLines = ClampWrappedLines(subtitleLines, MaxSubtitleLines, subtitleWidth, subtitleFontSize);

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
                : titleY + TitleToSubtitleGap;
            float rollControlY = bounds.Y + (hasSubtitle ? ControlYBaseWithSubtitle : ControlYBaseNoSubtitle);
            if (hasSubtitle)
                rollControlY = Math.Max(rollControlY, subtitleBottom + 16f);

            bool hasSkill = !string.IsNullOrEmpty(rollSkill);
            // 卡片内容严格按纵向流式排列：标题/说明 → 标签 → 角色能力状态 → 槽位 → 按钮。
            // 角色能力状态是独立信息区，不能再作为右侧浮层压住资源槽。
            float tagStartY = subtitleBottom + 8f;
            float actorRailReservedWidth = hasSkill && actors != null
                ? GetActorAbilityRailReservedWidth(rollSkill!, actors)
                : 0f;
            float tagRightEdge = actorRailReservedWidth > 0f
                ? bounds.X + bounds.Width - actorRailReservedWidth
                : bounds.X + bounds.Width - 8f;
            float tagBottomY = DrawNodeTags(bounds, tags, tagStartY, disabled, tagRightEdge);
            // 能力标签与工作/风险标签共享同一行；只有左侧标签换行时才会向下延展。
            float actorRailStartY = tagStartY;
            float actorRailBottomY = actorRailStartY;
            if (hasSkill && actors != null && actors.Count > 0)
            {
                actorRailBottomY = DrawActorAbilityRail(bounds, rollSkill!, actors, actorRailStartY);
            }
            float controlY = Math.Max(rollControlY, Math.Max(tagBottomY + 8f, actorRailBottomY + 8f));

            // Draw skill badge (roll cards) or plain type text — the skill tells the player
            // which ability this action tests, replacing the meaningless "判定" label.
            if (hasSkill && !disabled)
            {
                string skillText = SkillInfo.DisplayName(rollSkill!);
                int skillFont = 14;
                int skillTextW = FontManager.MeasureTextWidth(skillText, skillFont);
                float badgeW = skillTextW + 22;
                float badgeH = 20;
                float badgeX = hasRequires
                    ? bounds.X + 42f - badgeW / 2f
                    : bounds.X + (bounds.Width - badgeW) / 2f;
                float badgeY = hasRequires
                    ? controlY + 6f
                    : showButton
                        ? (hasSubtitle ? Math.Max(bounds.Y + 66, subtitleBottom + 2) : bounds.Y + 48)
                    : bounds.Y + bounds.Height - 26;
                var badgeRect = new Rectangle(badgeX, badgeY, badgeW, badgeH);
                Raylib.DrawRectangleRounded(badgeRect, 0.5f, 8, TerminalPalette.AccentDark);
                Raylib.DrawRectangleRoundedLinesEx(badgeRect, 0.5f, 8, 1f, TerminalPalette.Accent);
                FontManager.DrawText(skillText, badgeX + 11, badgeY + 3, skillFont, TerminalPalette.AccentBright);
                if (hasRequires)
                    FontManager.DrawText("+", bounds.X + 84f, controlY + 9f, 14, TerminalPalette.TextMuted);
            }
            else if (!string.IsNullOrEmpty(typeLabel) && !showButton)
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
            // Bottom of the execute button; used to gate the odds preview so it only shows
            // when the card is tall enough to leave room below the button.
            float executeBottomY = bounds.Y + bounds.Height;

            // Draw Slots & Execute Button if there are requirements
            if (hasRequires)
            {
                int M = requires!.Count;
                float slotH = SlotHeight;
                float spacing = 8;
                float totalWidth = 0f;
                for (int j = 0; j < M; j++)
                {
                    totalWidth += SlotWidth(requires[j]);
                    if (j < M - 1)
                        totalWidth += spacing;
                }
                float slotStartX = bounds.X + (bounds.Width - totalWidth) / 2f;
                float slotY = controlY;
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
                float exeH = SlotButtonHeight;
                float exeX = bounds.X + (bounds.Width - exeW) / 2f;
                // 槽位高 32px；其后保留明确的 8px 呼吸空间。
                float exeY = slotY + slotH + ControlToButtonGap;
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
                        TerminalPalette.AccentDark, new Color(65, 65, 112, 255), new Color((byte)50, (byte)50, (byte)55, (byte)255),
                        TerminalPalette.Accent, Color.White, new Color((byte)70, (byte)70, (byte)75, (byte)255),
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
                float exeH = SimpleButtonHeight;
                float exeX = bounds.X + (bounds.Width - exeW) / 2f;
                float exeY = bounds.Y + (hasSubtitle ? SimpleButtonYBaseWithSubtitle : SimpleButtonYBaseNoSubtitle);
                if (hasSubtitle)
                {
                    exeY = Math.Max(exeY, subtitleBottom + 12);
                }
                exeY = Math.Max(exeY, tagBottomY + ControlToButtonGap);
                var exeRect = new Rectangle(exeX, exeY, exeW, exeH);

                if (isExecuting)
                {
                    DrawExecuteProgress(exeRect, executeProgress, executingText);
                }
                else
                {
                    var exeBtn = SSNoir.TerminalApp.Rendering.UiButton.Draw(exeRect, disabled ? "不可用" : "执行", ui, !disabled, 12,
                        TerminalPalette.AccentDark, new Color(65, 65, 112, 255), new Color(50, 50, 55, 255),
                        TerminalPalette.Accent, Color.White, new Color(70, 70, 75, 255),
                        Color.White, new Color(100, 100, 110, 255));
                    
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
                float modifierTagStartY = Math.Max(tagStartY, tagBottomY + 4);
                for (int k = 0; k < effectiveModifiers.Count; k++)
                {
                    var mod = effectiveModifiers[k];
                    string modText = $"{mod.Reason} {(mod.Value > 0 ? "+" : "")}{mod.Value}";
                    int fontSize = 10;
                    int textWidth = FontManager.MeasureTextWidth(modText, fontSize);
                    float tagW = textWidth + 10;
                    float tagH = 16;
                    float tagX = tagStartX;
                    float tagY = modifierTagStartY + k * 20;
                    var tagRect = new Rectangle(tagX, tagY, tagW, tagH);

                    Color tagBg = disabled
                        ? new Color(44, 45, 52, 255)
                        : mod.Value < 0 ? new Color(92, 34, 34, 255)
                        : (mod.Value > 0 ? TerminalPalette.AccentDark : new Color(48, 49, 56, 255));
                    Color tagBorder = disabled
                        ? new Color(88, 90, 98, 255)
                        : mod.Value < 0 ? new Color(180, 60, 60, 255)
                        : (mod.Value > 0 ? TerminalPalette.Accent : new Color(100, 100, 100, 255));
                    Color tagText = disabled ? new Color(145, 146, 152, 255) : Color.White;

                    Raylib.DrawRectangleRounded(tagRect, 0.4f, 4, tagBg);
                    Raylib.DrawRectangleRoundedLinesEx(tagRect, 0.4f, 4, 1f, tagBorder);
                    FontManager.DrawText(modText, tagX + 5, tagY + 3, fontSize, tagText);
                }
            }

            if (localRoll != null)
            {
                DrawLocalRoll(bounds, localRoll, localRollPhase, localRollDisplayDieValue, localRollDisplayScale);
            }
            else if (residue != null)
            {
                DrawResidue(bounds, residue);
            }
            else if (hasSkill && !disabled && hasRequires && actors != null)
            {
                // 命运信息是卡片下缘附件：只有放入骰子后才出现，不占主卡内容区。
                TryDrawFatePreview(bounds, rollSkill!, slotted!, effectiveModifiers, actors, executeBottomY);
            }

            return interaction;
        }

        /// <summary>
        /// Returns the minimum height needed for an action card with a skill rail. The renderer
        /// uses this before laying out rows, so additional active actors grow the card instead
        /// of overlapping its resource controls.
        /// </summary>
        public static float GetMinimumHeight(string subtitle, List<string>? tags,
            List<ActionCost>? requires, string? rollSkill, IReadOnlyList<ActorSnapshot>? actors,
            List<GameClock>? clocks = null, float cardWidth = 240f, bool isInstant = false,
            List<DifficultyModifierInfo>? modifiers = null)
        {
            // 与 DrawCard 的 showButton 判定保持一致，这样徽标要不要把卡片撑高才会跟实际绘制时的标题位置对得上。
            bool hasRequires = requires != null && requires.Count > 0;
            bool showButton = hasRequires || isInstant;
            bool hasSubtitle = !string.IsNullOrWhiteSpace(subtitle);
            float clockPushDown = GetClockPushDown(clocks, cardWidth, showButton, hasSubtitle);

            bool qualifiesForRollLayout = !string.IsNullOrEmpty(rollSkill) && hasRequires && actors != null;
            int activeActorCount = qualifiesForRollLayout
                ? actors!.Count(actor => actor.Status != "away" && actor.Stats.ContainsKey(rollSkill!))
                : 0;

            if (!qualifiesForRollLayout || activeActorCount == 0)
            {
                // 没有可用角色（或本来就不是判定卡）时 DrawCard 仍会画标签，甚至画完整的槽位/按钮，
                // 只是不会有能力轨道——这里补上标签会不会把内容顶出默认高度的判断。
                float tagPushDown = GetTagPushDown(tags, cardWidth, showButton, hasSubtitle, subtitle, hasRequires);
                return DefaultCardHeight + clockPushDown + tagPushDown;
            }

            float subtitleBottom = MeasureSubtitleBottom(subtitle, TitleTopY, hasSubtitle, cardWidth);

            float reservedRailWidth = GetActorAbilityRailReservedWidth(rollSkill!, actors!);
            float tagBottom = MeasureNodeTagsBottom(cardWidth - reservedRailWidth, tags, subtitleBottom + 8f);
            float railTop = subtitleBottom + 8f;
            float railBottom = railTop + activeActorCount * ActorAbilityChipHeight
                + Math.Max(0, activeActorCount - 1) * ActorAbilityChipGap;
            float rollControlY = Math.Max(hasSubtitle ? ControlYBaseWithSubtitle : ControlYBaseNoSubtitle, subtitleBottom + 16f);
            float controlY = Math.Max(rollControlY, Math.Max(railBottom + 8f, tagBottom + 8f));

            // 难度修正标签紧贴在工作/风险标签下方纵向堆叠（每条 20px），条数一多也会顶到槽位。
            int modifierCount = modifiers?.Count ?? 0;
            if (modifierCount > 0)
            {
                float modifiersBottom = Math.Max(subtitleBottom + 8f, tagBottom + 4f) + modifierCount * 20f;
                controlY = Math.Max(controlY, modifiersBottom + 8f);
            }

            // 槽位、按钮以及卡片底部的最小呼吸空间。
            return Math.Max(DefaultCardHeight, controlY + SlotHeight + ControlToButtonGap + SlotButtonHeight + CardBottomPadding) + clockPushDown;
        }

        // 副标题占用的高度：贴顶布局标题固定在 titleY，副标题紧随其后并按同一套换行/收缩规则处理。
        // cardWidth 必须传实际卡宽——之前这里长期写死 216f，跟 DrawCard 用的 bounds.Width-24 只在卡宽正好
        // 是 240 时凑巧一致，一旦卡宽换了两边就会悄悄对不上。
        private static float MeasureSubtitleBottom(string subtitle, float titleY, bool hasSubtitle, float cardWidth)
        {
            if (!hasSubtitle)
            {
                return titleY + TitleToSubtitleGap;
            }

            float subtitleWidth = cardWidth - SubtitleHorizontalMargin;
            int subtitleFontSize = SubtitleFontSize;
            var lines = WrapTextLines(subtitle, subtitleWidth, subtitleFontSize);
            while (lines.Count > MaxSubtitleLines && subtitleFontSize > MinSubtitleFontSize)
            {
                subtitleFontSize--;
                lines = WrapTextLines(subtitle, subtitleWidth, subtitleFontSize);
            }
            lines = ClampWrappedLines(lines, MaxSubtitleLines, subtitleWidth, subtitleFontSize);
            return titleY + TitleToSubtitleGap + lines.Count * (subtitleFontSize + 4);
        }

        // 标签把内容往下顶多少：贴顶布局用该分支实际会画的按钮/槽位落点反推；居中布局只要标签
        // 自然伸展的高度别超过默认高度即可，超多少补多少（跟 GetClockPushDown 的两套逻辑呼应）。
        private static float GetTagPushDown(List<string>? tags, float cardWidth, bool showButton,
            bool hasSubtitle, string subtitle, bool hasRequires)
        {
            if (tags == null || tags.Count == 0) return 0f;

            if (showButton)
            {
                float subtitleBottom = MeasureSubtitleBottom(subtitle, TitleTopY, hasSubtitle, cardWidth);
                float tagBottom = MeasureNodeTagsBottom(cardWidth, tags, subtitleBottom + 8f);

                float contentBottom;
                if (hasRequires)
                {
                    float rollControlY = Math.Max(hasSubtitle ? ControlYBaseWithSubtitle : ControlYBaseNoSubtitle, subtitleBottom + 16f);
                    float controlY = Math.Max(rollControlY, tagBottom + 8f);
                    contentBottom = controlY + SlotHeight + ControlToButtonGap + SlotButtonHeight + CardBottomPadding;
                }
                else
                {
                    float exeY = Math.Max(hasSubtitle ? SimpleButtonYBaseWithSubtitle : SimpleButtonYBaseNoSubtitle,
                        Math.Max(subtitleBottom + 12f, tagBottom + ControlToButtonGap));
                    contentBottom = exeY + SimpleButtonHeight + CardBottomPadding;
                }
                return Math.Max(0f, contentBottom - DefaultCardHeight);
            }
            else
            {
                float titleY = DefaultCardHeight / 2f - (hasSubtitle ? CenteredTitleOffsetWithSubtitle : CenteredTitleOffsetNoSubtitle);
                float subtitleBottom = MeasureSubtitleBottom(subtitle, titleY, hasSubtitle, cardWidth);
                float tagBottom = MeasureNodeTagsBottom(cardWidth, tags, subtitleBottom + 8f);
                return Math.Max(0f, tagBottom + 12f - DefaultCardHeight);
            }
        }

        private static float DrawActorAbilityRail(Rectangle bounds, string skill,
            IReadOnlyList<ActorSnapshot> actors, float startY)
        {
            const float chipH = ActorAbilityChipHeight;
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

                string shortName = actor.Name.Length > 2 ? actor.Name.Substring(0, 2) : actor.Name;
                string label = GetActorAbilityLabel(shortName, level);
                float chipW = Math.Max(58f, FontManager.MeasureTextWidth(label, 10) + 12f);
                var chip = new Rectangle(bounds.X + bounds.Width - chipW - 6f,
                    startY + drawn * (chipH + ActorAbilityChipGap), chipW, chipH);
                Raylib.DrawRectangleRounded(chip, 0.4f, 5, new Color((byte)(r / 5), (byte)(g / 5), (byte)(b / 5), (byte)235));
                Raylib.DrawRectangleRoundedLinesEx(chip, 0.4f, 5, 1.4f, color);
                FontManager.DrawText(label, chip.X + 6f, chip.Y + 5f, 10, color);
                drawn++;
            }

            return drawn == 0
                ? startY
                : startY + drawn * chipH + (drawn - 1) * ActorAbilityChipGap;
        }

        private static float GetActorAbilityRailReservedWidth(string skill,
            IReadOnlyList<ActorSnapshot> actors)
        {
            float widestChip = 0f;
            foreach (var actor in actors)
            {
                if (actor.Status == "away" || !actor.Stats.TryGetValue(skill, out int level))
                {
                    continue;
                }

                string shortName = actor.Name.Length > 2 ? actor.Name.Substring(0, 2) : actor.Name;
                string label = GetActorAbilityLabel(shortName, level);
                widestChip = Math.Max(widestChip, Math.Max(58f, FontManager.MeasureTextWidth(label, 10) + 12f));
            }

            // 右侧留白 6px，标签与能力状态之间保留 8px。
            return widestChip > 0f ? widestChip + 14f : 0f;
        }

        private static string GetActorAbilityLabel(string shortName, int level)
        {
            return $"{shortName} · {level}";
        }

        private static readonly Color OddsFailColor    = new Color(209, 58, 74, 255);
        private static readonly Color OddsNeutralColor  = new Color(214, 169, 78, 255);
        private static readonly Color OddsSuccessColor  = new Color(89, 180, 119, 255);

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
            string modifierReasons = modifiers == null
                ? string.Empty
                : string.Join("、", modifiers.Where(m => m.Value != 0)
                    .Select(m => $"{m.Reason}{(m.Value > 0 ? "+" : string.Empty)}{m.Value}"));
            DrawFateResolutionPanel(bounds, strip, dieSlot.Value, skillLevel.Value, modSum,
                modifierReasons, executeBottomY);
        }

        private static void DrawFateResolutionPanel(Rectangle bounds, RollOutcome[] strip,
            int actionDie, int skill, int modifierTotal, string modifierReasons, float executeBottomY)
        {
            var panel = new Rectangle(bounds.X + 5f, bounds.Y + bounds.Height + 4f,
                bounds.Width - 10f, 54f);
            DrawAttachmentConnector(bounds, panel);
            DrawResolutionPanelFrame(panel);

            int prepared = FateStrip.PreparedValue(actionDie, skill, modifierTotal);
            string modifier = modifierTotal >= 0 ? $"+ {modifierTotal}" : $"− {Math.Abs(modifierTotal)}";
            string formula = $"准备 {prepared} = 骰 {actionDie} + 技能 {skill} {modifier}";
            if (!string.IsNullOrEmpty(modifierReasons)) formula += $"  [{modifierReasons}]";
            int formulaFont = FontManager.MeasureTextWidth(formula, 9) <= panel.Width - 14f ? 9 : 8;
            FontManager.DrawText(formula, panel.X + 7f, panel.Y + 4f, formulaFont,
                new Color(210, 212, 220, 255));

            string summary = FateStrip.Describe(strip);
            int summaryW = FontManager.MeasureTextWidth(summary, 9);
            FontManager.DrawText(summary, panel.X + (panel.Width - summaryW) / 2f, panel.Y + 17f, 9,
                new Color(170, 174, 188, 255));

            // 放骰后、掷骰前的赔率预览：无落格高亮（highlightedFace = 0）。
            DrawOddsStrip(new Rectangle(panel.X + 7f, panel.Y + 33f, panel.Width - 14f, 14f), strip, 0, 8);
        }

        private static void DrawResolutionPanelFrame(Rectangle panel)
        {
            Raylib.DrawRectangleRounded(panel, 0.08f, 4, new Color(16, 18, 25, 245));
            Raylib.DrawRectangleRoundedLinesEx(panel, 0.08f, 4, 1f, new Color(72, 74, 86, 220));
        }

        private static void DrawLocalRoll(Rectangle bounds, ActionReport report, int phase, int displayDieValue, float displayScale)
        {
            var panel = new Rectangle(bounds.X + 5f, bounds.Y + bounds.Height + 4f, bounds.Width - 10f, 58f);
            DrawAttachmentConnector(bounds, panel);
            DrawResolutionPanelFrame(panel);

            bool settled = phase >= 2;
            string label = settled ? FormatOutcome(report.Outcome) : "判定中";
            Color color = settled ? OutcomeColor(report.Outcome) : new Color(220, 220, 235, 255);
            FontManager.DrawText(label, panel.X + 8f, panel.Y + 5f, 11, color);

            string detail = settled
                ? FormatRollDetail(report)
                : $"准备 {report.PreparedValue} · 命运骰滚动...";
            FontManager.DrawText(detail, panel.X + 60f, panel.Y + 6f, 9, new Color(170, 170, 190, 255));

            var strip = FateStrip.Compute(report.ChosenDieValue, report.SkillLevel, report.ModifierTotal);
            int highlightedFace = phase == 0 ? displayDieValue : report.FateDieValue;
            float pulse = phase == 1 ? Math.Max(0f, displayScale - 1f) : 0f;
            DrawOddsStrip(new Rectangle(panel.X + 8f, panel.Y + 23f, panel.Width - 16f, 20f),
                strip, highlightedFace, 9, pulse, settled);

            // 结果定格：面板描边染成结果色，给出清晰的成/败信号。
            if (settled)
                Raylib.DrawRectangleRoundedLinesEx(panel, 0.08f, 4, 1.5f, color);
        }

        private static string FormatRollDetail(ActionReport report)
        {
            return $"准备 {report.PreparedValue} · 命运骰 {report.FateDieValue}";
        }

        private static void DrawAttachmentConnector(Rectangle bounds, Rectangle attachment)
        {
            float centerX = bounds.X + bounds.Width / 2f;
            Raylib.DrawLineEx(new System.Numerics.Vector2(centerX, bounds.Y + bounds.Height - 1f),
                new System.Numerics.Vector2(centerX, attachment.Y + 1f), 2f,
                new Color(TerminalPalette.Accent.R, TerminalPalette.Accent.G, TerminalPalette.Accent.B, (byte)150));
        }

        private static float ResidueHeaderHeight(CardPresentationResidue residue)
            => residue.FateDieValue.HasValue ? 58f : 24f;

        private static Color WithA(Color c, float a)
            => new Color(c.R, c.G, c.B, (byte)Math.Clamp(c.A * a, 0f, 255f));

        // 结算结果 = 命运条「原地定格」+ 结果从其下方揭开。头部与掷骰动画落定态像素一致，
        // 形成无缝冻结（动画一停，命运条就留在原处）；身体（叙事 + 影响）淡入并轻微下滑弹出。
        private static void DrawResidue(Rectangle bounds, CardPresentationResidue residue)
        {
            // residue 只有动画落定后才被绘制——首帧即结果该「揭开」的时刻，惰性记录起点。
            if (residue.RevealStartTime <= 0)
                residue.RevealStartTime = Raylib.GetTime();
            float reveal = (float)Math.Clamp((Raylib.GetTime() - residue.RevealStartTime) / 0.28, 0.0, 1.0);
            float ease = 1f - (float)Math.Pow(1f - reveal, 3f);

            var attachment = new Rectangle(bounds.X + 5f, bounds.Y + bounds.Height + 4f,
                bounds.Width - 10f, ResidueAttachmentHeight(residue));
            DrawAttachmentConnector(bounds, attachment);

            // ── 头部：定格的命运条（几何与内容同 DrawLocalRoll 落定态，实现原地冻结）──
            float headerH = ResidueHeaderHeight(residue);
            var header = new Rectangle(attachment.X, attachment.Y, attachment.Width, headerH);
            DrawResolutionPanelFrame(header);

            bool hasOutcome = residue.RollOutcome.HasValue;
            RollOutcome outcome = residue.RollOutcome ?? RollOutcome.Neutral;
            Color oc = hasOutcome ? OutcomeColor(outcome) : new Color(210, 212, 222, 255);
            string label = hasOutcome ? FormatOutcome(outcome)
                : string.IsNullOrWhiteSpace(residue.Title) ? "行动结果" : residue.Title;
            FontManager.DrawText(label, header.X + 8f, header.Y + 5f, 11, oc);

            if (residue.FateDieValue.HasValue)
            {
                FontManager.DrawText($"准备 {residue.PreparedValue} · 命运骰 {residue.FateDieValue.Value}",
                    header.X + 60f, header.Y + 6f, 9, new Color(170, 170, 190, 255));
                var strip = FateStrip.StripForPrepared(residue.PreparedValue);
                DrawOddsStrip(new Rectangle(header.X + 8f, header.Y + 23f, header.Width - 16f, 20f),
                    strip, residue.FateDieValue.Value, 9, 0f, true);
                Raylib.DrawRectangleRoundedLinesEx(header, 0.08f, 4, 1.5f, oc);
            }

            // ── 身体：结果从命运条下方揭开（淡入 + 轻微下滑）──
            float y = header.Y + headerH + 5f + (1f - ease) * 5f;
            float bodyX = attachment.X + 3f;
            float bodyW = attachment.Width - 6f;

            if (!string.IsNullOrWhiteSpace(residue.Subtitle))
            {
                Raylib.DrawRectangleRounded(new Rectangle(bodyX, y + 1f, 2.5f, 13f), 1f, 2, WithA(oc, ease));
                FontManager.DrawText(residue.Subtitle, bodyX + 9f, y + 2f, 9,
                    WithA(new Color(206, 210, 226, 255), ease));
                y += 19f;
            }

            DrawEffectRows(new Rectangle(bodyX, y, bodyW, 0f), residue.Effects, ease);
        }

        private const float EffectRowHeight = 14f;
        private const float EffectRowGap = 16f;

        private static void DrawEffectRows(Rectangle area, IReadOnlyList<ActionEffectRecord> effects, float alpha = 1f)
        {
            for (int i = 0; i < effects.Count; i++)
                DrawSingleEffectRow(effects[i], area.X, area.Y + i * EffectRowGap, area.Width, alpha);
        }

        public static float ResidueAttachmentHeight(CardPresentationResidue residue)
        {
            float h = ResidueHeaderHeight(residue) + 5f;
            if (!string.IsNullOrWhiteSpace(residue.Subtitle)) h += 19f;
            if (residue.Effects.Count > 0) h += residue.Effects.Count * EffectRowGap;
            return Math.Max(74f, h + 5f);
        }

        // 影响行：极淡的结果色底 + 左侧实心结果色条，标签左、数值右（结果色）。alpha 用于揭开淡入。
        private static void DrawSingleEffectRow(ActionEffectRecord effect, float x, float y, float width, float alpha)
        {
            Color accent = effect.Tone switch
            {
                ActionEffectTone.Positive => new Color(90, 190, 125, 255),
                ActionEffectTone.Negative => new Color(220, 105, 95, 255),
                _ => new Color(140, 158, 200, 255)
            };
            var row = new Rectangle(x, y, width, EffectRowHeight);
            Raylib.DrawRectangleRounded(row, 0.35f, 4, WithA(new Color(accent.R, accent.G, accent.B, (byte)28), alpha));
            Raylib.DrawRectangleRounded(new Rectangle(x, y, 2.5f, EffectRowHeight), 1f, 2, WithA(accent, alpha));

            if (effect.Kind == ActionEffectKind.Note)
            {
                FontManager.DrawText(effect.Text, x + 9f, y + 2f, 9, WithA(new Color(206, 210, 226, 255), alpha));
                return;
            }

            FontManager.DrawText(effect.Label, x + 9f, y + 2f, 9, WithA(new Color(214, 218, 234, 255), alpha));
            string value = effect.Delta.HasValue
                ? (effect.Delta.Value > 0 ? $"+{effect.Delta.Value}" : effect.Delta.Value.ToString())
                : string.Empty;
            if (value.Length > 0)
            {
                int valueW = FontManager.MeasureTextWidth(value, 9);
                FontManager.DrawText(value, x + width - valueW - 8f, y + 2f, 9, WithA(accent, alpha));
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
                RollOutcome.Success => new Color(90, 190, 125, 255), // 薄荷绿
                RollOutcome.Neutral => new Color(255, 182, 147, 255), // 暖杏黄
                RollOutcome.Fail => new Color(220, 105, 95, 255),    // 珊瑚红
                _ => Color.White
            };
        }

        // 命运条即赔率条：实心染色让坏/中/好的比例一眼可读；档位之间留更大的缝，
        // 把「三档占比」读成连续区段而非六颗独立骰子；命运骰落格的骰面抬起金描边。
        // highlightPulse (0..~0.4)：落格弹跳的额外放大量；settled：结果定格时压暗其余格，让命中格更跳。
        public static void DrawOddsStrip(Rectangle bounds, RollOutcome[] strip, int highlightedFace,
            int fontSize, float highlightPulse = 0f, bool settled = false)
        {
            const float gap = 3f, boundaryGap = 10f;
            int boundaries = 0;
            for (int i = 1; i < strip.Length; i++)
                if (strip[i] != strip[i - 1]) boundaries++;

            float cellW = (bounds.Width - gap * (5 - boundaries) - boundaryGap * boundaries) / 6f;
            float x = bounds.X;
            for (int i = 0; i < 6; i++)
            {
                if (i > 0) x += strip[i] != strip[i - 1] ? boundaryGap : gap;
                Color tier = OutcomeColor(strip[i]);
                bool highlighted = i + 1 == highlightedFace;

                // 命中格：抬起 + 弹跳放大；未命中格：默认略暗，结果定格时进一步压暗。
                float pop = highlighted ? highlightPulse * 6f : 0f;
                var cell = new Rectangle(x - pop / 2f, bounds.Y + (highlighted ? -2f : 0f) - pop,
                    cellW + pop, bounds.Height + (highlighted ? 4f : 0f) + pop * 2f);
                float dim = highlighted ? 1f : (settled ? 0.34f : 0.70f);
                Color fill = new Color((byte)(tier.R * dim), (byte)(tier.G * dim), (byte)(tier.B * dim), (byte)255);
                Raylib.DrawRectangleRounded(cell, 0.12f, 3, fill);
                if (highlighted)
                    Raylib.DrawRectangleRoundedLinesEx(cell, 0.12f, 3, 2f, new Color(240, 236, 220, 255));

                string face = (i + 1).ToString();
                int fs = highlighted ? fontSize + 1 : fontSize;
                int faceW = FontManager.MeasureTextWidth(face, fs);
                float textDim = highlighted ? 0.18f : (settled ? 0.10f : 0.20f);
                Color faceColor = new Color((byte)(tier.R * textDim), (byte)(tier.G * textDim * 0.8f),
                    (byte)(tier.B * textDim * 0.8f), (byte)255);
                FontManager.DrawText(face, cell.X + (cell.Width - faceW) / 2f,
                    cell.Y + (cell.Height - fs) / 2f, fs, faceColor);
                x += cellW;
            }
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
            if (string.IsNullOrWhiteSpace(subtitle) && residue.FateDieValue.HasValue)
                subtitle = $"准备 {residue.PreparedValue} · 命运骰 {residue.FateDieValue.Value}";
            if (!string.IsNullOrWhiteSpace(subtitle))
                DrawWrappedText(subtitle, bounds.X + 14f, titleY + 30f, bounds.Width - 28f, 11, new Color(185, 190, 210, 255));

            float effectsY = string.IsNullOrWhiteSpace(subtitle) ? titleY + 30f : titleY + 48f;
            DrawEffectRows(new Rectangle(bounds.X + 10f, effectsY, bounds.Width - 20f, bounds.Y + bounds.Height - effectsY - 8f), residue.Effects);
        }

        // 标签配色：工作/风险标签全局一致，玩家一眼判断类型与风险。
        private static (Color bg, Color border, Color text) TagColors(string label, bool disabled = false)
        {
            if (disabled)
            {
                return (new Color(44, 45, 52, 220), new Color(88, 90, 98, 255), new Color(145, 146, 152, 255));
            }

            switch (label)
            {
                case "交锋":
                    return (new Color(95, 34, 34, 230), new Color(210, 86, 76, 255), new Color(255, 215, 205, 255));
                case "工作": // 能赚钱：青绿
                    return (new Color(28, 58, 64, 220), new Color(90, 180, 190, 255), new Color(210, 240, 245, 255));
                case "低风险": // 绿
                    return (new Color(34, 66, 44, 220), new Color(96, 190, 120, 255), new Color(215, 245, 220, 255));
                case "高风险": // 红
                    return (new Color(90, 40, 34, 225), new Color(214, 96, 74, 255), new Color(255, 220, 205, 255));
                case "非法": // 深红：非法工作/掉关系
                    return (new Color(70, 26, 44, 230), new Color(200, 70, 110, 255), new Color(255, 210, 225, 255));
                default:
                    return (new Color(35, 48, 78, 220), new Color(105, 145, 220, 255), new Color(220, 235, 255, 255));
            }
        }

        private static float DrawNodeTags(Rectangle bounds, List<string>? tags, float startY, bool disabled = false,
            float? rightEdge = null)
        {
            if (tags == null || tags.Count == 0)
            {
                return bounds.Y + 2;
            }

            float x = bounds.X + 6;
            float y = startY;
            float maxRight = rightEdge ?? bounds.X + bounds.Width - 8f;
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
                float tagW = Math.Min(textW + 12f, maxRight - (bounds.X + 6f));
                if (x + tagW > maxRight)
                {
                    x = bounds.X + 6;
                    y += lineH + 3;
                }

                var rect = new Rectangle(x, y, tagW, lineH);
                var (bg, border, text) = TagColors(label, disabled);

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

        private static float MeasureNodeTagsBottom(float width, List<string>? tags, float startY)
        {
            if (tags == null || tags.Count == 0)
            {
                return 2f;
            }

            float x = 6f;
            float y = startY;
            float maxRight = width - 8f;
            const float lineH = 18f;

            foreach (string label in tags)
            {
                if (string.IsNullOrWhiteSpace(label))
                {
                    continue;
                }

                float tagW = Math.Min(FontManager.MeasureTextWidth(label, 10) + 12f, maxRight - 6f);
                if (x + tagW > maxRight)
                {
                    x = 6f;
                    y += lineH + 3f;
                }
                x += tagW + 5f;
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

        // 复刻 DrawCard 里徽标流式换行的判定，供 GetMinimumHeight 在实际绘制前预留同样的高度。
        private static float MeasureClocksReservedHeight(List<GameClock>? clocks, float cardWidth)
        {
            if (clocks == null || clocks.Count == 0) return 0f;

            float badgeX = 6f;
            float badgeY = 6f;
            float rightEdge = cardWidth - 6f;
            foreach (var clock in clocks)
            {
                float badgeW = Math.Min(MeasureClockBadgeWidth(clock), cardWidth - 12f);
                if (badgeX > 6f && badgeX + badgeW > rightEdge)
                {
                    badgeX = 6f;
                    badgeY += 20f;
                }
                badgeX += badgeW + 4f;
            }
            return badgeY + 16f;
        }

        // 时钟徽标把标题往下挤了多少，卡片高度要同步补上——但两种标题布局的"够不够"判断不一样：
        // showButton 卡标题贴顶（Y+8），徽标一旦更高就必然顶到它；居中卡标题在 height/2 附近，
        // 默认高度本身就留了余量，通常不需要额外长高，硬套贴顶的判断只会平白拉长卡片。
        private static float GetClockPushDown(List<GameClock>? clocks, float cardWidth, bool showButton, bool hasSubtitle)
        {
            if (clocks == null || clocks.Count == 0) return 0f;
            float clocksBottom = MeasureClocksReservedHeight(clocks, cardWidth);

            if (showButton)
            {
                return Math.Max(0f, clocksBottom + 4f - TitleTopY);
            }

            float titleOffset = hasSubtitle ? CenteredTitleOffsetWithSubtitle : CenteredTitleOffsetNoSubtitle;
            float requiredHeight = 2f * (clocksBottom + 4f + titleOffset);
            return Math.Max(0f, requiredHeight - DefaultCardHeight);
        }

        private static float MeasureClockBadgeWidth(GameClock clock)
        {
            const int fontSize = 12;
            int labelWidth = FontManager.MeasureTextWidth(clock.Label, fontSize);
            return clock.Style switch
            {
                ClockStyle.Countdown => labelWidth + FontManager.MeasureTextWidth($" {clock.Current}/{clock.Max}", fontSize) + 8f,
                ClockStyle.Segments => labelWidth + 12f + Math.Max(0, clock.Max * 8f - 2f),
                ClockStyle.Pie => labelWidth + 26f,
                _ => labelWidth + 8f
            };
        }

        private static void DrawClockBadge(Rectangle rect, GameClock clock)
        {
            Color activeColor = new Color(130, 130, 250, 255);
            Color inactiveColor = new Color(50, 50, 60, 255);
            Color textColor = new Color(220, 220, 240, 255);

            Raylib.DrawRectangleRounded(rect, 0.4f, 4, new Color(20, 20, 25, 180));
            Raylib.DrawRectangleRoundedLinesEx(rect, 0.4f, 4, 1f, new Color(80, 80, 100, 255));

            if (clock.Style == ClockStyle.Countdown)
            {
                int fontSize = 12;
                string progress = $" {clock.Current}/{clock.Max}";
                float progressW = FontManager.MeasureTextWidth(progress, fontSize);
                string label = FitTextWithEllipsis(clock.Label, rect.Width - progressW - 8f, fontSize);
                FontManager.DrawText(label + progress, rect.X + 4f, rect.Y + 2f, fontSize, textColor);
            }
            else if (clock.Style == ClockStyle.Segments)
            {
                int fontSize = 12;
                int dotSize = 6;
                int spacing = 2;
                float dotsW = Math.Max(0, clock.Max * (dotSize + spacing) - spacing);
                string labelText = FitTextWithEllipsis(clock.Label, rect.Width - dotsW - 12f, fontSize);
                int labelWidth = FontManager.MeasureTextWidth(labelText, fontSize);
                FontManager.DrawText(labelText, rect.X + 4f, rect.Y + 2f, fontSize, textColor);

                float dotStartX = rect.X + rect.Width - dotsW - 4f;
                for (int i = 0; i < clock.Max; i++)
                {
                    var dotRect = new Rectangle(dotStartX + i * (dotSize + spacing), rect.Y + (rect.Height - dotSize) / 2f, dotSize, dotSize);
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

            }
            else if (clock.Style == ClockStyle.Pie)
            {
                int fontSize = 12;
                string labelText = FitTextWithEllipsis(clock.Label, rect.Width - 26f, fontSize);
                int labelWidth = FontManager.MeasureTextWidth(labelText, fontSize);

                float radius = 7f;
                FontManager.DrawText(labelText, rect.X + 4f, rect.Y + 2f, fontSize, textColor);

                var center = new System.Numerics.Vector2(rect.X + rect.Width - radius - 4f, rect.Y + rect.Height / 2f);
                // Empty ring
                Raylib.DrawCircleLines((int)center.X, (int)center.Y, radius, new Color(70, 70, 90, 255));
                // Filled sector
                if (clock.Max > 0 && clock.Current > 0)
                {
                    float pct = (float)clock.Current / clock.Max;
                    Raylib.DrawCircleSector(center, radius, -90f, -90f + 360f * pct, 36, activeColor);
                }
            }
        }

        private static string FitTextWithEllipsis(string text, float maxWidth, int fontSize)
        {
            if (maxWidth <= 0f) return string.Empty;
            if (FontManager.MeasureTextWidth(text, fontSize) <= maxWidth) return text;
            const string ellipsis = "…";
            if (FontManager.MeasureTextWidth(ellipsis, fontSize) > maxWidth) return string.Empty;
            int length = text.Length;
            while (length > 0 && FontManager.MeasureTextWidth(text[..length] + ellipsis, fontSize) > maxWidth)
                length--;
            return text[..length] + ellipsis;
        }
    }
}
