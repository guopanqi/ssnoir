using System;
using System.Collections.Generic;
using Raylib_cs;
using SSNoir.Core;

namespace SSNoir.Rendering
{
    public static class HandPanelWidget
    {
        public struct HandPanelInteraction
        {
            public bool TurnClicked;
            public SelectedResource? SelectedResourceToSet;
            public bool ShouldClearSelection;
        }

        public static HandPanelInteraction Draw(RendererState state, SSNoir.TerminalApp.Rendering.UiInteractionContext ui, float windowWidth, float windowHeight, bool isInEncounter)
        {
            var snapshot = state.DisplayedSnapshot;
            var interaction = new HandPanelInteraction
            {
                TurnClicked = false,
                SelectedResourceToSet = null,
                ShouldClearSelection = false
            };

            float handY = windowHeight - 100;

            // Draw Hand Panel (height 75)
            Raylib.DrawRectangle(0, (int)handY, (int)windowWidth, 75, new Color(18, 18, 24, 255));
            Raylib.DrawLineEx(new System.Numerics.Vector2(0, handY), new System.Numerics.Vector2(windowWidth, handY), 1.5f, new Color(40, 40, 50, 255));

            Color labelColor = new Color(150, 150, 170, 255);

            // ── Draw Action Dice Grouped by Actor ──
            int flatDieIdx = 0;
            float actorAreaWidth = 110f;
            float startX = 20f;

            for (int aIdx = 0; aIdx < snapshot.Actors.Count; aIdx++)
            {
                var actor = snapshot.Actors[aIdx];
                float actorX = startX + aIdx * (actorAreaWidth + 10);
                
                // Draw Actor name and stress at the bottom: e.g. "主角 0/6"
                string subtitle = $"{actor.Name} {actor.Stress}/6";
                Color textColor = new Color(180, 180, 200, 255);
                if (actor.Status == "away")
                {
                    subtitle += " [离开]";
                    textColor = new Color(100, 100, 100, 255);
                }
                else if (actor.Stress >= 5)
                {
                    textColor = new Color(250, 100, 100, 255);
                }
                
                int subW = FontManager.MeasureTextWidth(subtitle, 11);
                FontManager.DrawText(subtitle, actorX + (actorAreaWidth - subW) / 2f, handY + 52, 11, textColor);

                // Draw their Action Dice at the top
                if (actor.Status == "away")
                {
                    string awayText = "休息中";
                    int awayW = FontManager.MeasureTextWidth(awayText, 12);
                    FontManager.DrawText(awayText, actorX + (actorAreaWidth - awayW) / 2f, handY + 22, 12, new Color(100, 100, 100, 255));
                }
                else
                {
                    int diceCount = actor.ActionDice.Count;
                    float diceStartX = actorX + (actorAreaWidth - (diceCount * 44 + (diceCount - 1) * 6)) / 2f;
                    
                    for (int d = 0; d < diceCount; d++)
                    {
                        int dieVal = actor.ActionDice[d];
                        int currentFlatIdx = flatDieIdx++;

                        float dieX = diceStartX + d * 50;
                        float dieY = handY + 12;
                        var dieRect = new Rectangle(dieX, dieY, 44, 32);

                        bool isSlotted = state.IsDieSlotted(currentFlatIdx);
                        bool hover = !isSlotted && ui.CanHover(dieRect);

                        Color bg, border;
                        if (actor.Id == "player")
                        {
                            bg = hover ? new Color(50, 90, 130, 255) : new Color(30, 60, 90, 255);
                            border = hover ? Color.White : new Color(80, 150, 220, 255);
                        }
                        else if (actor.Id == "anna")
                        {
                            bg = hover ? new Color(100, 50, 120, 255) : new Color(70, 30, 80, 255);
                            border = hover ? Color.White : new Color(180, 80, 200, 255);
                        }
                        else if (actor.Id == "laozhou")
                        {
                            bg = hover ? new Color(120, 60, 50, 255) : new Color(80, 40, 30, 255);
                            border = hover ? Color.White : new Color(220, 100, 80, 255);
                        }
                        else
                        {
                            bg = hover ? new Color(70, 70, 100, 255) : new Color(45, 45, 60, 255);
                            border = hover ? Color.White : new Color(90, 90, 110, 255);
                        }

                        string text = dieVal.ToString();

                        if (isSlotted)
                        {
                            Raylib.DrawRectangleRounded(dieRect, 0.2f, 4, new Color(30, 30, 35, 120));
                            Raylib.DrawRectangleRoundedLinesEx(dieRect, 0.2f, 4, 1f, new Color(40, 40, 45, 120));

                            int numW = FontManager.MeasureTextWidth(text, 14);
                            FontManager.DrawText(text, dieX + (44 - numW) / 2f, dieY + 8, 14, new Color(80, 80, 90, 120));
                        }
                        else
                        {
                            Raylib.DrawRectangleRounded(dieRect, 0.2f, 4, bg);
                            Raylib.DrawRectangleRoundedLinesEx(dieRect, 0.2f, 4, 1.5f, border);

                            int numW = FontManager.MeasureTextWidth(text, 14);
                            FontManager.DrawText(text, dieX + (44 - numW) / 2f, dieY + 8, 14, Color.White);

                            if (!isSlotted && ui.WasClicked(dieRect))
                            {
                                interaction.SelectedResourceToSet = new SelectedResource
                                {
                                    Type = "die",
                                    Value = dieVal,
                                    SourceIndex = currentFlatIdx,
                                    ActorId = actor.Id,
                                    DieIndex = d
                                };
                            }
                        }
                    }
                }
            }

            // ── Draw Items in Hand (plus money) ──
            float itemsStartX = 370f;
            FontManager.DrawText("手牌物品: ", itemsStartX, handY + 28, 14, labelColor);

            var items = state.GetInventoryItems().ToList();

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                float itemX = itemsStartX + 70 + i * 62;
                float itemY = handY + 18;
                var itemRect = new Rectangle(itemX, itemY, 58, 32);

                int remaining = state.GetRemainingItemQty(item.Name);
                bool hover = (remaining > 0) && ui.CanHover(itemRect);

                if (remaining <= 0)
                {
                    Raylib.DrawRectangleRounded(itemRect, 0.2f, 4, new Color(30, 30, 35, 120));
                    Raylib.DrawRectangleRoundedLinesEx(itemRect, 0.2f, 4, 1f, new Color(40, 40, 45, 120));

                    string label = item.Name == "金钱" ? "$0" : $"{item.Name} x0";
                    int lblW = FontManager.MeasureTextWidth(label, 11);
                    FontManager.DrawText(label, itemX + (58 - lblW) / 2f, itemY + 9, 11, new Color(80, 80, 90, 120));
                }
                else
                {
                    Color bg = hover ? new Color(70, 70, 100, 255) : new Color(45, 45, 60, 255);
                    Color border = hover ? new Color(150, 150, 250, 255) : new Color(90, 90, 110, 255);

                    Raylib.DrawRectangleRounded(itemRect, 0.2f, 4, bg);
                    Raylib.DrawRectangleRoundedLinesEx(itemRect, 0.2f, 4, 1.5f, border);

                    string label = item.Name == "金钱" ? $"${remaining}" : $"{item.Name} x{remaining}";
                    int lblW = FontManager.MeasureTextWidth(label, 11);
                    FontManager.DrawText(label, itemX + (58 - lblW) / 2f, itemY + 9, 11, Color.White);

                    if (remaining > 0 && ui.WasClicked(itemRect))
                    {
                        interaction.SelectedResourceToSet = new SelectedResource
                        {
                            Type = "item",
                            ItemName = item.Name
                        };
                    }
                }
            }


            // ── Draw Turn Button ──
            float turnX = windowWidth - 110;
            float turnY = handY + 18;
            var turnRect = new Rectangle(turnX, turnY, 80, 32);

            string turnText = isInEncounter ? "回合" : "回家";
            var turnBtn = SSNoir.TerminalApp.Rendering.UiButton.Draw(turnRect, turnText, ui, true, 14,
                new Color((byte)85, (byte)30, (byte)30, (byte)255), new Color((byte)120, (byte)50, (byte)50, (byte)255), null,
                new Color((byte)140, (byte)60, (byte)60, (byte)255), new Color((byte)220, (byte)100, (byte)100, (byte)255), null,
                Color.White, null);

            if (turnBtn.Clicked)
            {
                interaction.TurnClicked = true;
            }

            // Right click anywhere on the hand panel to clear selection
            var panelRect = new Rectangle(0, handY, windowWidth, 75);
            if (!ui.IsLocked && Raylib.CheckCollisionPointRec(ui.Mouse, panelRect) && Raylib.IsMouseButtonPressed(MouseButton.Right))
            {
                interaction.ShouldClearSelection = true;
            }

            return interaction;
        }
    }
}
