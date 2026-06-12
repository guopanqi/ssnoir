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
            public bool RestClicked;
            public SelectedResource? SelectedResourceToSet;
            public bool ShouldClearSelection;
        }

        public static HandPanelInteraction Draw(RendererState state, GameState gameState, System.Numerics.Vector2 mousePos, float windowWidth, float windowHeight)
        {
            var interaction = new HandPanelInteraction
            {
                RestClicked = false,
                SelectedResourceToSet = null,
                ShouldClearSelection = false
            };

            float handY = windowHeight - 100;

            // Draw Hand Panel (height 75)
            Raylib.DrawRectangle(0, (int)handY, (int)windowWidth, 75, new Color(18, 18, 24, 255));
            Raylib.DrawLineEx(new System.Numerics.Vector2(0, handY), new System.Numerics.Vector2(windowWidth, handY), 1.5f, new Color(40, 40, 50, 255));

            Color labelColor = new Color(150, 150, 170, 255);

            // ── Draw Action Dice in Hand ──
            FontManager.DrawText("手牌骰子: ", 30, handY + 28, 14, labelColor);
            var dice = gameState.Get<List<object>>("action-dice");
            if (dice != null)
            {
                for (int i = 0; i < dice.Count; i++)
                {
                    float dieX = 110 + i * 42;
                    float dieY = handY + 18;
                    var dieRect = new Rectangle(dieX, dieY, 32, 32);

                    bool isSlotted = state.IsDieSlotted(i);
                    bool hover = !isSlotted && Raylib.CheckCollisionPointRec(mousePos, dieRect);

                    if (isSlotted)
                    {
                        Raylib.DrawRectangleRounded(dieRect, 0.2f, 4, new Color(30, 30, 35, 120));
                        Raylib.DrawRectangleRoundedLinesEx(dieRect, 0.2f, 4, 1f, new Color(40, 40, 45, 120));

                        string numStr = dice[i]?.ToString() ?? "0";
                        int numW = FontManager.MeasureTextWidth(numStr, 14);
                        FontManager.DrawText(numStr, dieX + (32 - numW) / 2f, dieY + 8, 14, new Color(80, 80, 90, 120));
                    }
                    else
                    {
                        Color bg = hover ? new Color(70, 70, 100, 255) : new Color(45, 45, 60, 255);
                        Color border = hover ? new Color(150, 150, 250, 255) : new Color(90, 90, 110, 255);

                        Raylib.DrawRectangleRounded(dieRect, 0.2f, 4, bg);
                        Raylib.DrawRectangleRoundedLinesEx(dieRect, 0.2f, 4, 1.5f, border);

                        string numStr = dice[i]?.ToString() ?? "0";
                        int numW = FontManager.MeasureTextWidth(numStr, 14);
                        FontManager.DrawText(numStr, dieX + (32 - numW) / 2f, dieY + 8, 14, Color.White);

                        if (hover && Raylib.IsMouseButtonPressed(MouseButton.Left))
                        {
                            int val = 0;
                            if (dice[i] is double d) val = (int)d;
                            else if (dice[i] is long l) val = (int)l;
                            else if (dice[i] is int valInt) val = valInt;

                            interaction.SelectedResourceToSet = new SelectedResource
                            {
                                Type = "die",
                                Value = val,
                                SourceIndex = i
                            };
                        }
                    }
                }
            }

            // ── Draw Items in Hand (plus money) ──
            float itemsStartX = 360f;
            FontManager.DrawText("手牌物品: ", itemsStartX, handY + 28, 14, labelColor);

            var items = new List<(string Name, int Qty)>();
            int money = gameState.Get<int>("money");
            if (money > 0)
            {
                items.Add(("金钱", money));
            }

            foreach (var kvp in gameState.GetAllStates())
            {
                if (kvp.Key.StartsWith("item:"))
                {
                    string itemName = kvp.Key.Substring(5);
                    int qty = 0;
                    if (kvp.Value is double d) qty = (int)d;
                    else if (kvp.Value is long l) qty = (int)l;
                    else if (kvp.Value is int valInt) qty = valInt;

                    if (qty > 0)
                    {
                        items.Add((itemName, qty));
                    }
                }
            }

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                float itemX = itemsStartX + 75 + i * 75;
                float itemY = handY + 18;
                var itemRect = new Rectangle(itemX, itemY, 68, 32);

                int remaining = state.GetRemainingItemQty(gameState, item.Name);
                bool hover = (remaining > 0) && Raylib.CheckCollisionPointRec(mousePos, itemRect);

                if (remaining <= 0)
                {
                    Raylib.DrawRectangleRounded(itemRect, 0.2f, 4, new Color(30, 30, 35, 120));
                    Raylib.DrawRectangleRoundedLinesEx(itemRect, 0.2f, 4, 1f, new Color(40, 40, 45, 120));

                    string label = item.Name == "金钱" ? "$0" : $"{item.Name} x0";
                    int lblW = FontManager.MeasureTextWidth(label, 12);
                    FontManager.DrawText(label, itemX + (68 - lblW) / 2f, itemY + 8, 12, new Color(80, 80, 90, 120));
                }
                else
                {
                    Color bg = hover ? new Color(70, 70, 100, 255) : new Color(45, 45, 60, 255);
                    Color border = hover ? new Color(150, 150, 250, 255) : new Color(90, 90, 110, 255);

                    Raylib.DrawRectangleRounded(itemRect, 0.2f, 4, bg);
                    Raylib.DrawRectangleRoundedLinesEx(itemRect, 0.2f, 4, 1.5f, border);

                    string label = item.Name == "金钱" ? $"${remaining}" : $"{item.Name} x{remaining}";
                    int lblW = FontManager.MeasureTextWidth(label, 12);
                    FontManager.DrawText(label, itemX + (68 - lblW) / 2f, itemY + 8, 12, Color.White);

                    if (hover && Raylib.IsMouseButtonPressed(MouseButton.Left))
                    {
                        interaction.SelectedResourceToSet = new SelectedResource
                        {
                            Type = "item",
                            ItemName = item.Name
                        };
                    }
                }
            }

            // ── Draw Rest / End Turn Button ──
            float restX = windowWidth - 110;
            float restY = handY + 18;
            var restRect = new Rectangle(restX, restY, 80, 32);
            bool restHover = Raylib.CheckCollisionPointRec(mousePos, restRect);

            Color restBg = restHover ? new Color(120, 50, 50, 255) : new Color(85, 30, 30, 255);
            Color restBorder = restHover ? new Color(220, 100, 100, 255) : new Color(140, 60, 60, 255);

            Raylib.DrawRectangleRounded(restRect, 0.2f, 4, restBg);
            Raylib.DrawRectangleRoundedLinesEx(restRect, 0.2f, 4, 1.5f, restBorder);

            string restText = "休息";
            int restW = FontManager.MeasureTextWidth(restText, 14);
            FontManager.DrawText(restText, restX + (80 - restW) / 2f, restY + 9, 14, Color.White);

            if (restHover && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                interaction.RestClicked = true;
            }

            // Right click anywhere on the hand panel to clear selection
            var panelRect = new Rectangle(0, handY, windowWidth, 75);
            if (Raylib.CheckCollisionPointRec(mousePos, panelRect) && Raylib.IsMouseButtonPressed(MouseButton.Right))
            {
                interaction.ShouldClearSelection = true;
            }

            return interaction;
        }
    }
}
