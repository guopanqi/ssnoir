using System;
using System.Collections.Generic;
using Raylib_cs;
using SSNoir.Core;

namespace SSNoir.Rendering
{
    public static class CardWidget
    {
        public static bool DrawCard(Rectangle bounds, string name, string typeLabel, bool isHovered, List<GameClock> clocks)
        {
            Color bgColor = isHovered ? new Color(50, 50, 70, 255) : new Color(30, 30, 40, 255);
            Color outlineColor = isHovered ? new Color(130, 130, 220, 255) : new Color(60, 60, 80, 255);
            Color titleColor = isHovered ? Color.White : new Color(200, 200, 200, 255);
            Color typeColor = isHovered ? new Color(150, 150, 230, 255) : new Color(110, 110, 130, 255);

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

            // Draw Title text (centered)
            int titleFontSize = 20;
            int titleWidth = FontManager.MeasureTextWidth(name, titleFontSize);
            float titleX = bounds.X + (bounds.Width - titleWidth) / 2f;
            float titleY = bounds.Y + (bounds.Height / 2f) - 15;
            FontManager.DrawText(name, titleX, titleY, titleFontSize, titleColor);

            // Draw Type text (bottom-center)
            int typeFontSize = 15;
            int typeWidth = FontManager.MeasureTextWidth(typeLabel, typeFontSize);
            float typeX = bounds.X + (bounds.Width - typeWidth) / 2f;
            float typeY = bounds.Y + bounds.Height - 22;
            FontManager.DrawText(typeLabel, typeX, typeY, typeFontSize, typeColor);

            // Return if clicked
            return isHovered && Raylib.IsMouseButtonPressed(MouseButton.Left);
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
