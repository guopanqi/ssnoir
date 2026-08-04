using System;
using System.Collections.Generic;
using Raylib_cs;
using SSNoir.Core;

namespace SSNoir.Rendering
{
    public static class OverlayWidget
    {
        public struct OverlayInteraction
        {
            public bool ConfirmClicked;
            public bool SpotlightDismissClicked;
            public bool DialogueAdvanceClicked;
        }

        public static OverlayInteraction Draw(RendererState state, NotificationCenter notificationCenter, SSNoir.TerminalApp.Rendering.UiInteractionContext ui, float windowWidth, float windowHeight)
        {
            var interaction = new OverlayInteraction { ConfirmClicked = false, SpotlightDismissClicked = false, DialogueAdvanceClicked = false };

            // 1. Toast Notifications
            var visibleNotifs = notificationCenter.GetVisible();
            for (int i = 0; i < visibleNotifs.Count; i++)
            {
                var notif = visibleNotifs[i];
                float remaining = notif.Duration - notif.ElapsedTime;
                float alpha = 1f;
                if (remaining < 0.5f)
                {
                    alpha = Math.Clamp(remaining / 0.5f, 0f, 1f);
                }

                // Layout settings
                float cardW = 240f;
                float cardH = 34f;
                float cardX = windowWidth - cardW - 40f;
                float cardY = 75f + i * (cardH + 8f);
                var cardRect = new Rectangle(cardX, cardY, cardW, cardH);

                // Define colors based on Kind
                Color bg, border, textCol;
                switch (notif.Kind)
                {
                    case NotificationKind.Success:
                        bg = new Color((byte)15, (byte)40, (byte)20, (byte)(alpha * 230));
                        border = new Color((byte)50, (byte)200, (byte)80, (byte)(alpha * 255));
                        textCol = new Color((byte)220, (byte)255, (byte)220, (byte)(alpha * 255));
                        break;
                    case NotificationKind.Error:
                        bg = new Color((byte)45, (byte)15, (byte)15, (byte)(alpha * 230));
                        border = new Color((byte)220, (byte)60, (byte)60, (byte)(alpha * 255));
                        textCol = new Color((byte)255, (byte)220, (byte)220, (byte)(alpha * 255));
                        break;
                    case NotificationKind.Warning:
                        bg = new Color((byte)40, (byte)30, (byte)15, (byte)(alpha * 230));
                        border = new Color((byte)220, (byte)160, (byte)40, (byte)(alpha * 255));
                        textCol = new Color((byte)255, (byte)240, (byte)200, (byte)(alpha * 255));
                        break;
                    case NotificationKind.Info:
                    default:
                        bg = new Color((byte)15, (byte)20, (byte)40, (byte)(alpha * 230));
                        border = new Color((byte)80, (byte)150, (byte)240, (byte)(alpha * 255));
                        textCol = new Color((byte)220, (byte)240, (byte)255, (byte)(alpha * 255));
                        break;
                }

                Raylib.DrawRectangleRounded(cardRect, 0.2f, 4, bg);
                Raylib.DrawRectangleRoundedLinesEx(cardRect, 0.2f, 4, 1.2f, border);
                FontManager.DrawText(notif.Text, cardX + 15, cardY + 9, 12, textCol);
            }

            // 2. Trailing Selected Resource (following mouse cursor)
            if (state.SelectedResource != null)
            {
                float overlayW = state.SelectedResource.Type == "die" ? 36f : 72f;
                float overlayH = 32f;
                var rect = new Rectangle(ui.Mouse.X + 12, ui.Mouse.Y + 12, overlayW, overlayH);

                Raylib.DrawRectangleRounded(rect, 0.2f, 4, new Color(50, 50, 90, 200));
                Raylib.DrawRectangleRoundedLinesEx(rect, 0.2f, 4, 1.5f, new Color(150, 150, 250, 255));

                string text = state.SelectedResource.Type == "die" 
                    ? state.SelectedResource.Value.ToString() 
                    : (state.SelectedResource.Qty > 1 ? $"{state.SelectedResource.ItemName}x{state.SelectedResource.Qty}" : state.SelectedResource.ItemName);
                int textW = FontManager.MeasureTextWidth(text, 12);
                FontManager.DrawText(text, rect.X + (overlayW - textW) / 2f, rect.Y + 8, 12, Color.White);
            }

            DrawBanterBubble(state, windowWidth, windowHeight);

            // 3. Heavy Roll Result Modal
            if (state.ActiveRollResult != null
                && OutcomePresentationPolicy.ShouldUseRollModal(state.ActiveRollResult, state.ActiveRollActionName))
            {
                Raylib.DrawRectangle(0, 0, (int)windowWidth, (int)windowHeight, new Color(0, 0, 0, 180));

                float modalW = 440;
                float modalH = 310;
                float modalX = (windowWidth - modalW) / 2f;
                float modalY = (windowHeight - modalH) / 2f;
                var modalRect = new Rectangle(modalX, modalY, modalW, modalH);

                Raylib.DrawRectangleRounded(modalRect, 0.1f, 8, new Color(30, 30, 42, 255));
                Raylib.DrawRectangleRoundedLinesEx(modalRect, 0.1f, 8, 2f, new Color(100, 100, 130, 255));

                string title = string.IsNullOrEmpty(state.ActiveRollActionName)
                    ? "[判定结果]"
                    : $"[判定结果] {state.ActiveRollActionName}";
                int titleW = FontManager.MeasureTextWidth(title, 18);
                FontManager.DrawText(title, modalX + (modalW - titleW) / 2f, modalY + 20, 18, Color.White);

                float contentY = modalY + 50;

                // 命运条本身就是概率表；掷骰阶段命运高亮减速扫掠，落格弹跳，定格后压暗其余格。
                var fateStrip = FateStrip.Compute(state.ActiveRollResult.ChosenDieValue,
                    state.ActiveRollResult.SkillLevel, state.ActiveRollResult.ModifierTotal);
                int highlightedFace = state.ActiveRollPhase == 0
                    ? state.ActiveRollDisplayDieValue
                    : state.ActiveRollResult.FateDieValue;
                float stripPulse = state.ActiveRollPhase == 1
                    ? Math.Max(0f, state.ActiveRollDisplayScale - 1f)
                    : 0f;
                bool stripSettled = state.ActiveRollPhase >= 2;
                string summary = FateStrip.Describe(fateStrip);
                int summaryW = FontManager.MeasureTextWidth(summary, 12);
                FontManager.DrawText(summary, modalX + (modalW - summaryW) / 2f, contentY, 12,
                    new Color(175, 178, 192, 255));
                contentY += 20f;

                float stripW = 52f * 6f + 7f * 5f;
                float startX = modalX + (modalW - stripW) / 2f;
                CardWidget.DrawOddsStrip(new Rectangle(startX, contentY, stripW, 37f), fateStrip,
                    highlightedFace, 14, stripPulse, stripSettled);

                contentY += 52f;

                // Details revealed in phase >= 1
                if (state.ActiveRollPhase >= 1)
                {
                    string mod = state.ActiveRollResult.ModifierTotal >= 0
                        ? $"+ {state.ActiveRollResult.ModifierTotal}"
                        : $"− {Math.Abs(state.ActiveRollResult.ModifierTotal)}";
                    string line1 = $"准备 {state.ActiveRollResult.PreparedValue} = 骰 {state.ActiveRollResult.ChosenDieValue} + 技能 {state.ActiveRollResult.SkillLevel} {mod}";
                    FontManager.DrawText(line1, modalX + 40, contentY, 14, new Color(200, 200, 220, 255));
                    contentY += 22;

                    string line2 = $"命运骰 {state.ActiveRollResult.FateDieValue} 落在赔率条上";
                    FontManager.DrawText(line2, modalX + 40, contentY, 14, new Color(220, 220, 210, 255));
                    contentY += 25;
                }

                // Outcome and confirmation button revealed in phase >= 2
                if (state.ActiveRollPhase >= 2)
                {
                    string outcomeText = state.ActiveRollResult.Outcome switch
                    {
                        RollOutcome.Success => "判定成功",
                        RollOutcome.Neutral => "判定中性",
                        RollOutcome.Fail => "判定失败",
                        _ => ""
                    };

                    Color outcomeColor = state.ActiveRollResult.Outcome switch
                    {
                        RollOutcome.Fail => new Color(250, 80, 80, 255),
                        RollOutcome.Neutral => new Color(250, 220, 100, 255),
                        RollOutcome.Success => new Color(80, 250, 80, 255),
                        _ => Color.White
                    };

                    string outcomeStr = $"判定结论: {outcomeText}";
                    int outW = FontManager.MeasureTextWidth(outcomeStr, 16);
                    FontManager.DrawText(outcomeStr, modalX + (modalW - outW) / 2f, contentY, 16, outcomeColor);
                    contentY += 24;

                    var presentation = state.ActiveRollResult.OutcomePresentation;
                    if (presentation != null && presentation.HasText)
                    {
                        int resultTitleW = FontManager.MeasureTextWidth(presentation.Title, 15);
                        FontManager.DrawText(presentation.Title, modalX + (modalW - resultTitleW) / 2f, contentY, 15, Color.White);
                    }

                    float btnW = 90;
                    float btnH = 32;
                    float btnX = modalX + (modalW - btnW) / 2f;
                    float btnY = modalY + modalH - 45;
                    var btnRect = new Rectangle(btnX, btnY, btnW, btnH);
                    var modalUi = new SSNoir.TerminalApp.Rendering.UiInteractionContext
                    {
                        Mouse = ui.Mouse,
                        IsLocked = false
                    };
                    var confirmBtn = SSNoir.TerminalApp.Rendering.UiButton.Draw(btnRect, "确定", modalUi, true, 14,
                        new Color((byte)50, (byte)50, (byte)70, (byte)255), new Color((byte)80, (byte)80, (byte)110, (byte)255), null,
                        new Color((byte)90, (byte)90, (byte)120, (byte)255), new Color((byte)180, (byte)180, (byte)250, (byte)255), null,
                        Color.White, null);

                    if (confirmBtn.Clicked)
                    {
                        interaction.ConfirmClicked = true;
                    }
                }
            }

            // 4. Heavy Instant Result Modal
            if (state.ActiveOutcomeResult != null)
            {
                Raylib.DrawRectangle(0, 0, (int)windowWidth, (int)windowHeight, new Color(0, 0, 0, 180));

                float modalW = 380;
                float modalH = 190;
                float modalX = (windowWidth - modalW) / 2f;
                float modalY = (windowHeight - modalH) / 2f;
                var modalRect = new Rectangle(modalX, modalY, modalW, modalH);

                Raylib.DrawRectangleRounded(modalRect, 0.1f, 8, new Color(30, 30, 42, 255));
                Raylib.DrawRectangleRoundedLinesEx(modalRect, 0.1f, 8, 2f, new Color(100, 100, 130, 255));

                var presentation = state.ActiveOutcomeResult.OutcomePresentation;
                string title = presentation?.Title ?? state.ActiveOutcomeActionName;
                int titleW = FontManager.MeasureTextWidth(title, 18);
                FontManager.DrawText(title, modalX + (modalW - titleW) / 2f, modalY + 30, 18, Color.White);

                float btnW = 90;
                float btnH = 32;
                float btnX = modalX + (modalW - btnW) / 2f;
                float btnY = modalY + modalH - 48;
                var btnRect = new Rectangle(btnX, btnY, btnW, btnH);
                var modalUi = new SSNoir.TerminalApp.Rendering.UiInteractionContext
                {
                    Mouse = ui.Mouse,
                    IsLocked = false
                };
                var confirmBtn = SSNoir.TerminalApp.Rendering.UiButton.Draw(btnRect, "确定", modalUi, true, 14,
                    new Color((byte)50, (byte)50, (byte)70, (byte)255), new Color((byte)80, (byte)80, (byte)110, (byte)255), null,
                    new Color((byte)90, (byte)90, (byte)120, (byte)255), new Color((byte)180, (byte)180, (byte)250, (byte)255), null,
                    Color.White, null);

                if (confirmBtn.Clicked)
                {
                    interaction.ConfirmClicked = true;
                }
            }

            var dialogue = state.ActiveImmediateDialogue ?? state.ActiveActionDialogue;
            int dialogueLineIndex = state.ActiveImmediateDialogue != null
                ? state.ActiveImmediateDialogueLineIndex
                : state.ActiveActionDialogueLineIndex;
            if (dialogue != null)
            {
                DrawDialogueOverlay(dialogue, dialogueLineIndex, ui, windowWidth, windowHeight, ref interaction);
            }

            var displaySpotlight = state.ActiveActionSpotlight ?? state.Spotlight;
            if (displaySpotlight != null)
            {
                Raylib.DrawRectangle(0, 0, (int)windowWidth, (int)windowHeight, new Color(0, 0, 0, 185));

                float modalW = 420;
                float modalH = 210;
                float modalX = (windowWidth - modalW) / 2f;
                float modalY = (windowHeight - modalH) / 2f;
                var modalRect = new Rectangle(modalX, modalY, modalW, modalH);

                Raylib.DrawRectangleRounded(modalRect, 0.1f, 8, new Color(30, 30, 42, 255));
                Raylib.DrawRectangleRoundedLinesEx(modalRect, 0.1f, 8, 2f, new Color(120, 125, 170, 255));

                int titleW = FontManager.MeasureTextWidth(displaySpotlight.Title, 20);
                FontManager.DrawText(displaySpotlight.Title, modalX + (modalW - titleW) / 2f, modalY + 34, 20, Color.White);

                if (!string.IsNullOrWhiteSpace(displaySpotlight.Subtitle))
                {
                    DrawWrappedText(displaySpotlight.Subtitle, modalX + 46, modalY + 76, modalW - 92, 13, new Color(210, 212, 232, 255));
                }

                float btnW = 96;
                float btnH = 32;
                float btnX = modalX + (modalW - btnW) / 2f;
                float btnY = modalY + modalH - 48;
                var btnRect = new Rectangle(btnX, btnY, btnW, btnH);
                var modalUi = new SSNoir.TerminalApp.Rendering.UiInteractionContext
                {
                    Mouse = ui.Mouse,
                    IsLocked = false
                };
                var confirmBtn = SSNoir.TerminalApp.Rendering.UiButton.Draw(btnRect, "确定", modalUi, true, 14,
                    new Color((byte)50, (byte)50, (byte)70, (byte)255), new Color((byte)80, (byte)80, (byte)110, (byte)255), null,
                    new Color((byte)90, (byte)90, (byte)120, (byte)255), new Color((byte)180, (byte)180, (byte)250, (byte)255), null,
                    Color.White, null);

                if (confirmBtn.Clicked)
                {
                    interaction.SpotlightDismissClicked = true;
                }
            }

            return interaction;
        }

        private static void DrawDialogueOverlay(
            DialogueSequence dialogue,
            int lineIndex,
            SSNoir.TerminalApp.Rendering.UiInteractionContext ui,
            float windowWidth,
            float windowHeight,
            ref OverlayInteraction interaction)
        {
            if (lineIndex < 0 || lineIndex >= dialogue.Lines.Count)
                throw new InvalidOperationException("对白层收到越界的台词索引。");

            var line = dialogue.Lines[lineIndex];
            Raylib.DrawRectangle(0, 0, (int)windowWidth, (int)windowHeight, new Color(5, 6, 12, 105));

            float bubbleW = Math.Min(680f, windowWidth - 96f);
            float bubbleH = 184f;
            float bubbleX = (windowWidth - bubbleW) / 2f;
            float bubbleY = windowHeight - bubbleH - 70f;
            var bubbleRect = new Rectangle(bubbleX, bubbleY, bubbleW, bubbleH);

            Raylib.DrawRectangleRounded(bubbleRect, 0.08f, 8, TerminalPalette.SurfaceRaised);
            Raylib.DrawRectangleRoundedLinesEx(bubbleRect, 0.08f, 8, 1.5f, TerminalPalette.AccentDark);
            Raylib.DrawRectangleRounded(new Rectangle(bubbleX + 20f, bubbleY + 18f, 5f, 28f), 0.5f, 4, TerminalPalette.Accent);

            FontManager.DrawText(line.Speaker, bubbleX + 40f, bubbleY + 20f, 18, TerminalPalette.AccentBright);
            string progress = $"{lineIndex + 1}/{dialogue.Lines.Count}";
            int progressW = FontManager.MeasureTextWidth(progress, 12);
            FontManager.DrawText(progress, bubbleX + bubbleW - 28f - progressW, bubbleY + 25f, 12, TerminalPalette.TextMuted);

            DrawWrappedText(line.Text, bubbleX + 40f, bubbleY + 64f, bubbleW - 80f, 16, TerminalPalette.Text);

            string hint = "点击继续";
            int hintW = FontManager.MeasureTextWidth(hint, 12);
            FontManager.DrawText(hint, bubbleX + bubbleW - 30f - hintW, bubbleY + bubbleH - 27f, 12, TerminalPalette.TextMuted);

            // 对白是全屏阻塞层：气泡之外的遮罩也承担“继续”的点击目标，
            // 但底层世界仍由 Renderer 的 inputBlocked 明确锁定。
            var dialogueUi = new SSNoir.TerminalApp.Rendering.UiInteractionContext { Mouse = ui.Mouse };
            var overlayRect = new Rectangle(0f, 0f, windowWidth, windowHeight);
            if (dialogueUi.WasClicked(overlayRect))
                interaction.DialogueAdvanceClicked = true;
        }

        private static void DrawBanterBubble(RendererState state, float windowWidth, float windowHeight)
        {
            if (state.IsBanterSuspended)
                return;

            var sequence = state.ActiveBanter;
            if (sequence == null)
                return;
            if (state.ActiveBanterLineIndex < 0 || state.ActiveBanterLineIndex >= sequence.Lines.Count)
                throw new InvalidOperationException("插话气泡收到越界的台词索引。");

            var line = sequence.Lines[state.ActiveBanterLineIndex];
            bool anchored = state.VisibleNodeCardBounds.TryGetValue(line.Speaker, out var cardRect);
            float bubbleW = anchored ? 276f : 300f;
            float textWidth = bubbleW - 32f;
            var textLines = WrapTextLines(line.Text, textWidth, 13);
            float bubbleH = 46f + textLines.Count * 17f;
            float bubbleX;
            float bubbleY;
            bool tailPointsDown;

            if (anchored)
            {
                bubbleX = Math.Clamp(cardRect.X + (cardRect.Width - bubbleW) / 2f, 12f, windowWidth - bubbleW - 12f);
                bubbleY = cardRect.Y - bubbleH - 12f;
                tailPointsDown = true;
                if (bubbleY < 52f)
                {
                    bubbleY = Math.Min(cardRect.Y + cardRect.Height + 12f, windowHeight - bubbleH - 12f);
                    tailPointsDown = false;
                }
            }
            else
            {
                bubbleX = windowWidth - bubbleW - 28f;
                bubbleY = 84f;
                tailPointsDown = false;
            }

            var bubbleRect = new Rectangle(bubbleX, bubbleY, bubbleW, bubbleH);
            Raylib.DrawRectangleRounded(bubbleRect, 0.12f, 6, new Color(27, 30, 43, 238));
            Raylib.DrawRectangleRoundedLinesEx(bubbleRect, 0.12f, 6, 1.2f, TerminalPalette.AccentDark);

            float tailX = Math.Clamp(
                anchored ? cardRect.X + cardRect.Width / 2f : bubbleX + 28f,
                bubbleX + 18f,
                bubbleX + bubbleW - 18f);
            if (tailPointsDown)
            {
                Raylib.DrawTriangle(
                    new System.Numerics.Vector2(tailX - 8f, bubbleY + bubbleH - 1f),
                    new System.Numerics.Vector2(tailX + 8f, bubbleY + bubbleH - 1f),
                    new System.Numerics.Vector2(tailX, bubbleY + bubbleH + 8f),
                    new Color(27, 30, 43, 238));
            }
            else if (anchored)
            {
                Raylib.DrawTriangle(
                    new System.Numerics.Vector2(tailX - 8f, bubbleY + 1f),
                    new System.Numerics.Vector2(tailX + 8f, bubbleY + 1f),
                    new System.Numerics.Vector2(tailX, bubbleY - 8f),
                    new Color(27, 30, 43, 238));
            }

            FontManager.DrawText(line.Speaker, bubbleX + 16f, bubbleY + 11f, 12, TerminalPalette.AccentBright);
            float textY = bubbleY + 29f;
            foreach (var textLine in textLines)
            {
                FontManager.DrawText(textLine, bubbleX + 16f, textY, 13, TerminalPalette.Text);
                textY += 17f;
            }
        }

        internal static List<string> WrapTextLines(string text, float width, int fontSize)
        {
            var lines = new List<string>();
            string currentLine = string.Empty;
            foreach (char c in text)
            {
                if (c == '\n')
                {
                    lines.Add(currentLine);
                    currentLine = string.Empty;
                    continue;
                }

                string candidate = currentLine + c;
                if (FontManager.MeasureTextWidth(candidate, fontSize) > width && currentLine.Length > 0)
                {
                    lines.Add(currentLine);
                    currentLine = c.ToString();
                }
                else
                {
                    currentLine = candidate;
                }
            }

            if (currentLine.Length > 0)
                lines.Add(currentLine);
            return lines.Count > 0 ? lines : new List<string> { string.Empty };
        }

        private static void DrawWrappedText(string text, float x, float y, float width, int fontSize, Color color)
        {
            float currentY = y;
            string currentLine = "";

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\n')
                {
                    FontManager.DrawText(currentLine, x, currentY, fontSize, color);
                    currentLine = "";
                    currentY += fontSize + 4;
                    continue;
                }

                string testLine = currentLine + c;
                int testW = FontManager.MeasureTextWidth(testLine, fontSize);
                if (testW > width)
                {
                    if (currentLine.Length > 0)
                    {
                        FontManager.DrawText(currentLine, x, currentY, fontSize, color);
                        currentLine = c.ToString();
                        currentY += fontSize + 4;
                    }
                    else
                    {
                        FontManager.DrawText(testLine, x, currentY, fontSize, color);
                        currentLine = "";
                        currentY += fontSize + 4;
                    }
                }
                else
                {
                    currentLine = testLine;
                }
            }

            if (currentLine.Length > 0)
            {
                FontManager.DrawText(currentLine, x, currentY, fontSize, color);
            }
        }
    }
}
