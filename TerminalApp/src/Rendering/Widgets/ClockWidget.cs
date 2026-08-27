using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;
using SSNoir.Core;

namespace SSNoir.Rendering
{
    // 时钟是当前地点的状态，不是可点击的行动卡。这里用紧凑的状态条表现，避免和行动网格混淆。
    public static class ClockWidget
    {
        private const float Margin = 40f;
        private const float LabelColumnWidth = 105f;
        private const float ClockGap = 12f;
        private const float RowGap = 10f;
        private const float MinPanelWidth = 170f;
        private const float MaxPanelWidth = 260f;
        private const float NoteMaxWidth = 220f;
        private const int NoteFontSize = 12;
        private const float NoteLineHeight = 16f;
        private const int NoteMaxLines = 3;

        public static float Draw(RendererState state, float y, float windowWidth)
        {
            var clocksToShow = new List<GameClock>();
            if (state.NavigationStack.Count > 0)
            {
                var currentNode = state.NavigationStack[state.NavigationStack.Count - 1];
                if (currentNode.Clocks != null)
                    clocksToShow.AddRange(currentNode.Clocks);
            }
            else if (state.DisplayedSnapshot.RootNode != null)
            {
                clocksToShow.AddRange(state.DisplayedSnapshot.RootNode.Clocks);
            }

            FontManager.DrawText("当前节点状态", Margin, y + 8f, 14, TerminalPalette.TextMuted);
            float contentX = Margin + LabelColumnWidth;
            float rightEdge = windowWidth - Margin;

            if (clocksToShow.Count == 0)
            {
                FontManager.DrawText("—", contentX, y + 8f, 16, TerminalPalette.TextMuted);
                float dividerY = y + 36f;
                DrawDivider(dividerY, windowWidth);
                return dividerY + 15f;
            }

            float x = contentX;
            float rowY = y;
            float rowHeight = 0f;
            foreach (var clock in clocksToShow)
            {
                var layout = MeasureClock(clock, rightEdge - contentX);
                if (x > contentX && x + layout.Width > rightEdge)
                {
                    rowY += rowHeight + RowGap;
                    x = contentX;
                    rowHeight = 0f;
                }

                DrawClockPanel(new Rectangle(x, rowY, layout.Width, layout.Height), clock, layout.NoteLines);
                x += layout.Width + ClockGap;
                rowHeight = System.Math.Max(rowHeight, layout.Height);
            }

            float divider = rowY + rowHeight + 8f;
            DrawDivider(divider, windowWidth);
            return divider + 15f;
        }

        private readonly struct ClockLayout
        {
            public readonly float Width;
            public readonly float Height;
            public readonly List<string> NoteLines;

            public ClockLayout(float width, float height, List<string> noteLines)
            {
                Width = width;
                Height = height;
                NoteLines = noteLines;
            }
        }

        private static ClockLayout MeasureClock(GameClock clock, float availableWidth)
        {
            string progress = ProgressText(clock);
            float headerWidth = FontManager.MeasureTextWidth(clock.Label, 14)
                + FontManager.MeasureTextWidth(progress, 14) + 34f;
            float noteWrapWidth = System.Math.Min(NoteMaxWidth, System.Math.Max(MinPanelWidth - 24f, availableWidth - 24f));
            var noteLines = string.IsNullOrWhiteSpace(clock.Note)
                ? new List<string>()
                : ClampWrappedLines(WrapTextLines(clock.Note, noteWrapWidth, NoteFontSize), NoteMaxLines, noteWrapWidth, NoteFontSize);

            float noteWidth = 0f;
            foreach (string line in noteLines)
                noteWidth = System.Math.Max(noteWidth, FontManager.MeasureTextWidth(line, NoteFontSize));

            float desiredWidth = System.Math.Max(headerWidth, noteWidth + 24f);
            float maxWidth = System.Math.Max(MinPanelWidth, System.Math.Min(MaxPanelWidth, availableWidth));
            float width = System.Math.Min(System.Math.Max(MinPanelWidth, desiredWidth), maxWidth);
            float height = 50f + noteLines.Count * NoteLineHeight;
            return new ClockLayout(width, height, noteLines);
        }

        private static void DrawClockPanel(Rectangle bounds, GameClock clock, List<string> noteLines)
        {
            Raylib.DrawRectangleRec(bounds, TerminalPalette.InfoSurface);
            Raylib.DrawRectangle((int)bounds.X, (int)bounds.Y + 1, 3, (int)bounds.Height - 2, TerminalPalette.AccentBright);
            Raylib.DrawLineEx(new Vector2(bounds.X, bounds.Y), new Vector2(bounds.X + bounds.Width, bounds.Y), 1f, TerminalPalette.InfoBorder);
            Raylib.DrawLineEx(new Vector2(bounds.X, bounds.Y + bounds.Height), new Vector2(bounds.X + bounds.Width, bounds.Y + bounds.Height), 1f, TerminalPalette.InfoBorder);

            const float padding = 12f;
            string title = FitTextWithEllipsis(clock.Label, bounds.Width - 90f, 14);
            FontManager.DrawText(title, bounds.X + padding, bounds.Y + 8f, 14, TerminalPalette.InfoText);

            string progress = ProgressText(clock);
            int progressWidth = FontManager.MeasureTextWidth(progress, 14);
            FontManager.DrawText(progress, bounds.X + bounds.Width - padding - progressWidth, bounds.Y + 8f, 14, TerminalPalette.InfoText);

            DrawProgressTrack(new Rectangle(bounds.X + padding, bounds.Y + 30f, bounds.Width - padding * 2f, 8f), clock);
            for (int i = 0; i < noteLines.Count; i++)
                FontManager.DrawText(noteLines[i], bounds.X + padding, bounds.Y + 46f + i * NoteLineHeight, NoteFontSize, TerminalPalette.InfoBody);
        }

        public static void DrawProgressTrack(Rectangle rect, GameClock clock)
        {
            Raylib.DrawRectangleRounded(rect, 0.35f, 4, TerminalPalette.InfoTrack);
            if (clock.Max <= 0 || clock.Current <= 0)
                return;

            float fraction = System.Math.Clamp((float)clock.Current / clock.Max, 0f, 1f);
            if (clock.Style == ClockStyle.Gauge)
            {
                float gap = 2f;
                float segmentWidth = (rect.Width - gap * System.Math.Max(0, clock.Max - 1)) / clock.Max;
                for (int i = 0; i < clock.Max; i++)
                {
                    var segment = new Rectangle(rect.X + i * (segmentWidth + gap), rect.Y, segmentWidth, rect.Height);
                    Raylib.DrawRectangleRounded(segment, 0.3f, 3,
                        i < clock.Current ? TerminalPalette.AccentBright : TerminalPalette.InfoTrack);
                }
                return;
            }

            Raylib.DrawRectangleRounded(new Rectangle(rect.X, rect.Y, rect.Width * fraction, rect.Height),
                0.35f, 4, TerminalPalette.AccentBright);
        }

        public static string ProgressText(GameClock clock) => $"{clock.Current}/{clock.Max}";

        private static void DrawDivider(float y, float windowWidth) =>
            Raylib.DrawLineEx(new Vector2(Margin, y), new Vector2(windowWidth - Margin, y), 1f, TerminalPalette.Border);

        private static List<string> WrapTextLines(string text, float width, int fontSize)
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
            return lines;
        }

        private static List<string> ClampWrappedLines(List<string> lines, int maxLines, float width, int fontSize)
        {
            if (lines.Count <= maxLines)
                return lines;

            var visible = lines.GetRange(0, maxLines);
            string last = visible[maxLines - 1];
            while (last.Length > 0 && FontManager.MeasureTextWidth(last + "…", fontSize) > width)
                last = last[..^1];
            visible[maxLines - 1] = last + "…";
            return visible;
        }

        private static string FitTextWithEllipsis(string text, float maxWidth, int fontSize)
        {
            if (FontManager.MeasureTextWidth(text, fontSize) <= maxWidth)
                return text;
            const string ellipsis = "…";
            while (text.Length > 0 && FontManager.MeasureTextWidth(text + ellipsis, fontSize) > maxWidth)
                text = text[..^1];
            return text + ellipsis;
        }
    }
}
