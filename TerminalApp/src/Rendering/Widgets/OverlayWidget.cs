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
                float modalH = 240;
                float modalX = (windowWidth - modalW) / 2f;
                float modalY = (windowHeight - modalH) / 2f;
                var modalRect = new Rectangle(modalX, modalY, modalW, modalH);

                Raylib.DrawRectangleRounded(modalRect, 0.1f, 8, new Color(30, 30, 42, 255));
                Raylib.DrawRectangleRoundedLinesEx(modalRect, 0.1f, 8, 2f, new Color(100, 100, 130, 255));

                string title = "判定结果";
                int titleW = FontManager.MeasureTextWidth(title, 18);
                FontManager.DrawText(title, modalX + (modalW - titleW) / 2f, modalY + 20, 18, Color.White);

                float lineY = modalY + 60;
                
                string line1 = $"投入行动力骰子值: {state.ActiveRollResult.ChosenDieValue}";
                FontManager.DrawText(line1, modalX + 40, lineY, 14, new Color(200, 200, 220, 255));
                lineY += 20;

                string line2 = state.ActiveRollResult.RandomDice.Count > 0 
                    ? $"额外技能掷骰结果: {string.Join(", ", state.ActiveRollResult.RandomDice)}"
                    : "无额外技能掷骰";
                FontManager.DrawText(line2, modalX + 40, lineY, 14, new Color(200, 200, 220, 255));
                lineY += 20;

                // Difficulty modifiers summary
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
                FontManager.DrawText(lineMod, modalX + 40, lineY, 14, new Color(180, 180, 200, 255));
                lineY += 20;

                string line3 = $"最终修正判定值: {state.ActiveRollResult.ModifiedRollValue} (原始最大值 {state.ActiveRollResult.FinalRollValue})";
                FontManager.DrawText(line3, modalX + 40, lineY, 14, new Color(220, 220, 250, 255));
                lineY += 25;

                string outcomeText = state.ActiveRollResult.Outcome switch
                {
                    RollOutcome.Success => "成功",
                    RollOutcome.Neutral => "中性",
                    RollOutcome.Fail => "失败",
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
                FontManager.DrawText(outcomeStr, modalX + (modalW - outW) / 2f, lineY, 16, outcomeColor);

                float btnW = 90;
                float btnH = 32;
                float btnX = modalX + (modalW - btnW) / 2f;
                float btnY = modalY + modalH - 50;
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

            return interaction;
        }
    }
}
