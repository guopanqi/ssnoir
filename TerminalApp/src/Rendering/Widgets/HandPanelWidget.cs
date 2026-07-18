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
        public const float PanelHeight = 150f;
        private const float Pad = 18f;
        private const float TokenSize = 38f;
        private const float TokenGap = 6f;
        private const float PoolLabelW = 48f;

        private static readonly Color PanelBg = new Color(15, 18, 26, 248);
        private static readonly Color BlockBg = new Color(8, 10, 15, 255);
        private static readonly Color Paper = new Color(224, 224, 216, 255);
        private static readonly Color PaperDim = new Color(150, 154, 170, 255);
        private static readonly Color Gold = new Color(210, 174, 86, 255);
        private static readonly Color SealRed = new Color(186, 66, 62, 255);

        public struct HandPanelInteraction
        {
            public bool TurnClicked;
            public bool SmokeClicked;
            public bool DrinkClicked;
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

            DrawCharacters(state, ui, new Rectangle(Pad, y + 12f, leftW - Pad * 2f, PanelHeight - 24f),
                isInEncounter, ref interaction);
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
            bool isInEncounter, ref HandPanelInteraction interaction)
        {
            var snapshot = state.DisplayedSnapshot;
            FontManager.DrawText("人物 / 行动", area.X, area.Y, 13, PaperDim);

            const float vitalsW = 126f;
            DrawVital(area.X, area.Y + 26f, 120f, "健康", snapshot.Health, snapshot.MaxHealth, 0.75f, 0.40f);
            if (snapshot.Health <= TeamState.HealthPenaltyThreshold)
                FontManager.DrawText("健康低 · −1颗骰", area.X, area.Y + 44f, 9, SealRed);
            Raylib.DrawLineEx(new System.Numerics.Vector2(area.X + vitalsW, area.Y + 18f),
                new System.Numerics.Vector2(area.X + vitalsW, area.Y + area.Height - 2f),
                1f, new Color(58, 62, 78, 150));

            float x = area.X + vitalsW + 18f;
            int flatDie = 0;
            bool leadDrawn = false;
            foreach (var actor in snapshot.Actors)
            {
                if (actor.Status == "away")
                {
                    flatDie += actor.ActionDice.Count;
                    continue;
                }

                // 交锋里只有主角行动，同伴连骰子都不发；再挂着名字和冷静只会让人
                // 以为还能指挥他们，整簇不画。
                if (isInEncounter && actor.Role != "protagonist")
                {
                    flatDie += actor.ActionDice.Count;
                    continue;
                }

                // 主角与同伴之间一条淡分隔线，浓淡与左侧生命体征那条一致。
                if (leadDrawn)
                    Raylib.DrawLineEx(new System.Numerics.Vector2(x - 12f, area.Y + 18f),
                        new System.Numerics.Vector2(x - 12f, area.Y + area.Height - 2f),
                        1f, new Color(58, 62, 78, 150));
                leadDrawn = true;

                float diceW = actor.Role == "protagonist"
                    ? 3 * TokenSize + 2 * TokenGap
                    : (actor.ActionDice.Count == 0 ? 0f : TokenSize);
                float clusterW = Math.Max(150f, PoolLabelW + diceW);
                float poolX = x + PoolLabelW;

                // 只画名字。Role 是内部标识（protagonist / companion），不是给玩家看的职业。
                FontManager.DrawText(actor.Name, x, area.Y + 14f, 14, Paper);
                DrawDicePoolStatus(actor, x, poolX, area.Y + 80f);
                DrawComposureCells(x, poolX, area.Y + 110f, actor.Composure);

                for (int d = 0; d < actor.ActionDice.Count; d++)
                {
                    int globalIndex = flatDie + d;
                    int slotId = actor.ActionDiceSlotIds[d];
                    var rect = new Rectangle(poolX + slotId * (TokenSize + TokenGap), area.Y + 34f, TokenSize, TokenSize);
                    var activeDamage = actor.ActiveActionSlotStatuses
                        .Where(status => status.SlotId == slotId && status.DiePenalty < 0)
                        .ToList();
                    bool damaged = activeDamage.Count > 0;
                    DrawDie(state, ui, rect, actor.ActionDice[d], globalIndex, actor.Id, slotId,
                        damaged, ref interaction);
                }

                x += clusterW + 24f;
                flatDie += actor.ActionDice.Count;
            }
        }

        // 骰池位置默认留空——没有状态就什么都不画，让人一眼看出这里干净。
        // 只有当某个位置带上身体状态时，才在对应格贴一枚标签；同格多个状态纵向排开。
        private static void DrawDicePoolStatus(ActorSnapshot actor, float labelX, float poolX, float y)
        {
            FontManager.DrawText("骰池", labelX, y + 5f, 9, PaperDim);
            for (int slotId = 0; slotId < TeamState.ActionSlotCount; slotId++)
            {
                var statuses = GetSlotStatuses(actor, slotId);
                if (statuses.Count == 0)
                    continue;
                Color color = StatusColor(statuses[0]);
                var cell = new Rectangle(poolX + slotId * (TokenSize + TokenGap), y, TokenSize, 24f);
                Raylib.DrawRectangleRounded(cell, 0.3f, 3, new Color((byte)color.R, (byte)color.G, (byte)color.B, (byte)46));
                Raylib.DrawRectangleRoundedLinesEx(cell, 0.3f, 3, 1f, color);
                if (statuses.Count == 1 && statuses[0].Label == "失态")
                {
                    DrawCenteredStatusLine(cell, statuses[0].Label, cell.Y + 2f, 8, StatusColor(statuses[0]));
                    DrawCenteredStatusLine(cell, FormatPenalty(statuses[0]), cell.Y + 12f, 8, StatusColor(statuses[0]));
                    continue;
                }
                for (int i = 0; i < statuses.Count; i++)
                {
                    string label = FormatStatusLabel(statuses[i]);
                    int fontSize = FontManager.MeasureTextWidth(label, 8) <= cell.Width - 4f ? 8 : 7;
                    int labelW = FontManager.MeasureTextWidth(label, fontSize);
                    FontManager.DrawText(label, cell.X + (cell.Width - labelW) / 2f,
                        cell.Y + 3f + i * 10f, fontSize, StatusColor(statuses[i]));
                }
            }
        }

        private static void DrawCenteredStatusLine(Rectangle cell, string text, float y, int fontSize, Color color)
        {
            int textWidth = FontManager.MeasureTextWidth(text, fontSize);
            FontManager.DrawText(text, cell.X + (cell.Width - textWidth) / 2f, y, fontSize, color);
        }

        private static string FormatPenalty(ActionSlotStatus status)
        {
            return status.DiePenalty < 0
                ? $"-{Math.Abs(status.DiePenalty)}"
                : $"+{status.DiePenalty}";
        }

        private static string FormatStatusLabel(ActionSlotStatus status)
        {
            return $"{status.Label}，{FormatPenalty(status)}";
        }

        private static List<ActionSlotStatus> GetSlotStatuses(ActorSnapshot actor, int slotId)
        {
            var result = new List<ActionSlotStatus>();
            foreach (var status in actor.ActiveActionSlotStatuses)
            {
                if (status.SlotId == slotId)
                    result.Add(status);
            }
            foreach (var status in actor.PendingActionSlotStatuses)
            {
                if (status.SlotId == slotId)
                    result.Add(status);
            }
            return result;
        }

        private static Color StatusColor(ActionSlotStatus status)
        {
            return status.Label == "失控" ? SealRed : Gold;
        }

        private static void DrawVital(float x, float y, float width, string label, int current, int max,
            float highThreshold, float midThreshold)
        {
            float pct = max > 0 ? Math.Clamp(current / (float)max, 0f, 1f) : 0f;
            Color color = pct >= highThreshold ? Paper : pct >= midThreshold ? Gold : SealRed;
            FontManager.DrawText(label, x, y, 10, PaperDim);
            float barX = x + 34f;
            float barW = width - 72f;
            int segmentCount = Math.Max(1, max);
            int filledSegments = Math.Clamp(current, 0, segmentCount);
            const float segmentGap = 2f;
            float segmentW = (barW - segmentGap * (segmentCount - 1)) / segmentCount;
            for (int i = 0; i < segmentCount; i++)
            {
                var segment = new Rectangle(barX + i * (segmentW + segmentGap), y + 2f, segmentW, 9f);
                bool filled = i < filledSegments;
                Raylib.DrawRectangleRounded(segment, 0.22f, 3,
                    filled ? color : new Color(224, 224, 216, 30));
                if (!filled)
                {
                    Raylib.DrawRectangleRoundedLinesEx(segment, 0.22f, 3, 1f, new Color(224, 224, 216, 48));
                }
            }
            FontManager.DrawText($"{current}/{max}", x + width - 34f, y, 10, color);
        }

        // 单行冷静条：格数即数值，失态阈值靠一道留白分隔危险区与缓冲区，危险格常驻暖色暗示——
        // 不再用文字与刻度重复说明，真正的失态/失控效果会显示在上方骰池标签里。
        private static void DrawComposureCells(float labelX, float poolX, float y, int composure)
        {
            int max = TeamState.MaxComposure;
            int faint = TeamState.FaintThreshold;
            Color fill = composure <= TeamState.LossOfControlThreshold ? SealRed
                       : composure <= faint ? Gold
                       : Paper;
            FontManager.DrawText("冷静", labelX, y + 1f, 9, PaperDim);

            const float segmentW = 13f;
            const float segmentGap = 2f;
            const float boundaryGap = 6f; // 失态线：以留白替代刻度线和数字
            int filledSegments = Math.Clamp(composure, 0, max);
            for (int i = 0; i < max; i++)
            {
                float extra = i >= faint ? boundaryGap : 0f;
                var segment = new Rectangle(poolX + i * (segmentW + segmentGap) + extra, y, segmentW, 12f);
                bool filled = i < filledSegments;
                bool danger = i < faint;
                if (filled)
                {
                    Raylib.DrawRectangleRounded(segment, 0.24f, 3, fill);
                }
                else
                {
                    Color body = danger ? new Color((byte)Gold.R, (byte)Gold.G, (byte)Gold.B, (byte)24)
                                        : new Color(224, 224, 216, 22);
                    Color edge = danger ? new Color((byte)Gold.R, (byte)Gold.G, (byte)Gold.B, (byte)72)
                                        : new Color(224, 224, 216, 48);
                    Raylib.DrawRectangleRounded(segment, 0.24f, 3, body);
                    Raylib.DrawRectangleRoundedLinesEx(segment, 0.24f, 3, 1f, edge);
                }
            }
        }

        private static void DrawDie(RendererState state, SSNoir.TerminalApp.Rendering.UiInteractionContext ui,
            Rectangle rect, int value, int globalIndex, string actorId, int dieIndex,
            bool damaged,
            ref HandPanelInteraction interaction)
        {
            bool slotted = state.IsDieSlotted(globalIndex);
            bool selected = state.SelectedResource?.Type == "die" && state.SelectedResource.SourceIndex == globalIndex;
            bool hover = !slotted && ui.CanHover(rect);
            bool disabled = slotted || ui.IsLocked;
            Action<Rectangle>? backgroundPattern = damaged
                ? drawRect => DrawDamagedDiePattern(drawRect, disabled)
                : null;
            DrawToken(rect, value.ToString(), null, selected, hover, disabled, backgroundPattern);
            if (!slotted && !ui.IsLocked && ui.WasClicked(rect))
            {
                interaction.SelectedResourceToSet = new SelectedResource
                {
                    Type = "die", Value = value, SourceIndex = globalIndex,
                    ActorId = actorId, DieIndex = dieIndex
                };
            }
        }

        private static void DrawDamagedDiePattern(Rectangle rect, bool dimmed)
        {
            byte lineAlpha = dimmed ? (byte)38 : (byte)96;
            byte fillAlpha = dimmed ? (byte)10 : (byte)24;
            var line = new Color((byte)142, (byte)126, (byte)202, lineAlpha);
            var fillA = new Color((byte)126, (byte)108, (byte)190, fillAlpha);
            var fillB = new Color((byte)164, (byte)146, (byte)218, fillAlpha);
            float lineWidth = dimmed ? 0.65f : 0.95f;

            var topLeft = new System.Numerics.Vector2(rect.X + 2f, rect.Y + rect.Height * 0.16f);
            var topMid = new System.Numerics.Vector2(rect.X + rect.Width * 0.56f, rect.Y + 1f);
            var rightTop = new System.Numerics.Vector2(rect.X + rect.Width - 2f, rect.Y + rect.Height * 0.25f);
            var rightBottom = new System.Numerics.Vector2(rect.X + rect.Width - 2f, rect.Y + rect.Height * 0.78f);
            var bottomMid = new System.Numerics.Vector2(rect.X + rect.Width * 0.58f, rect.Y + rect.Height - 1f);
            var leftBottom = new System.Numerics.Vector2(rect.X + 2f, rect.Y + rect.Height * 0.74f);
            var center = new System.Numerics.Vector2(rect.X + rect.Width * 0.49f, rect.Y + rect.Height * 0.48f);

            Raylib.DrawTriangle(topMid, rightTop, center, fillA);
            Raylib.DrawTriangle(rightTop, rightBottom, center, fillB);
            Raylib.DrawTriangle(bottomMid, leftBottom, center, fillA);
            Raylib.DrawTriangle(leftBottom, topLeft, center, fillB);

            Raylib.DrawLineEx(topLeft, center, lineWidth, line);
            Raylib.DrawLineEx(topMid, center, lineWidth, line);
            Raylib.DrawLineEx(rightTop, center, lineWidth, line);
            Raylib.DrawLineEx(rightBottom, center, lineWidth, line);
            Raylib.DrawLineEx(bottomMid, center, lineWidth, line);
            Raylib.DrawLineEx(leftBottom, center, lineWidth, line);
            Raylib.DrawLineEx(topLeft, topMid, lineWidth, line);
            Raylib.DrawLineEx(rightTop, rightBottom, lineWidth, line);
            Raylib.DrawLineEx(bottomMid, leftBottom, lineWidth, line);
        }

        private static void DrawItemsAndFunction(RendererState state,
            SSNoir.TerminalApp.Rendering.UiInteractionContext ui, Rectangle area, bool isInEncounter,
            ref HandPanelInteraction interaction)
        {
            const float functionW = 158f;
            FontManager.DrawText("物品", area.X, area.Y, 13, PaperDim);
            float functionX = area.X + area.Width - functionW;
            FontManager.DrawText("功能", functionX, area.Y, 13, PaperDim);

            if (isInEncounter)
            {
                var smokeRect = new Rectangle(functionX, area.Y + 24f, 46f, 64f);
                var drinkRect = new Rectangle(functionX + 54f, area.Y + 24f, 46f, 64f);
                var restRect = new Rectangle(functionX + 108f, area.Y + 24f, 50f, 64f);
                bool hasSmoke = state.DisplayedSnapshot.Inventory.TryGetValue("香烟", out int smoke) && smoke > 0;
                bool hasDrink = state.DisplayedSnapshot.Inventory.TryGetValue("酒", out int drink) && drink > 0;
                DrawToken(smokeRect, "烟", null, false, ui.CanHover(smokeRect), ui.IsLocked || !hasSmoke);
                DrawToken(drinkRect, "酒", null, false, ui.CanHover(drinkRect), ui.IsLocked || !hasDrink);
                DrawToken(restRect, "休息", null, false, ui.CanHover(restRect), ui.IsLocked);
                if (!ui.IsLocked && hasSmoke && ui.WasClicked(smokeRect)) interaction.SmokeClicked = true;
                else if (!ui.IsLocked && hasDrink && ui.WasClicked(drinkRect)) interaction.DrinkClicked = true;
                else if (!ui.IsLocked && ui.WasClicked(restRect)) interaction.TurnClicked = true;
            }
            else
            {
                var functionRect = new Rectangle(functionX + 48f, area.Y + 24f, 110f, 64f);
                bool fnHover = ui.CanHover(functionRect);
                DrawToken(functionRect, "回家", null, false, fnHover, ui.IsLocked);
                if (!ui.IsLocked && ui.WasClicked(functionRect)) interaction.TurnClicked = true;
            }

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
            bool selected, bool hover, bool disabled, Action<Rectangle>? backgroundPattern = null)
        {
            Rectangle drawRect = selected && !disabled
                ? new Rectangle(rect.X, rect.Y - 4f, rect.Width, rect.Height) : rect;
            Color border = disabled ? new Color(120, 124, 138, 70)
                : selected ? TerminalPalette.Accent : hover ? TerminalPalette.AccentBright : new Color(190, 194, 204, 180);
            Raylib.DrawRectangleRounded(new Rectangle(drawRect.X + 2f, drawRect.Y + 2f, drawRect.Width, drawRect.Height),
                0.08f, 3, new Color(0, 0, 0, disabled ? 40 : 130));
            Raylib.DrawRectangleRounded(drawRect, 0.08f, 3,
                disabled ? new Color(8, 10, 15, 120) : BlockBg);
            backgroundPattern?.Invoke(drawRect);
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
            "金钱" => "$", "酒" => "酒", "香烟" => "烟", "药品" => "药",
            _ => name.Length > 0 ? name.Substring(0, 1) : "?"
        };
    }
}
