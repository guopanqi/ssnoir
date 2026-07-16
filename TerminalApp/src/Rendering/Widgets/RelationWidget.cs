#nullable enable
using System;
using Raylib_cs;
using SSNoir.Core;

namespace SSNoir.Rendering
{
    // 声望不是一组孤立数字，而是一条“当前位置 → 档位 → 解锁诱饵”的进展轨道。
    public static class RelationWidget
    {
        private static readonly string[] Factions = { "官僚", "劳工", "富商" };
        // 六档配色（敌视/冷淡/中立/相识/信任/核心），正面三档逐级变亮。
        private static readonly Color[] BandColors =
        {
            new Color(186, 66, 62, 255),
            new Color(165, 120, 154, 255),
            new Color(158, 162, 178, 255),
            TerminalPalette.Accent,
            new Color(150, 176, 168, 255),
            TerminalPalette.AccentBright,
        };

        // 折叠栏背景很暗，直接用 BandColors 太暗读不清；文字单独调亮，不影响展开面板里同一套颜色的轨道/图表用途。
        private static Color Brighten(Color c, float amount)
        {
            byte Lerp(byte channel) => (byte)Math.Min(255, channel + (255 - channel) * amount);
            return new Color(Lerp(c.R), Lerp(c.G), Lerp(c.B), c.A);
        }

        // 某势力在指定档位序号上的显示名：正面三档取内容层定制称呼，其余用通用档名。
        private static string BandDisplay(PresentationSnapshot snapshot, string faction, int band)
        {
            if (band < 3) return RelationScale.BandNames[band];
            string tier = RelationScale.PositiveTiers[band - 3];
            return snapshot.RelationBandNames.TryGetValue($"{faction}:{tier}", out string? name)
                ? name : tier;
        }

        private const float CollapsedWidth = 220f;
        private const float CollapsedHeight = 28f;
        private const float ExpandedWidth = 620f;
        private const float ExpandedHeight = 392f;

        public static float Draw(RendererState state, SSNoir.TerminalApp.Rendering.UiInteractionContext ui,
            float rightEdge, float topY)
        {
            // 顶部开关始终可见；展开面板只是它的附属内容，而不是替换掉按钮。
            float x = DrawToggle(state, ui, rightEdge, topY);
            if (state.IsRelationExpanded)
            {
                DrawExpanded(state, ui, rightEdge, topY);
            }
            return x;
        }

        private static float DrawToggle(RendererState state,
            SSNoir.TerminalApp.Rendering.UiInteractionContext ui, float rightEdge, float topY)
        {
            float x = rightEdge - CollapsedWidth;
            var rect = new Rectangle(x, topY, CollapsedWidth, CollapsedHeight);
            bool hovered = ui.CanHover(rect);
            DrawPanel(rect, hovered);
            FontManager.DrawText("关系", x + 12f, topY + 8f, 11, new Color(205, 208, 222, 255));

            float itemX = x + 56f;
            float itemW = (CollapsedWidth - 64f) / Factions.Length;
            for (int i = 0; i < Factions.Length; i++)
            {
                int value = state.DisplayedSnapshot.Relations.TryGetValue(Factions[i], out int v) ? v : 0;
                int band = RelationScale.BandIndex(value);
                FontManager.DrawText($"{Factions[i]} {value}", itemX + i * itemW, topY + 7f, 11, Brighten(BandColors[band], 0.4f));
            }
            if (ui.WasClicked(rect))
            {
                state.IsRelationExpanded = !state.IsRelationExpanded;
                if (state.IsRelationExpanded)
                {
                    state.IsGrowthPanelOpen = false;
                }
            }
            return x;
        }

        private static float DrawExpanded(RendererState state,
            SSNoir.TerminalApp.Rendering.UiInteractionContext ui, float rightEdge, float topY)
        {
            float x = rightEdge - ExpandedWidth;
            float panelY = topY + 36f;
            var rect = new Rectangle(x, panelY, ExpandedWidth, ExpandedHeight);
            DrawPanel(rect, false);

            FontManager.DrawText("城市声望", x + 18f, panelY + 14f, 17, TerminalPalette.AccentBright);
            FontManager.DrawText("声望每上一档，会打开这条路线专属的营生、人脉与门路",
                x + 108f, panelY + 18f, 11, new Color(150, 155, 175, 255));

            var closeRect = new Rectangle(x + ExpandedWidth - 66f, panelY + 10f, 50f, 24f);
            bool closeHover = ui.CanHover(closeRect);
            Raylib.DrawRectangleRounded(closeRect, 0.18f, 4,
                closeHover ? new Color(75, 80, 100, 255) : new Color(45, 48, 62, 255));
            FontManager.DrawText("收起", closeRect.X + 13f, closeRect.Y + 7f, 10, new Color(205, 208, 220, 255));
            if (ui.WasClicked(closeRect)) state.IsRelationExpanded = false;

            for (int i = 0; i < Factions.Length; i++)
                DrawFactionCard(state.DisplayedSnapshot, Factions[i],
                    new Rectangle(x + 16f, panelY + 48f + i * 111f, ExpandedWidth - 32f, 101f));
            return x;
        }

        private static void DrawFactionCard(PresentationSnapshot snapshot, string faction, Rectangle rect)
        {
            int value = snapshot.Relations.TryGetValue(faction, out int v) ? v : 0;
            int band = RelationScale.BandIndex(value);
            Color active = BandColors[band];

            Raylib.DrawRectangleRounded(rect, 0.06f, 4, new Color(28, 30, 40, 245));
            Raylib.DrawRectangleRoundedLinesEx(rect, 0.06f, 4, 1f, new Color(58, 62, 80, 255));
            FontManager.DrawText(faction, rect.X + 12f, rect.Y + 10f, 15, new Color(215, 218, 230, 255));
            FontManager.DrawText($"{BandDisplay(snapshot, faction, band)}  {value}", rect.X + 68f, rect.Y + 12f, 12, active);

            float trackX = rect.X + 150f;
            float trackY = rect.Y + 20f;
            float trackW = rect.Width - 170f;
            const int pointCount = RelationScale.Max - RelationScale.Min + 1;
            const float cellGap = 1f;
            float cellW = (trackW - cellGap * (pointCount - 1)) / pointCount;
            for (int point = RelationScale.Min; point <= RelationScale.Max; point++)
            {
                int index = point - RelationScale.Min;
                int pointBand = RelationScale.BandIndex(point);
                Color bandColor = BandColors[pointBand];
                bool traversed = value >= 0 ? point >= 0 && point <= value : point <= 0 && point >= value;
                byte alpha = traversed ? (byte)155 : (byte)38;
                var cell = new Rectangle(trackX + index * (cellW + cellGap), trackY - 4f, cellW, 8f);
                Raylib.DrawRectangleRounded(cell, 0.16f, 2,
                    new Color(bandColor.R, bandColor.G, bandColor.B, alpha));
                if (point == value)
                    Raylib.DrawRectangleRoundedLinesEx(cell, 0.16f, 2, 1.5f, new Color(245, 245, 245, 230));
            }
            // 当前位置已由轨道格子的白色描边表达，无需再叠加圆点标记。

            const float chipGap = 6f;
            float chipW = (rect.Width - 24f - chipGap * 2f) / 3f;
            for (int t = 0; t < RelationScale.PositiveTiers.Length; t++)
            {
                var chip = new Rectangle(rect.X + 12f + t * (chipW + chipGap), rect.Y + 42f, chipW, 47f);
                DrawUnlock(snapshot, faction, RelationScale.PositiveTiers[t],
                    RelationScale.PositiveThresholds[t], value, chip, trackX, trackY, trackW);
            }
        }

        private static void DrawUnlock(PresentationSnapshot snapshot, string faction, string tier, int threshold,
            int value, Rectangle chip, float trackX, float trackY, float trackW)
        {
            bool unlocked = value >= threshold;
            const int pointCount = RelationScale.Max - RelationScale.Min + 1;
            const float cellGap = 1f;
            float cellW = (trackW - cellGap * (pointCount - 1)) / pointCount;
            float nodeX = trackX + (threshold - RelationScale.Min) * (cellW + cellGap) + cellW / 2f;
            Color color = unlocked ? TerminalPalette.AccentBright : TerminalPalette.TextMuted;
            Raylib.DrawCircle((int)nodeX, (int)trackY, 4f, color);
            Raylib.DrawLineEx(new System.Numerics.Vector2(nodeX, trackY + 5f),
                new System.Numerics.Vector2(chip.X + chip.Width / 2f, chip.Y), 1f,
                new Color(color.R, color.G, color.B, (byte)100));
            Raylib.DrawRectangleRounded(chip, 0.10f, 4,
                unlocked ? TerminalPalette.AccentDark : new Color(34, 36, 45, 230));
            Raylib.DrawRectangleRoundedLinesEx(chip, 0.10f, 4, 1f, new Color(color.R, color.G, color.B, (byte)170));

            string name = snapshot.RelationBandNames.TryGetValue($"{faction}:{tier}", out string? disp) ? disp : tier;
            string state = unlocked ? "已解锁" : $"还差 {Math.Max(0, threshold - value)}";
            FontManager.DrawText($"{name}  {threshold:+#;-#;0}  ·  {state}", chip.X + 8f, chip.Y + 6f, 10, color);
            string unlock = snapshot.RelationUnlocks.TryGetValue($"{faction}:{tier}", out string? text)
                ? text : "当前无新增动作";
            FontManager.DrawText(unlock, chip.X + 8f, chip.Y + 25f, 9, new Color(185, 188, 202, 255));
        }

        private static void DrawPanel(Rectangle rect, bool hovered)
        {
            Raylib.DrawRectangleRounded(rect, 0.05f, 5, TerminalPalette.Surface);
            Raylib.DrawRectangleRoundedLinesEx(rect, 0.05f, 5, 1f,
                hovered ? TerminalPalette.Accent : TerminalPalette.Border);
        }
    }
}
