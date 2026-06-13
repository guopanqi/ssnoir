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
            IMGUIInteractionContext ui, SSNoirGameManager gameManager, bool isExecuting = false, float executeProgress = 0f, string executingText = "执行中")
        {
            var interaction = new CardInteraction { CardClicked = false, ClickedSlotIndex = -1, ExecuteClicked = false };

            if (isFlipped)
            {
                DrawFlippedCard(rect, node, backText, isHovered, ui, ref interaction, gameManager);
                return interaction;
            }

            // ── Location Capsule Dispatch (when not focused) ──
            bool isLocation = node.IsContainer;
            if (isLocation && !isFocused)
            {
                DrawLocationLabel(rect, node.Name, isHovered);
                if (isHovered && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    interaction.CardClicked = true;
                    Event.current.Use();
                }
                return interaction;
            }

            // Determine node type and colors
            string typeLabel = "地点";
            Color normalColor = IMGUIStyles.CardBg;
            Color hoverColor = IMGUIStyles.CardHoverBg;
            Color outlineNormal = IMGUIStyles.CardOutline;
            Color outlineHover = IMGUIStyles.CardHoverOutline;

            if (node.IsContainer)
            {
                typeLabel = "地点";
            }
            else if (node.Resolve != null)
            {
                if (node.Resolve.Type == ResolveType.Instant)
                {
                    typeLabel = "行动";
                    outlineNormal = IMGUIStyles.OutlineVariantColor;
                    outlineHover = IMGUIStyles.SecondaryColor;
                }
                else if (node.Resolve.Type == ResolveType.Roll)
                {
                    typeLabel = "判定";
                    outlineNormal = IMGUIStyles.OutlineColor;
                    outlineHover = IMGUIStyles.TertiaryColor; // Burnt Amber alert/POIs
                }
                else if (node.Resolve.Type == ResolveType.Observe)
                {
                    typeLabel = "观察";
                    outlineNormal = IMGUIStyles.OutlineColor;
                    outlineHover = IMGUIStyles.SecondaryColor;
                }
            }

            Color bgColor = isHovered ? hoverColor : (isFocused ? hoverColor : normalColor);
            Color outlineColor = isHovered ? outlineHover : (isFocused ? IMGUIStyles.PrimaryColor : outlineNormal);
            float outlineThickness = (isHovered || isFocused) ? 2f : 1f;

            // Draw card background
            GUI.color = bgColor;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(rect, outlineThickness, outlineColor);

            // Draw subtle horizontal scanline animation over active focused panels
            if (isFocused)
            {
                IMGUIStyles.DrawScanLine(rect, new Color(0.671f, 0.780f, 1.0f, 0.15f), 100f, 1.5f);
            }

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
            bool showButton = hasRequires || (node.Resolve != null && node.Resolve.Type == ResolveType.Instant);

            // Title
            float titleY = showButton ? rect.y + 12 : rect.y + rect.height / 2f - 20;
            GUI.Label(new Rect(rect.x, titleY, rect.width, 22), node.Name, IMGUIStyles.CardTitle);

            // Type label
            float typeY = showButton ? rect.y + 34 : rect.y + rect.height / 2f + 4;
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
                    bool slotHover = ui.CanHover(slotRect);
                    var res = slotted[j];

                    if (res == null)
                    {
                        GUI.color = IMGUIStyles.SlotEmpty;
                        GUI.DrawTexture(slotRect, Texture2D.whiteTexture);
                        GUI.color = Color.white;
                        IMGUIStyles.DrawOutline(slotRect, 1f, slotHover ? IMGUIStyles.PrimaryColor : IMGUIStyles.SlotEmptyBorder);

                        string placeholder = node.Requires[j].Type == "die" ? "D" : node.Requires[j].ItemId.Substring(0, 1);
                        if (node.Requires[j].Type == "item" && node.Requires[j].Qty > 1)
                            placeholder += node.Requires[j].Qty;
                        int fontSize = placeholder.Length > 2 ? 10 : (placeholder.Length > 1 ? 12 : 16);
                        var pStyle = new GUIStyle(IMGUIStyles.SlotLabel);
                        pStyle.fontSize = fontSize;
                        pStyle.normal.textColor = IMGUIStyles.OnSurfaceVariant;
                        GUI.Label(slotRect, placeholder, pStyle);
                    }
                    else
                    {
                        GUI.color = IMGUIStyles.SlotFilled;
                        GUI.DrawTexture(slotRect, Texture2D.whiteTexture);
                        GUI.color = Color.white;
                        IMGUIStyles.DrawOutline(slotRect, 1f, IMGUIStyles.SlotFilledBorder);

                        string valStr = res.Type == "die" ? res.Value.ToString() : res.ItemId.Substring(0, 1);
                        if (res.Type == "item" && res.Value > 1)
                            valStr += res.Value;
                        int fontSize = valStr.Length > 2 ? 10 : (valStr.Length > 1 ? 12 : 16);
                        var vStyle = new GUIStyle(IMGUIStyles.SlotLabel);
                        vStyle.fontSize = fontSize;
                        vStyle.normal.textColor = Color.white;
                        GUI.Label(slotRect, valStr, vStyle);
                    }

                    if (ui.WasClicked(slotRect))
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
                if (isExecuting)
                {
                    DrawExecuteProgress(exeRect, executeProgress, executingText);
                }
                else if (allFilled)
                {
                    if (IMGUIButton.Draw(exeRect, "执行", ui, IMGUIStyles.PrimaryColor, IMGUIStyles.ExecuteBtnHover, IMGUIStyles.ExecuteLabel))
                    {
                        interaction.ExecuteClicked = true;
                    }
                }
                else
                {
                    IMGUIButton.Draw(exeRect, "待命", ui, IMGUIStyles.OutlineVariantColor, Color.clear, IMGUIStyles.ExecuteLabel, false);
                }
            }
            else if (showButton)
            {
                // For instant-action cards (no requirements, but show button)
                float exeW = 90;
                float exeH = 20;
                float exeX = rect.x + (rect.width - exeW) / 2f;
                float exeY = rect.y + 75;
                var exeRect = new Rect(exeX, exeY, exeW, exeH);

                if (isExecuting)
                {
                    DrawExecuteProgress(exeRect, executeProgress, executingText);
                }
                else if (IMGUIButton.Draw(exeRect, "执行", ui, IMGUIStyles.PrimaryColor, IMGUIStyles.ExecuteBtnHover, IMGUIStyles.ExecuteLabel))
                {
                    interaction.ExecuteClicked = true;
                }
            }
            else
            {
                // Simple card click
                if (ui.WasClicked(rect))
                {
                    interaction.CardClicked = true;
                    Event.current.Use();
                }
            }

            // Draw Modifier Tags on the left side of the card
            var modifiers = node.Resolve?.DifficultyModifiers.Count > 0 ? node.Resolve.DifficultyModifiers : null;
            if (modifiers != null && modifiers.Count > 0)
            {
                for (int k = 0; k < modifiers.Count; k++)
                {
                    var mod = modifiers[k];
                    string modText = $"{mod.Reason} {(mod.Value > 0 ? "+" : "")}{mod.Value}";
                    
                    var tagStyle = new GUIStyle(GUI.skin.label);
                    tagStyle.fontSize = 10;
                    tagStyle.alignment = TextAnchor.MiddleCenter;
                    tagStyle.normal.textColor = Color.white;

                    Vector2 textSize = tagStyle.CalcSize(new GUIContent(modText));
                    float tagW = textSize.x + 12;
                    float tagH = 18;
                    float tagX = rect.x - tagW + 6;
                    float tagY = rect.y + 8 + k * 22;
                    var tagRect = new Rect(tagX, tagY, tagW, tagH);

                    Color tagBg = mod.Value < 0 ? new Color(0.412f, 0.0f, 0.020f, 0.85f) 
                                 : (mod.Value > 0 ? new Color(0.0f, 0.184f, 0.40f, 0.85f) : IMGUIStyles.SlotEmpty);
                    Color tagBorder = mod.Value < 0 ? IMGUIStyles.ErrorColor 
                                     : (mod.Value > 0 ? IMGUIStyles.PrimaryColor : IMGUIStyles.OutlineColor);

                    GUI.color = tagBg;
                    GUI.DrawTexture(tagRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    IMGUIStyles.DrawOutline(tagRect, 1f, tagBorder);

                    GUI.Label(tagRect, modText, tagStyle);
                }
            }

            return interaction;
        }

        private static void DrawFlippedCard(Rect rect, GameNode node, string backText, bool isHovered, IMGUIInteractionContext ui,
            ref CardInteraction interaction, SSNoirGameManager gameManager)
        {
            Color bg = isHovered ? IMGUIStyles.CardHoverBg : IMGUIStyles.FlippedBg;
            Color outline = isHovered ? IMGUIStyles.PrimaryColor : IMGUIStyles.FlippedOutline;
            float thickness = isHovered ? 2f : 1f;

            GUI.color = bg;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(rect, thickness, outline);

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

            if (ui.WasClicked(rect))
            {
                interaction.CardClicked = true;
                Event.current.Use();
            }
        }

        private static void DrawExecuteProgress(Rect rect, float progress, string text)
        {
            progress = Mathf.Clamp01(progress);
            GUI.color = IMGUIStyles.ModalBg;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            var fillRect = new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * progress, rect.height - 4f);
            GUI.color = IMGUIStyles.PrimaryColor;
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(rect, 1f, IMGUIStyles.PrimaryColor);

            string label = string.IsNullOrEmpty(text) ? "执行中" : text;
            if (label.Length > 5)
            {
                label = "执行中";
            }
            GUI.Label(rect, label, IMGUIStyles.ExecuteLabel);
        }

        private static void DrawClockBadge(ref float rightX, float topY, GameClock clock)
        {
            Color activeColor = IMGUIStyles.ClockActive;
            Color inactiveColor = IMGUIStyles.ClockInactive;

            if (clock.Style == ClockStyle.Countdown)
            {
                string text = $"{clock.Label} {clock.Current}/{clock.Max}";
                int fontSize = 10;
                float textWidth = 80;
                float badgeW = textWidth + 8;
                float badgeH = 14;
                float badgeX = rightX - badgeW;
                float badgeY = topY;

                GUI.color = IMGUIStyles.SlotEmpty;
                GUI.DrawTexture(new Rect(badgeX, badgeY, badgeW, badgeH), Texture2D.whiteTexture);
                GUI.color = Color.white;
                IMGUIStyles.DrawOutline(new Rect(badgeX, badgeY, badgeW, badgeH), 1f, IMGUIStyles.OutlineColor);

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

                GUI.color = IMGUIStyles.SlotEmpty;
                GUI.DrawTexture(new Rect(badgeX, badgeY, badgeW, badgeH), Texture2D.whiteTexture);
                GUI.color = Color.white;
                IMGUIStyles.DrawOutline(new Rect(badgeX, badgeY, badgeW, badgeH), 1f, IMGUIStyles.OutlineColor);

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
                        GUI.color = Color.white;
                        IMGUIStyles.DrawOutline(dotRect, 1f, IMGUIStyles.OutlineColor);
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

                GUI.color = IMGUIStyles.SlotEmpty;
                GUI.DrawTexture(new Rect(badgeX, badgeY, badgeW, badgeH), Texture2D.whiteTexture);
                GUI.color = Color.white;
                IMGUIStyles.DrawOutline(new Rect(badgeX, badgeY, badgeW, badgeH), 1f, IMGUIStyles.OutlineColor);

                var style = new GUIStyle(IMGUIStyles.ClockLabel);
                style.fontSize = fontSize;
                GUI.Label(new Rect(badgeX + 4, badgeY, labelWidth, badgeH), labelText, style);

                // Real pie sector
                float pieX = badgeX + 4 + labelWidth + 4;
                float pieY = badgeY + (badgeH - pieRadius * 2) / 2f;
                var pieRect = new Rect(pieX, pieY, pieRadius * 2, pieRadius * 2);
                float fillPct = clock.Max > 0 ? Mathf.Clamp01((float)clock.Current / clock.Max) : 0f;
                PieDrawer.DrawPieBadge(pieRect, fillPct, activeColor, IMGUIStyles.OutlineColor);

                rightX -= (badgeW + 4);
            }
        }

        private static void DrawLocationLabel(Rect rect, string name, bool isHovered)
        {
            Color bgColor = isHovered ? IMGUIStyles.CardHoverBg : IMGUIStyles.CardBg;
            Color border = isHovered ? IMGUIStyles.PrimaryColor : IMGUIStyles.CardOutline;

            GUI.color = bgColor;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(rect, 1f, border);

            var labelStyle = new GUIStyle(IMGUIStyles.CardTitle)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
            labelStyle.normal.textColor = isHovered ? Color.white : new Color(0.8f, 0.85f, 0.95f, 1f);
            GUI.Label(rect, name, labelStyle);
        }
    }
}
