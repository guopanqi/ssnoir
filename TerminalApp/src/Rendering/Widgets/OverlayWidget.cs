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
        }

        public static OverlayInteraction Draw(RendererState state, NotificationCenter notificationCenter, SSNoir.TerminalApp.Rendering.UiInteractionContext ui, float windowWidth, float windowHeight)
        {
            var interaction = new OverlayInteraction { ConfirmClicked = false, SpotlightDismissClicked = false };

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
                        contentY += 20;
                        DrawWrappedText(presentation.Subtitle, modalX + 40, contentY, modalW - 80, 12, new Color(205, 205, 225, 255));
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

                if (presentation != null && !string.IsNullOrWhiteSpace(presentation.Subtitle))
                {
                    DrawWrappedText(presentation.Subtitle, modalX + 42, modalY + 66, modalW - 84, 13, new Color(205, 205, 225, 255));
                }

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
