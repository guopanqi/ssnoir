#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Raylib_cs;
using SSNoir.Core;

namespace SSNoir.Rendering
{
    // Unity 手牌 HUD 的横屏版本：人物与物品仍是两个语义簇，但由一个底栏统一承载。
    public static class HandPanelWidget
    {
        public const float PanelHeight = 118f;
        private const float Pad = 18f;
        private const float TokenSize = 38f;
        private const float TokenGap = 6f;

        private static readonly Color PanelBg = new Color(15, 18, 26, 248);
        private static readonly Color BlockBg = new Color(8, 10, 15, 255);
        private static readonly Color Paper = new Color(224, 224, 216, 255);
        private static readonly Color PaperDim = new Color(150, 154, 170, 255);
        private static readonly Color Gold = new Color(210, 174, 86, 255);
        private static readonly Color SealRed = new Color(186, 66, 62, 255);

        public struct HandPanelInteraction
        {
            public bool TurnClicked;
            public SelectedResource? SelectedResourceToSet;
            public bool ShouldClearSelection;
        }

        public static HandPanelInteraction Draw(RendererState state,
            SSNoir.TerminalApp.Rendering.UiInteractionContext ui,
            float windowWidth, float windowHeight, bool isInEncounter)
        {
            var interaction = new HandPanelInteraction();
            var snapshot = state.DisplayedSnapshot;
            float y = windowHeight - PanelHeight;
            var panel = new Rectangle(0f, y, windowWidth, PanelHeight);

            Raylib.DrawRectangleRec(panel, PanelBg);
            Raylib.DrawLineEx(new System.Numerics.Vector2(0f, y),
                new System.Numerics.Vector2(windowWidth, y), 2f, new Color(55, 60, 78, 255));

            float leftW = Math.Clamp(windowWidth * 0.56f, 520f, 820f);
            float dividerX = leftW;
            Raylib.DrawLineEx(new System.Numerics.Vector2(dividerX, y + 14f),
                new System.Numerics.Vector2(dividerX, windowHeight - 14f), 1f, new Color(58, 62, 78, 180));

            DrawCharacters(state, ui, new Rectangle(Pad, y + 12f, leftW - Pad * 2f, PanelHeight - 24f), ref interaction);
            DrawItemsAndFunction(state, ui,
                new Rectangle(dividerX + Pad, y + 12f, windowWidth - dividerX - Pad * 2f, PanelHeight - 24f),
                isInEncounter, ref interaction);

            if (!ui.IsLocked && Raylib.CheckCollisionPointRec(ui.Mouse, panel)
                && Raylib.IsMouseButtonPressed(MouseButton.Right))
                interaction.ShouldClearSelection = true;
            return interaction;
        }

        private static void DrawCharacters(RendererState state,
            SSNoir.TerminalApp.Rendering.UiInteractionContext ui, Rectangle area,
            ref HandPanelInteraction interaction)
        {
            var snapshot = state.DisplayedSnapshot;
            FontManager.DrawText("人物 / 行动", area.X, area.Y, 13, PaperDim);

            const float vitalsW = 190f;
            DrawVital(area.X, area.Y + 27f, 184f, "健康", snapshot.Health, snapshot.MaxHealth, 0.75f, 0.40f);
            DrawVital(area.X, area.Y + 49f, 184f, "饱腹", snapshot.Satiety, snapshot.MaxSatiety, 0.65f, 0.30f);
            Raylib.DrawLineEx(new System.Numerics.Vector2(area.X + vitalsW, area.Y + 22f),
                new System.Numerics.Vector2(area.X + vitalsW, area.Y + area.Height - 4f),
                1f, new Color(58, 62, 78, 150));

            float x = area.X + vitalsW + 18f;
            int flatDie = 0;
            foreach (var actor in snapshot.Actors)
            {
                if (actor.Status == "away")
                {
                    flatDie += actor.ActionDice.Count;
                    continue;
                }

                float diceW = actor.ActionDice.Count == 0
                    ? 0f : actor.ActionDice.Count * TokenSize + Math.Max(0, actor.ActionDice.Count - 1) * TokenGap;
                float clusterW = Math.Max(120f, diceW);

                float stressY = area.Y + 20f;
                string stressState = actor.Stress >= TeamState.MaxStress
                    ? "压力 · 濒临崩溃 −1"
                    : actor.Stress >= TeamState.StressPenaltyThreshold
                        ? "压力 · 心绪不宁 −1"
                        : "压力 · 平稳";
                Color stressTextColor = actor.Stress >= TeamState.StressPenaltyThreshold ? SealRed : PaperDim;
                FontManager.DrawText(stressState, x, stressY, 9, stressTextColor);
                for (int s = 0; s < TeamState.MaxStress; s++)
                {
                    var dot = new Rectangle(x + s * 13f, stressY + 15f, 9f, 9f);
                    Raylib.DrawRectangleRec(dot, s < actor.Stress ? SealRed : new Color(224, 224, 216, 45));
                }

                FontManager.DrawText(actor.Name, x, area.Y + 49f, 14, Paper);
                if (!string.IsNullOrWhiteSpace(actor.Role) && actor.Role != "protagonist")
                {
                    int nameW = FontManager.MeasureTextWidth(actor.Name, 14);
                    FontManager.DrawText(actor.Role, x + nameW + 7f, area.Y + 52f, 9, PaperDim);
                }

                for (int d = 0; d < actor.ActionDice.Count; d++)
                {
                    int globalIndex = flatDie + d;
                    var rect = new Rectangle(x + d * (TokenSize + TokenGap), area.Y + 67f, TokenSize, TokenSize);
                    DrawDie(state, ui, rect, actor.ActionDice[d], globalIndex, actor.Id, d, ref interaction);
                }

                x += clusterW + 24f;
                flatDie += actor.ActionDice.Count;
            }
        }

        private static void DrawVital(float x, float y, float width, string label, int current, int max,
            float highThreshold, float midThreshold)
        {
            float pct = max > 0 ? Math.Clamp(current / (float)max, 0f, 1f) : 0f;
            Color color = pct >= highThreshold ? Paper : pct >= midThreshold ? Gold : SealRed;
            FontManager.DrawText(label, x, y, 10, PaperDim);
            float barX = x + 34f;
            float barW = width - 72f;
            Raylib.DrawRectangleRec(new Rectangle(barX, y + 3f, barW, 7f), new Color(224, 224, 216, 30));
            Raylib.DrawRectangleRec(new Rectangle(barX, y + 3f, barW * pct, 7f), color);
            FontManager.DrawText($"{current}/{max}", x + width - 34f, y, 10, color);
        }

        private static void DrawDie(RendererState state, SSNoir.TerminalApp.Rendering.UiInteractionContext ui,
            Rectangle rect, int value, int globalIndex, string actorId, int dieIndex,
            ref HandPanelInteraction interaction)
        {
            bool slotted = state.IsDieSlotted(globalIndex);
            bool selected = state.SelectedResource?.Type == "die" && state.SelectedResource.SourceIndex == globalIndex;
            bool hover = !slotted && ui.CanHover(rect);
            DrawToken(rect, value.ToString(), null, selected, hover, slotted || ui.IsLocked);
            if (!slotted && !ui.IsLocked && ui.WasClicked(rect))
            {
                interaction.SelectedResourceToSet = new SelectedResource
                {
                    Type = "die", Value = value, SourceIndex = globalIndex,
                    ActorId = actorId, DieIndex = dieIndex
                };
            }
        }

        private static void DrawItemsAndFunction(RendererState state,
            SSNoir.TerminalApp.Rendering.UiInteractionContext ui, Rectangle area, bool isInEncounter,
            ref HandPanelInteraction interaction)
        {
            const float functionW = 94f;
            FontManager.DrawText("物品", area.X, area.Y, 13, PaperDim);
            float functionX = area.X + area.Width - functionW;
            FontManager.DrawText("功能", functionX, area.Y, 13, PaperDim);

            var functionRect = new Rectangle(functionX, area.Y + 24f, functionW, 64f);
            bool fnHover = ui.CanHover(functionRect);
            DrawToken(functionRect, isInEncounter ? "休息" : "回家", null, false, fnHover, ui.IsLocked);
            if (!ui.IsLocked && ui.WasClicked(functionRect)) interaction.TurnClicked = true;

            var items = state.GetInventoryItems().ToList();
            var viewport = new Rectangle(area.X, area.Y + 24f,
                Math.Max(60f, functionX - area.X - 20f), area.Height - 28f);
            DrawItems(state, ui, items, viewport, ref interaction);
        }

        private static void DrawItems(RendererState state,
            SSNoir.TerminalApp.Rendering.UiInteractionContext ui,
            List<(string Name, int Qty)> items, Rectangle viewport, ref HandPanelInteraction interaction)
        {
            const float gap = 8f;
            int cols = Math.Max(1, (int)((viewport.Width + gap) / (TokenSize + gap)));
            int rows = items.Count == 0 ? 0 : (items.Count + cols - 1) / cols;
            float contentH = rows == 0 ? 0f : rows * TokenSize + Math.Max(0, rows - 1) * gap;
            float maxScroll = Math.Max(0f, contentH - viewport.Height);
            if (ui.CanHover(viewport)) state.HandItemsScrollOffset -= Raylib.GetMouseWheelMove() * 34f;
            state.HandItemsScrollOffset = Math.Clamp(state.HandItemsScrollOffset, 0f, maxScroll);

            Raylib.BeginScissorMode((int)viewport.X, (int)viewport.Y, (int)viewport.Width, (int)viewport.Height);
            for (int i = 0; i < items.Count; i++)
            {
                int row = i / cols;
                int col = i % cols;
                var item = items[i];
                var rect = new Rectangle(viewport.X + col * (TokenSize + gap),
                    viewport.Y + row * (TokenSize + gap) - state.HandItemsScrollOffset, TokenSize, TokenSize);
                int remaining = state.GetRemainingItemQty(item.Name);
                bool selected = state.SelectedResource?.Type == "item" && state.SelectedResource.ItemName == item.Name;
                bool disabled = remaining <= 0 || ui.IsLocked;
                DrawToken(rect, ItemSymbol(item.Name), item.Name == "金钱" ? $"${remaining}" : $"x{remaining}",
                    selected, ui.CanHover(rect), disabled);
                if (!disabled && ui.WasClicked(rect))
                    interaction.SelectedResourceToSet = new SelectedResource { Type = "item", ItemName = item.Name, Qty = 1 };
            }
            Raylib.EndScissorMode();

            if (maxScroll > 0f)
            {
                var track = new Rectangle(viewport.X + viewport.Width - 4f, viewport.Y, 4f, viewport.Height);
                float thumbH = Math.Max(18f, track.Height * viewport.Height / contentH);
                float thumbY = track.Y + (track.Height - thumbH) * state.HandItemsScrollOffset / maxScroll;
                Raylib.DrawRectangleRec(track, new Color(40, 44, 58, 180));
                Raylib.DrawRectangleRec(new Rectangle(track.X, thumbY, track.Width, thumbH), new Color(110, 118, 150, 220));
            }
        }

        private static void DrawToken(Rectangle rect, string big, string? small,
            bool selected, bool hover, bool disabled)
        {
            Rectangle drawRect = selected && !disabled
                ? new Rectangle(rect.X, rect.Y - 4f, rect.Width, rect.Height) : rect;
            Color border = disabled ? new Color(120, 124, 138, 70)
                : selected ? TerminalPalette.Accent : hover ? TerminalPalette.AccentBright : new Color(190, 194, 204, 180);
            Raylib.DrawRectangleRounded(new Rectangle(drawRect.X + 2f, drawRect.Y + 2f, drawRect.Width, drawRect.Height),
                0.08f, 3, new Color(0, 0, 0, disabled ? 40 : 130));
            Raylib.DrawRectangleRounded(drawRect, 0.08f, 3,
                disabled ? new Color(8, 10, 15, 120) : BlockBg);
            Raylib.DrawRectangleRoundedLinesEx(drawRect, 0.08f, 3, selected || hover ? 2f : 1f, border);
            Color text = disabled ? new Color(224, 224, 216, 65) : selected ? TerminalPalette.AccentBright : Paper;
            int bigSize = small == null ? 14 : 15;
            int bigW = FontManager.MeasureTextWidth(big, bigSize);
            FontManager.DrawText(big, drawRect.X + (drawRect.Width - bigW) / 2f,
                drawRect.Y + (small == null ? (drawRect.Height - bigSize) / 2f : 3f), bigSize, text);
            if (small != null)
            {
                int smallW = FontManager.MeasureTextWidth(small, 9);
                FontManager.DrawText(small, drawRect.X + (drawRect.Width - smallW) / 2f, drawRect.Y + 25f, 9, text);
            }
        }

        private static string ItemSymbol(string name) => name switch
        {
            "金钱" => "$", "酒" => "酒", "药品" => "药", "食物" => "食",
            _ => name.Length > 0 ? name.Substring(0, 1) : "?"
        };
    }
}
