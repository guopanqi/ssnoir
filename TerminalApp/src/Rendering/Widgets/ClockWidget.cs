using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;
using SSNoir.Core;

namespace SSNoir.Rendering
{
    public static class ClockWidget
    {
        private const float Margin = 40f;
        private const float LabelColumnWidth = 105f;
        private const float ClockGap = 28f;
        private const float NoteMaxWidth = 200f;
        private const int NoteFontSize = 12;
        private const float NoteLineHeight = 15f;
        private const int NoteMaxLines = 3;

        public static float Draw(RendererState state, float y, float windowWidth)
        {
            var clocksToShow = new List<GameClock>();
            if (state.NavigationStack.Count > 0)
            {
                var currentNode = state.NavigationStack[state.NavigationStack.Count - 1];
                if (currentNode.Clocks != null)
                {
                    clocksToShow.AddRange(currentNode.Clocks);
                }
            }
            else if (state.DisplayedSnapshot.RootNode != null)
            {
                clocksToShow.AddRange(state.DisplayedSnapshot.RootNode.Clocks);
            }

            if (clocksToShow.Count == 0)
            {
                FontManager.DrawText("当前节点状态: ", Margin, y, 14, new Color(150, 150, 170, 255));
                FontManager.DrawText("—", 145f, y, 14, new Color(90, 94, 110, 255));
                Raylib.DrawLineEx(new Vector2(Margin, y + 43f),
                    new Vector2(windowWidth - Margin, y + 43f),
                    1f, new Color(50, 50, 60, 255));
                return y + 58f;
            }

            FontManager.DrawText("当前节点状态: ", Margin, y, 14, new Color(150, 150, 170, 255));

            float x = Margin + LabelColumnWidth;
            float rowHeight = 0f;

            foreach (var clock in clocksToShow)
            {
                var layout = MeasureClock(clock, NoteMaxWidth);
                DrawDetailedClock(x, y, clock, layout);
                x += layout.Width + ClockGap;
                if (layout.Height > rowHeight)
                {
                    rowHeight = layout.Height;
                }
            }

            float dividerY = y + rowHeight + 6f;
            Raylib.DrawLineEx(new Vector2(Margin, dividerY), new Vector2(windowWidth - Margin, dividerY), 1.0f, new Color(50, 50, 60, 255));

            return dividerY + 15f;
        }

        private struct ClockLayout
        {
            public float Width;
            public float Height;
            public List<string> NoteLines;
        }

        private static ClockLayout MeasureClock(GameClock clock, float noteWrapWidth)
        {
            int labelWidth = FontManager.MeasureTextWidth(clock.Label, 16);
            float contentWidth = labelWidth + 8;

            if (clock.Style == ClockStyle.Pie)
            {
                float radius = 10f;
                string frac = $"{clock.Current}/{clock.Max}";
                int fracW = FontManager.MeasureTextWidth(frac, 14);
                contentWidth += radius * 2 + 6 + fracW;
            }
            else if (clock.Style == ClockStyle.Countdown)
            {
                float boxW = 22;
                string maxStr = $"/{clock.Max}";
                int maxW = FontManager.MeasureTextWidth(maxStr, 14);
                contentWidth += boxW + 4 + maxW;
            }
            else
            {
                contentWidth += clock.Max * 14;
            }

            var noteLines = new List<string>();
            float noteWidth = 0f;
            if (!string.IsNullOrEmpty(clock.Note))
            {
                float wrapWidth = System.Math.Max(contentWidth, noteWrapWidth);
                noteLines = WrapTextLines(clock.Note, wrapWidth, NoteFontSize);
                noteLines = ClampWrappedLines(noteLines, NoteMaxLines, wrapWidth, NoteFontSize);
                foreach (var line in noteLines)
                {
                    noteWidth = System.Math.Max(noteWidth, FontManager.MeasureTextWidth(line, NoteFontSize));
                }
            }

            float width = System.Math.Max(contentWidth, noteWidth);
            float height = noteLines.Count > 0 ? 22f + noteLines.Count * NoteLineHeight : 37f;

            return new ClockLayout { Width = width, Height = height, NoteLines = noteLines };
        }

        private static void DrawDetailedClock(float x, float y, GameClock clock, ClockLayout layout)
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
                var center = new Vector2(contentX + radius, y + 8);

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
            }

            for (int i = 0; i < layout.NoteLines.Count; i++)
            {
                FontManager.DrawText(layout.NoteLines[i], x, y + 22f + i * NoteLineHeight, NoteFontSize, new Color(135, 140, 160, 255));
            }
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

            var visible = lines.GetRange(0, maxLines);
            string last = visible[maxLines - 1];
            while (last.Length > 0 && FontManager.MeasureTextWidth(last + "…", fontSize) > width)
            {
                last = last.Substring(0, last.Length - 1);
            }
            visible[maxLines - 1] = last + "…";
            return visible;
        }
    }
}
