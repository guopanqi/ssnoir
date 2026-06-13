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
        }

        public static OverlayInteraction Draw(RendererState state, System.Numerics.Vector2 mousePos, float windowWidth, float windowHeight)
        {
            var interaction = new OverlayInteraction { ConfirmClicked = false };

            // 1. Toast Notification
            if (state.UiNotificationTimer > 0 && !string.IsNullOrEmpty(state.UiNotification))
            {
                int toastW = FontManager.MeasureTextWidth(state.UiNotification, 14) + 40;
                float toastX = (windowWidth - toastW) / 2f;
                float toastY = 15f;
                var toastRect = new Rectangle(toastX, toastY, toastW, 30);
                
                Raylib.DrawRectangleRounded(toastRect, 0.4f, 4, new Color(120, 20, 30, 230));
                Raylib.DrawRectangleRoundedLinesEx(toastRect, 0.4f, 4, 1.5f, new Color(180, 40, 50, 255));
                FontManager.DrawText(state.UiNotification, toastX + 20, toastY + 7, 14, Color.White);
            }

            // 2. Trailing Selected Resource (following mouse cursor)
            if (state.SelectedResource != null)
            {
                float overlayW = state.SelectedResource.Type == "die" ? 32f : 68f;
                float overlayH = 32f;
                var rect = new Rectangle(mousePos.X + 12, mousePos.Y + 12, overlayW, overlayH);

                Raylib.DrawRectangleRounded(rect, 0.2f, 4, new Color(50, 50, 90, 200));
                Raylib.DrawRectangleRoundedLinesEx(rect, 0.2f, 4, 1.5f, new Color(150, 150, 250, 255));

                string text = state.SelectedResource.Type == "die" 
                    ? state.SelectedResource.Value.ToString() 
                    : state.SelectedResource.ItemName;
                int textW = FontManager.MeasureTextWidth(text, 12);
                FontManager.DrawText(text, rect.X + (overlayW - textW) / 2f, rect.Y + 8, 12, Color.White);
            }

            // 3. Roll Result Modal
            if (state.ActiveRollResult != null)
            {
                Raylib.DrawRectangle(0, 0, (int)windowWidth, (int)windowHeight, new Color(0, 0, 0, 180));

                float modalW = 380;
                float modalH = 290;
                float modalX = (windowWidth - modalW) / 2f;
                float modalY = (windowHeight - modalH) / 2f;
                var modalRect = new Rectangle(modalX, modalY, modalW, modalH);

                Raylib.DrawRectangleRounded(modalRect, 0.1f, 8, new Color(30, 30, 42, 255));
                Raylib.DrawRectangleRoundedLinesEx(modalRect, 0.1f, 8, 2f, new Color(100, 100, 130, 255));

                string title = $"[判定结果] {state.ActiveRollActionName}";
                int titleW = FontManager.MeasureTextWidth(title, 18);
                FontManager.DrawText(title, modalX + (modalW - titleW) / 2f, modalY + 20, 18, Color.White);

                float contentY = modalY + 50;

                // Render all rolled dice side-by-side, centered
                int totalDice = 1 + state.ActiveRollResult.RandomDice.Count;
                float dieWidth = 60f;
                float spacing = 20f;
                float totalWidth = totalDice * dieWidth + (totalDice - 1) * spacing;
                float startX = modalX + (modalW - totalWidth) / 2f;

                for (int i = 0; i < totalDice; i++)
                {
                    float x = startX + i * (dieWidth + spacing);
                    
                    int val;
                    bool isWinner = false;
                    float scale = 1.0f;
                    Color dieColor;

                    if (state.ActiveRollPhase == 0)
                    {
                        var rand = new Random();
                        val = rand.Next(1, 7);
                        scale = 0.9f + (float)rand.NextDouble() * 0.2f;
                        dieColor = new Color(255, 182, 147, 255); // Burnt Amber
                    }
                    else
                    {
                        val = (i == 0) ? state.ActiveRollResult.ChosenDieValue : state.ActiveRollResult.RandomDice[i - 1];
                        isWinner = (val == state.ActiveRollResult.FinalRollValue);

                        if (state.ActiveRollPhase == 1)
                        {
                            scale = isWinner ? state.ActiveRollDisplayScale : 1.0f;
                        }
                        else // Phase 2
                        {
                            scale = isWinner ? 1.1f : 0.9f;
                        }

                        dieColor = isWinner ? new Color(255, 182, 147, 255) : new Color(110, 110, 130, 255);
                    }

                    int fontSize = (int)(32 * scale); // Base size is 32 for multiple dice
                    string text = $"D{val}";
                    int textW = FontManager.MeasureTextWidth(text, fontSize);

                    // Draw die container box
                    var boxRect = new Rectangle(x, contentY, dieWidth, 50);
                    Color boxBg = isWinner ? new Color(50, 40, 45, 255) : new Color(25, 25, 35, 255);
                    Color boxBorder = dieColor;

                    Raylib.DrawRectangleRounded(boxRect, 0.15f, 4, boxBg);
                    Raylib.DrawRectangleRoundedLinesEx(boxRect, 0.15f, 4, 1.5f, boxBorder);

                    // Center text in container
                    FontManager.DrawText(text, boxRect.X + (dieWidth - textW) / 2f, boxRect.Y + (50 - fontSize) / 2f, fontSize, dieColor);
                }

                contentY += 65;

                // Details revealed in phase >= 1
                if (state.ActiveRollPhase >= 1)
                {
                    string line1 = $"投入行动力骰子值: {state.ActiveRollResult.ChosenDieValue}";
                    FontManager.DrawText(line1, modalX + 40, contentY, 14, new Color(200, 200, 220, 255));
                    contentY += 20;

                    string line2 = state.ActiveRollResult.RandomDice.Count > 0 
                        ? $"额外技能掷骰结果: {string.Join(", ", state.ActiveRollResult.RandomDice)}"
                        : "无额外技能掷骰 (技能等级为1)";
                    FontManager.DrawText(line2, modalX + 40, contentY, 14, new Color(200, 200, 220, 255));
                    contentY += 20;

                    string lineMod = "难度修正: ";
                    if (state.ActiveRollResult.DifficultyModifiers.Count > 0)
                    {
                        var modStrList = new List<string>();
                        foreach (var m in state.ActiveRollResult.DifficultyModifiers)
                        {
                            modStrList.Add($"{m.Reason}({(m.Value > 0 ? "+" : "")}{m.Value})");
                        }
                        lineMod += string.Join(", ", modStrList);
                    }
                    else
                    {
                        lineMod += "无";
                    }
                    FontManager.DrawText(lineMod, modalX + 40, contentY, 14, new Color(180, 180, 200, 255));
                    contentY += 20;

                    string line3 = $"最终修正判定值: {state.ActiveRollResult.ModifiedRollValue} (原始最大值 {state.ActiveRollResult.FinalRollValue})";
                    FontManager.DrawText(line3, modalX + 40, contentY, 14, new Color(220, 220, 250, 255));
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

                    float btnW = 90;
                    float btnH = 32;
                    float btnX = modalX + (modalW - btnW) / 2f;
                    float btnY = modalY + modalH - 45;
                    var btnRect = new Rectangle(btnX, btnY, btnW, btnH);
                    bool btnHover = Raylib.CheckCollisionPointRec(mousePos, btnRect);

                    Color bBg = btnHover ? new Color(80, 80, 110, 255) : new Color(50, 50, 70, 255);
                    Color bBorder = btnHover ? new Color(180, 180, 250, 255) : new Color(90, 90, 120, 255);

                    Raylib.DrawRectangleRounded(btnRect, 0.2f, 4, bBg);
                    Raylib.DrawRectangleRoundedLinesEx(btnRect, 0.2f, 4, 1.5f, bBorder);

                    string btnText = "确定";
                    int btnTextW = FontManager.MeasureTextWidth(btnText, 14);
                    FontManager.DrawText(btnText, btnX + (btnW - btnTextW) / 2f, btnY + 8, 14, Color.White);

                    if (btnHover && Raylib.IsMouseButtonPressed(MouseButton.Left))
                    {
                        interaction.ConfirmClicked = true;
                    }
                }
            }

            return interaction;
        }
    }
}
