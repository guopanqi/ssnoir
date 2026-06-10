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
            public int ClickedSlotIndex; // -1 if none
            public bool ExecuteClicked;
        }

        public static CardInteraction DrawCard(
            Rectangle bounds,
            string name,
            string typeLabel,
            bool isHovered,
            List<GameClock> clocks,
            bool isFlipped = false,
            string backText = "",
            List<ActionCost>? requires = null,
            List<SlottedResource?>? slotted = null,
            System.Numerics.Vector2 mousePos = default,
            List<DifficultyModifierInfo>? modifiers = null)
        {
            var interaction = new CardInteraction
            {
                CardClicked = false,
                ClickedSlotIndex = -1,
                ExecuteClicked = false
            };

            Color bgColor = isHovered ? new Color(50, 50, 70, 255) : new Color(30, 30, 40, 255);
            Color outlineColor = isHovered ? new Color(130, 130, 220, 255) : new Color(60, 60, 80, 255);
            Color titleColor = isHovered ? Color.White : new Color(200, 200, 200, 255);
            Color typeColor = isHovered ? new Color(150, 150, 230, 255) : new Color(110, 110, 130, 255);

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

                interaction.CardClicked = isHovered && Raylib.IsMouseButtonPressed(MouseButton.Left);
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

            bool hasRequires = requires != null && requires.Count > 0 && slotted != null && slotted.Count == requires.Count;

            // Draw Title text (centered, adjusted upwards if card has slots)
            int titleFontSize = 20;
            int titleWidth = FontManager.MeasureTextWidth(name, titleFontSize);
            float titleX = bounds.X + (bounds.Width - titleWidth) / 2f;
            float titleY = hasRequires 
                ? bounds.Y + 12
                : bounds.Y + (bounds.Height / 2f) - 15;
            FontManager.DrawText(name, titleX, titleY, titleFontSize, titleColor);

            // Draw Type text (bottom-center or moved up if has slots)
            int typeFontSize = 14;
            int typeWidth = FontManager.MeasureTextWidth(typeLabel, typeFontSize);
            float typeX = bounds.X + (bounds.Width - typeWidth) / 2f;
            float typeY = hasRequires 
                ? bounds.Y + 32
                : bounds.Y + bounds.Height - 22;
            FontManager.DrawText(typeLabel, typeX, typeY, typeFontSize, typeColor);

            // Draw Slots & Execute Button if there are requirements
            if (hasRequires)
            {
                int M = requires!.Count;
                float slotW = 24;
                float slotH = 24;
                float spacing = 6;
                float totalWidth = M * slotW + (M - 1) * spacing;
                float slotStartX = bounds.X + (bounds.Width - totalWidth) / 2f;
                float slotY = bounds.Y + 52;

                for (int j = 0; j < M; j++)
                {
                    var slotRect = new Rectangle(slotStartX + j * (slotW + spacing), slotY, slotW, slotH);
                    bool slotHover = Raylib.CheckCollisionPointRec(mousePos, slotRect);
                    var res = slotted![j];

                    if (res == null)
                    {
                        // Draw empty dotted-like slot border
                        Raylib.DrawRectangleRoundedLinesEx(slotRect, 0.2f, 4, 1.5f, slotHover ? new Color(130, 130, 220, 255) : new Color(80, 80, 100, 255));
                        
                        // Draw placeholder letter
                        string placeholder = requires[j].Type == "die" ? "D" : requires[j].ItemName.Substring(0, 1);
                        if (requires[j].Type == "item" && requires[j].Qty > 1)
                        {
                            placeholder += requires[j].Qty.ToString();
                        }
                        int fontSize = placeholder.Length > 2 ? 8 : (placeholder.Length > 1 ? 10 : 12);
                        int pW = FontManager.MeasureTextWidth(placeholder, fontSize);
                        FontManager.DrawText(placeholder, slotRect.X + (slotW - pW) / 2f, slotRect.Y + (slotH - fontSize) / 2f, fontSize, new Color(80, 80, 100, 255));
                    }
                    else
                    {
                        // Draw filled slot
                        Raylib.DrawRectangleRounded(slotRect, 0.2f, 4, new Color(50, 50, 75, 255));
                        Raylib.DrawRectangleRoundedLinesEx(slotRect, 0.2f, 4, 1.5f, new Color(130, 130, 250, 255));

                        string valStr = res.Type == "die" ? res.Value.ToString() : res.ItemName.Substring(0, 1);
                        if (res.Type == "item" && res.Value > 1)
                        {
                            valStr += res.Value.ToString();
                        }
                        int fontSize = valStr.Length > 2 ? 8 : (valStr.Length > 1 ? 10 : 12);
                        int valW = FontManager.MeasureTextWidth(valStr, fontSize);
                        FontManager.DrawText(valStr, slotRect.X + (slotW - valW) / 2f, slotRect.Y + (slotH - fontSize) / 2f, fontSize, Color.White);
                    }

                    if (slotHover && Raylib.IsMouseButtonPressed(MouseButton.Left))
                    {
                        interaction.ClickedSlotIndex = j;
                    }
                }

                // Draw Execute Button
                float exeW = 80;
                float exeH = 18;
                float exeX = bounds.X + (bounds.Width - exeW) / 2f;
                float exeY = bounds.Y + 82;
                var exeRect = new Rectangle(exeX, exeY, exeW, exeH);

                bool allFilled = slotted != null && slotted.All(s => s != null);
                if (allFilled)
                {
                    bool exeHover = Raylib.CheckCollisionPointRec(mousePos, exeRect);
                    Raylib.DrawRectangleRounded(exeRect, 0.2f, 4, exeHover ? new Color(100, 200, 100, 255) : new Color(50, 150, 50, 255));
                    
                    string exeText = "执行";
                    int eW = FontManager.MeasureTextWidth(exeText, 12);
                    FontManager.DrawText(exeText, exeX + (exeW - eW) / 2f, exeY + 3, 12, Color.White);

                    if (exeHover && Raylib.IsMouseButtonPressed(MouseButton.Left))
                    {
                        interaction.ExecuteClicked = true;
                    }
                }
                else
                {
                    Raylib.DrawRectangleRounded(exeRect, 0.2f, 4, new Color(50, 50, 55, 255));
                    Raylib.DrawRectangleRoundedLinesEx(exeRect, 0.2f, 4, 1f, new Color(70, 70, 75, 255));

                    string exeText = "待命";
                    int eW = FontManager.MeasureTextWidth(exeText, 12);
                    FontManager.DrawText(exeText, exeX + (exeW - eW) / 2f, exeY + 3, 12, new Color(100, 100, 110, 255));
                }
            }
            else
            {
                // Simple action or container card click behavior
                interaction.CardClicked = isHovered && Raylib.IsMouseButtonPressed(MouseButton.Left);
            }

            // Draw Difficulty Modifier Tags on the top-left of the card
            if (modifiers != null && modifiers.Count > 0)
            {
                float tagStartX = bounds.X + 6;
                float tagStartY = bounds.Y + 6;
                for (int k = 0; k < modifiers.Count; k++)
                {
                    var mod = modifiers[k];
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
