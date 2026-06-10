#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    public static class CardDrawer
    {
        public struct CardInteraction
        {
            public bool CardClicked;
            public int ClickedSlotIndex;
            public bool ExecuteClicked;
        }

        public static CardInteraction DrawCard(Rect rect, GameNode node, bool isHovered, bool isFlipped, bool isFocused,
            List<SlottedResource?>? slotted, List<GameClock> clocks, string backText,
            Vector2 mousePos, SSNoirGameManager gameManager)
        {
            var interaction = new CardInteraction { CardClicked = false, ClickedSlotIndex = -1, ExecuteClicked = false };

            if (isFlipped)
            {
                DrawFlippedCard(rect, node, backText, isHovered, mousePos, ref interaction, gameManager);
                return interaction;
            }

            // Determine node type and colors
            string typeLabel = "地点";
            Color normalColor = new Color(0.12f, 0.16f, 0.22f, 0.85f);
            Color hoverColor = new Color(0.18f, 0.24f, 0.32f, 0.95f);
            Color outlineNormal = new Color(0.3f, 0.35f, 0.45f, 1f);
            Color outlineHover = new Color(0.5f, 0.55f, 0.7f, 1f);

            if (node.HasChildren)
            {
                typeLabel = "地点";
            }
            else if (node.Resolve != null)
            {
                if (node.Resolve.Type == ResolveType.Instant)
                {
                    typeLabel = "行动";
                    normalColor = new Color(0.24f, 0.14f, 0.08f, 0.85f);
                    hoverColor = new Color(0.36f, 0.22f, 0.12f, 0.95f);
                    outlineNormal = new Color(0.5f, 0.35f, 0.2f, 1f);
                    outlineHover = new Color(0.7f, 0.5f, 0.3f, 1f);
                }
                else if (node.Resolve.Type == ResolveType.Roll)
                {
                    typeLabel = "判定";
                    normalColor = new Color(0.18f, 0.12f, 0.24f, 0.85f);
                    hoverColor = new Color(0.28f, 0.18f, 0.36f, 0.95f);
                    outlineNormal = new Color(0.45f, 0.3f, 0.55f, 1f);
                    outlineHover = new Color(0.65f, 0.45f, 0.75f, 1f);
                }
                else if (node.Resolve.Type == ResolveType.Observe)
                {
                    typeLabel = "观察";
                    normalColor = new Color(0.08f, 0.18f, 0.14f, 0.85f);
                    hoverColor = new Color(0.12f, 0.26f, 0.20f, 0.95f);
                    outlineNormal = new Color(0.2f, 0.45f, 0.35f, 1f);
                    outlineHover = new Color(0.35f, 0.65f, 0.5f, 1f);
                }
            }

            Color bgColor = isHovered ? hoverColor : (isFocused ? hoverColor : normalColor);
            Color outlineColor = isHovered ? outlineHover : outlineNormal;

            // Draw card background
            GUI.color = bgColor;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = outlineColor;
            DrawOutline(rect, 1);
            GUI.color = Color.white;

            // Draw clock badges (top-right)
            if (clocks != null && clocks.Count > 0)
            {
                float badgeX = rect.x + rect.width - 6;
                float badgeY = rect.y + 4;
                foreach (var clock in clocks)
                {
                    DrawClockBadge(ref badgeX, badgeY, clock);
                }
            }

            bool hasRequires = node.Requires != null && node.Requires.Count > 0 && slotted != null && slotted.Count == node.Requires.Count;

            // Title
            float titleY = hasRequires ? rect.y + 12 : rect.y + rect.height / 2f - 20;
            GUI.Label(new Rect(rect.x, titleY, rect.width, 22), node.Name, IMGUIStyles.CardTitle);

            // Type label
            float typeY = hasRequires ? rect.y + 34 : rect.y + rect.height / 2f + 4;
            GUI.Label(new Rect(rect.x, typeY, rect.width, 18), $"— {typeLabel} —", IMGUIStyles.CardSubtitle);

            if (hasRequires)
            {
                int M = node.Requires.Count;
                float slotW = 36;
                float slotH = 36;
                float spacing = 8;
                float totalWidth = M * slotW + (M - 1) * spacing;
                float slotStartX = rect.x + (rect.width - totalWidth) / 2f;
                float slotY = rect.y + 56;

                for (int j = 0; j < M; j++)
                {
                    var slotRect = new Rect(slotStartX + j * (slotW + spacing), slotY, slotW, slotH);
                    bool slotHover = slotRect.Contains(mousePos);
                    var res = slotted[j];

                    if (res == null)
                    {
                        GUI.color = IMGUIStyles.SlotEmpty;
                        GUI.DrawTexture(slotRect, Texture2D.whiteTexture);
                        GUI.color = slotHover ? IMGUIStyles.SlotEmptyBorder : new Color(0.3f, 0.3f, 0.4f, 1f);
                        DrawOutline(slotRect, 1);
                        GUI.color = Color.white;

                        string placeholder = node.Requires[j].Type == "die" ? "D" : node.Requires[j].ItemName.Substring(0, 1);
                        if (node.Requires[j].Type == "item" && node.Requires[j].Qty > 1)
                            placeholder += node.Requires[j].Qty;
                        int fontSize = placeholder.Length > 2 ? 10 : (placeholder.Length > 1 ? 12 : 16);
                        var pStyle = new GUIStyle(IMGUIStyles.SlotLabel);
                        pStyle.fontSize = fontSize;
                        pStyle.normal.textColor = new Color(0.5f, 0.5f, 0.6f, 1f);
                        GUI.Label(slotRect, placeholder, pStyle);
                    }
                    else
                    {
                        GUI.color = IMGUIStyles.SlotFilled;
                        GUI.DrawTexture(slotRect, Texture2D.whiteTexture);
                        GUI.color = new Color(0.4f, 0.7f, 0.5f, 1f);
                        DrawOutline(slotRect, 1);
                        GUI.color = Color.white;

                        string valStr = res.Type == "die" ? res.Value.ToString() : res.ItemName.Substring(0, 1);
                        if (res.Type == "item" && res.Value > 1)
                            valStr += res.Value;
                        int fontSize = valStr.Length > 2 ? 10 : (valStr.Length > 1 ? 12 : 16);
                        var vStyle = new GUIStyle(IMGUIStyles.SlotLabel);
                        vStyle.fontSize = fontSize;
                        GUI.Label(slotRect, valStr, vStyle);
                    }

                    if (slotHover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                    {
                        interaction.ClickedSlotIndex = j;
                        Event.current.Use();
                    }
                }

                // Execute button
                float exeW = 90;
                float exeH = 20;
                float exeX = rect.x + (rect.width - exeW) / 2f;
                float exeY = slotY + slotH + 8;
                var exeRect = new Rect(exeX, exeY, exeW, exeH);

                bool allFilled = slotted != null && slotted.All(s => s != null);
                if (allFilled)
                {
                    bool exeHover = exeRect.Contains(mousePos);
                    GUI.color = exeHover ? new Color(0.4f, 0.8f, 0.4f, 1f) : new Color(0.25f, 0.6f, 0.25f, 1f);
                    GUI.DrawTexture(exeRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    GUI.Label(exeRect, "执行", IMGUIStyles.ExecuteLabel);

                    if (exeRect.Contains(mousePos) && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                    {
                        interaction.ExecuteClicked = true;
                        Event.current.Use();
                    }
                }
                else
                {
                    GUI.color = new Color(0.2f, 0.2f, 0.22f, 1f);
                    GUI.DrawTexture(exeRect, Texture2D.whiteTexture);
                    GUI.color = new Color(0.3f, 0.3f, 0.35f, 1f);
                    DrawOutline(exeRect, 1);
                    GUI.color = Color.white;
                    var waitStyle = new GUIStyle(IMGUIStyles.ExecuteLabel);
                    waitStyle.normal.textColor = new Color(0.4f, 0.4f, 0.45f, 1f);
                    GUI.Label(exeRect, "待命", waitStyle);
                }
            }
            else
            {
                // Simple card click
                if (isHovered && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    interaction.CardClicked = true;
                    Event.current.Use();
                }
            }

            return interaction;
        }

        private static void DrawFlippedCard(Rect rect, GameNode node, string backText, bool isHovered, Vector2 mousePos,
            ref CardInteraction interaction, SSNoirGameManager gameManager)
        {
            Color bg = isHovered ? new Color(0.08f, 0.22f, 0.16f, 0.95f) : IMGUIStyles.FlippedBg;
            Color outline = isHovered ? new Color(0.3f, 0.7f, 0.5f, 1f) : IMGUIStyles.FlippedOutline;

            GUI.color = bg;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = outline;
            DrawOutline(rect, 1);
            GUI.color = Color.white;

            // Title
            GUI.Label(new Rect(rect.x + 8, rect.y + 8, rect.width - 16, 20), node.Name, IMGUIStyles.FlippedTitle);

            // Subtitle
            GUI.Label(new Rect(rect.x + 8, rect.y + 28, rect.width - 16, 16), "— 已解读线索 —", IMGUIStyles.FlippedTip);

            // Content
            var contentRect = new Rect(rect.x + 8, rect.y + 48, rect.width - 16, rect.height - 68);
            string clueText = node.Resolve != null ? node.Resolve.ObserveText : "";
            GUI.Label(contentRect, clueText, IMGUIStyles.FlippedContent);

            // Tip
            GUI.Label(new Rect(rect.x + 8, rect.y + rect.height - 18, rect.width - 16, 14), "点击返回", IMGUIStyles.FlippedTip);

            if (isHovered && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                interaction.CardClicked = true;
                Event.current.Use();
            }
        }

        private static void DrawClockBadge(ref float rightX, float topY, GameClock clock)
        {
            Color activeColor = IMGUIStyles.ClockActive;
            Color inactiveColor = IMGUIStyles.ClockInactive;
            Color textColor = new Color(0.85f, 0.85f, 0.95f, 1f);

            if (clock.Style == ClockStyle.Countdown)
            {
                string text = $"{clock.Label} {clock.Current}/{clock.Max}";
                int fontSize = 10;
                float textWidth = 80; // approximate
                float badgeW = textWidth + 8;
                float badgeH = 14;
                float badgeX = rightX - badgeW;
                float badgeY = topY;

                GUI.color = new Color(0.12f, 0.12f, 0.15f, 0.85f);
                GUI.DrawTexture(new Rect(badgeX, badgeY, badgeW, badgeH), Texture2D.whiteTexture);
                GUI.color = new Color(0.4f, 0.4f, 0.55f, 1f);
                DrawOutline(new Rect(badgeX, badgeY, badgeW, badgeH), 1);
                GUI.color = Color.white;

                var style = new GUIStyle(IMGUIStyles.ClockLabel);
                style.fontSize = fontSize;
                style.alignment = TextAnchor.MiddleCenter;
                GUI.Label(new Rect(badgeX, badgeY, badgeW, badgeH), text, style);

                rightX -= (badgeW + 4);
            }
            else if (clock.Style == ClockStyle.Segments)
            {
                string labelText = clock.Label;
                int fontSize = 10;
                float labelWidth = 40;
                int dotSize = 5;
                int spacing = 2;
                float dotsW = clock.Max * (dotSize + spacing) - spacing;
                float badgeW = labelWidth + 6 + dotsW + 6;
                float badgeH = 14;
                float badgeX = rightX - badgeW;
                float badgeY = topY;

                GUI.color = new Color(0.12f, 0.12f, 0.15f, 0.85f);
                GUI.DrawTexture(new Rect(badgeX, badgeY, badgeW, badgeH), Texture2D.whiteTexture);
                GUI.color = new Color(0.4f, 0.4f, 0.55f, 1f);
                DrawOutline(new Rect(badgeX, badgeY, badgeW, badgeH), 1);
                GUI.color = Color.white;

                var style = new GUIStyle(IMGUIStyles.ClockLabel);
                style.fontSize = fontSize;
                GUI.Label(new Rect(badgeX + 4, badgeY, labelWidth, badgeH), labelText, style);

                float dotStartX = badgeX + 4 + labelWidth + 4;
                for (int i = 0; i < clock.Max; i++)
                {
                    var dotRect = new Rect(dotStartX + i * (dotSize + spacing), badgeY + (badgeH - dotSize) / 2f, dotSize, dotSize);
                    if (i < clock.Current)
                    {
                        GUI.color = activeColor;
                        GUI.DrawTexture(dotRect, Texture2D.whiteTexture);
                    }
                    else
                    {
                        GUI.color = inactiveColor;
                        GUI.DrawTexture(dotRect, Texture2D.whiteTexture);
                        GUI.color = new Color(0.4f, 0.4f, 0.5f, 1f);
                        DrawOutline(dotRect, 1);
                    }
                    GUI.color = Color.white;
                }

                rightX -= (badgeW + 4);
            }
            else // Pie
            {
                string labelText = clock.Label;
                int fontSize = 10;
                float labelWidth = 40;
                float pieRadius = 10f;
                float badgeW = labelWidth + 6 + pieRadius * 2 + 6;
                float badgeH = 14;
                float badgeX = rightX - badgeW;
                float badgeY = topY;

                GUI.color = new Color(0.12f, 0.12f, 0.15f, 0.85f);
                GUI.DrawTexture(new Rect(badgeX, badgeY, badgeW, badgeH), Texture2D.whiteTexture);
                GUI.color = new Color(0.4f, 0.4f, 0.55f, 1f);
                DrawOutline(new Rect(badgeX, badgeY, badgeW, badgeH), 1);
                GUI.color = Color.white;

                var style = new GUIStyle(IMGUIStyles.ClockLabel);
                style.fontSize = fontSize;
                GUI.Label(new Rect(badgeX + 4, badgeY, labelWidth, badgeH), labelText, style);

                // Real pie sector
                float pieX = badgeX + 4 + labelWidth + 4;
                float pieY = badgeY + (badgeH - pieRadius * 2) / 2f;
                var pieRect = new Rect(pieX, pieY, pieRadius * 2, pieRadius * 2);
                float fillPct = clock.Max > 0 ? Mathf.Clamp01((float)clock.Current / clock.Max) : 0f;
                PieDrawer.DrawPieBadge(pieRect, fillPct, activeColor, new Color(0.4f, 0.4f, 0.55f, 1f));

                rightX -= (badgeW + 4);
            }
        }

        private static void DrawOutline(Rect rect, int thickness)
        {
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        }
    }
}
