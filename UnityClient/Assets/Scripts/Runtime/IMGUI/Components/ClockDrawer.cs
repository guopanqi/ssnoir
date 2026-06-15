#nullable enable
using System.Collections.Generic;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    public static class ClockDrawer
    {
        public static void DrawClocksBar(List<GameClock> clocks, float y)
        {
            if (clocks == null || clocks.Count == 0) return;

            float x = 40f;
            float height = 22f;
            float spacing = 16f;

            GUI.Label(new Rect(x, y, 100, height), "当前节点状态: ", IMGUIStyles.SectionLabel);
            x += 110;

            foreach (var clock in clocks)
            {
                float clockWidth = DrawClock(ref x, y, height, clock);
                x += clockWidth + spacing;
            }

            // Divider
            IMGUIStyles.DrawLine(new Vector2(40, y + height + 4), new Vector2(UIScale.VW - 40, y + height + 4), IMGUIStyles.OutlineVariantColor, 1f);
        }

        private static float DrawClock(ref float x, float y, float height, GameClock clock)
        {
            Color activeColor = IMGUIStyles.ClockActive;
            Color inactiveColor = IMGUIStyles.ClockInactive;
            Color outlineColor = IMGUIStyles.OutlineColor;

            float labelWidth = 60;
            float clockWidth = labelWidth + 8;

            if (clock.Style == ClockStyle.Pie)
            {
                float radius = 12f;
                clockWidth = labelWidth + 8 + radius * 2 + 8;

                GUI.Label(new Rect(x, y, labelWidth, height), clock.Label, IMGUIStyles.ClockLabel);

                float centerX = x + labelWidth + 8 + radius;
                float centerY = y + height / 2f;
                var pieRect = new Rect(centerX - radius, centerY - radius, radius * 2, radius * 2);

                float fillPct = clock.Max > 0 ? Mathf.Clamp01((float)clock.Current / clock.Max) : 0f;
                PieDrawer.DrawPie(pieRect, fillPct, activeColor, outlineColor);

                string frac = $"{clock.Current}/{clock.Max}";
                var fracStyle = new GUIStyle(IMGUIStyles.ClockValue);
                fracStyle.fontSize = 11;
                GUI.Label(new Rect(x + labelWidth + 8 + radius * 2 + 4, y, 50, height), frac, fracStyle);
            }
            else if (clock.Style == ClockStyle.Countdown)
            {
                clockWidth = labelWidth + 8 + 40 + 8;

                GUI.Label(new Rect(x, y, labelWidth, height), clock.Label, IMGUIStyles.ClockLabel);

                float boxX = x + labelWidth + 8;
                float boxW = 30;
                float boxH = height - 4;
                GUI.color = inactiveColor;
                GUI.DrawTexture(new Rect(boxX, y + 2, boxW, boxH), Texture2D.whiteTexture);
                GUI.color = Color.white;
                IMGUIStyles.DrawOutline(new Rect(boxX, y + 2, boxW, boxH), 1f, outlineColor);

                var numStyle = new GUIStyle(IMGUIStyles.ClockValue);
                numStyle.fontSize = 12;
                numStyle.alignment = TextAnchor.MiddleCenter;
                GUI.Label(new Rect(boxX, y + 2, boxW, boxH), clock.Current.ToString(), numStyle);

                var maxStyle = new GUIStyle(IMGUIStyles.ClockValue);
                maxStyle.fontSize = 11;
                maxStyle.normal.textColor = IMGUIStyles.OnSurfaceVariant;
                GUI.Label(new Rect(boxX + boxW + 2, y, 40, height), $"/{clock.Max}", maxStyle);
            }
            else // Segments
            {
                int segW = 10;
                int segH = 10;
                int segSpacing = 3;
                float segTotalW = clock.Max * (segW + segSpacing) - segSpacing;
                clockWidth = labelWidth + 8 + segTotalW + 8;

                GUI.Label(new Rect(x, y, labelWidth, height), clock.Label, IMGUIStyles.ClockLabel);

                float segX = x + labelWidth + 8;
                for (int i = 0; i < clock.Max; i++)
                {
                    var segRect = new Rect(segX + i * (segW + segSpacing), y + (height - segH) / 2f, segW, segH);
                    if (i < clock.Current)
                    {
                        GUI.color = activeColor;
                        GUI.DrawTexture(segRect, Texture2D.whiteTexture);
                    }
                    else
                    {
                        GUI.color = inactiveColor;
                        GUI.DrawTexture(segRect, Texture2D.whiteTexture);
                        GUI.color = Color.white;
                        IMGUIStyles.DrawOutline(segRect, 1f, outlineColor);
                    }
                    GUI.color = Color.white;
                }
            }

            return clockWidth;
        }
    }
}

