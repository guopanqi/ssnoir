using System.Collections.Generic;
using Raylib_cs;
using SSNoir.Core;

namespace SSNoir.Rendering
{
    public static class ClockWidget
    {
        public static float Draw(RendererState state, float y, float windowWidth)
        {
            var clocksToShow = new List<GameClock>();
            if (state.NavigationStack.Count == 0)
            {
                if (state.DisplayedSnapshot.RootNode != null)
                    clocksToShow.AddRange(state.DisplayedSnapshot.RootNode.Clocks);
            }
            else
            {
                var currentNode = state.NavigationStack[state.NavigationStack.Count - 1];
                if (currentNode.Clocks != null)
                {
                    clocksToShow.AddRange(currentNode.Clocks);
                }
            }

            if (clocksToShow.Count == 0)
            {
                return 110f;
            }

            float x = 40f;
            FontManager.DrawText("当前节点状态: ", x, y, 14, new Color(150, 150, 170, 255));
            x += 105;

            foreach (var clock in clocksToShow)
            {
                DrawDetailedClock(ref x, y, clock);
            }

            Raylib.DrawLineEx(new System.Numerics.Vector2(40, y + 25), new System.Numerics.Vector2(windowWidth - 40, y + 25), 1.0f, new Color(50, 50, 60, 255));

            return y + 40f;
        }

        private static void DrawDetailedClock(ref float x, float y, GameClock clock)
        {
            Color textColor = new Color(200, 200, 220, 255);
            Color activeColor = new Color(130, 130, 250, 255);
            Color inactiveColor = new Color(45, 45, 55, 255);
            Color outlineColor = new Color(70, 70, 90, 255);

            FontManager.DrawText(clock.Label, x, y, 16, textColor);
            int labelWidth = FontManager.MeasureTextWidth(clock.Label, 16);
            float contentX = x + labelWidth + 8;

            if (clock.Style == ClockStyle.Pie)
            {
                float radius = 10f;
                var center = new System.Numerics.Vector2(contentX + radius, y + 8);
                
                Raylib.DrawCircleLines((int)center.X, (int)center.Y, radius, outlineColor);
                if (clock.Max > 0 && clock.Current > 0)
                {
                    float percent = (float)clock.Current / clock.Max;
                    float startAngle = -90f;
                    float endAngle = -90f + 360f * percent;
                    Raylib.DrawCircleSector(center, radius, startAngle, endAngle, 36, activeColor);
                }

                string frac = $"{clock.Current}/{clock.Max}";
                FontManager.DrawText(frac, contentX + radius * 2 + 6, y, 14, textColor);
                int fracW = FontManager.MeasureTextWidth(frac, 14);

                x += labelWidth + 8 + radius * 2 + 6 + fracW + 20;
            }
            else if (clock.Style == ClockStyle.Countdown)
            {
                float boxW = 22;
                float boxH = 18;
                var boxRect = new Rectangle(contentX, y, boxW, boxH);
                
                Raylib.DrawRectangleRounded(boxRect, 0.2f, 4, inactiveColor);
                Raylib.DrawRectangleRoundedLinesEx(boxRect, 0.2f, 4, 1f, outlineColor);

                string numStr = clock.Current.ToString();
                int numW = FontManager.MeasureTextWidth(numStr, 14);
                FontManager.DrawText(numStr, contentX + (boxW - numW) / 2f, y + 2, 14, activeColor);

                string maxStr = $"/{clock.Max}";
                FontManager.DrawText(maxStr, contentX + boxW + 4, y + 2, 14, new Color(120, 120, 140, 255));
                int maxW = FontManager.MeasureTextWidth(maxStr, 14);

                x += labelWidth + 8 + boxW + 4 + maxW + 20;
            }
            else
            {
                for (int i = 0; i < clock.Max; i++)
                {
                    var segRect = new Rectangle(contentX + i * 14, y + 2, 10, 10);
                    if (i < clock.Current)
                    {
                        Raylib.DrawRectangleRounded(segRect, 0.3f, 4, activeColor);
                    }
                    else
                    {
                        Raylib.DrawRectangleRounded(segRect, 0.3f, 4, inactiveColor);
                        Raylib.DrawRectangleRoundedLinesEx(segRect, 0.3f, 4, 1f, outlineColor);
                    }
                }

                x += labelWidth + 8 + clock.Max * 14 + 20;
            }
        }
    }
}
