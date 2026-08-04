#nullable enable
using System.Collections.Generic;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    // 顶栏时钟：显示「当前所在层」的时钟（世界层 / 当前地点）。子节点各自的时钟画在各自卡角
    // （CardDrawer.DrawClockBadges）。两者层级不同、不重复。
    //
    // 干净的居中徽章条：Ink 底 + 40% 纸白描边 + 标签 + 值（段/倒计时/饼），进行中=金。
    public static class ClockDrawer
    {
        private const float BadgeH = 36f;
        private const float PadX   = 14f;
        private const float MidGap = 10f;
        private const float BadgeGap = 16f;

        private static Color Active   => IMGUIStyles.Gold;
        private static Color Inactive => new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.25f);
        private static Color Outline  => new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f);

        // y = 顶栏内的基线（居中横排）。
        public static void DrawClocksBar(List<GameClock> clocks, float y)
        {
            if (clocks == null || clocks.Count == 0) return;

            var widths = new float[clocks.Count];
            float total = 0f;
            for (int i = 0; i < clocks.Count; i++)
            {
                widths[i] = BadgeWidth(clocks[i]);
                total += widths[i];
            }
            total += BadgeGap * (clocks.Count - 1);

            float x = (UIScale.VW - total) * 0.5f;
            for (int i = 0; i < clocks.Count; i++)
            {
                DrawBadge(x, y, widths[i], clocks[i]);
                x += widths[i] + BadgeGap;
            }
        }

        private static float LabelWidth(GameClock clock)
        {
            var style = new GUIStyle(IMGUIStyles.ClockLabel) { fontSize = IMGUIStyles.FontSize(17) };
            return style.CalcSize(new GUIContent(clock.Label)).x;
        }

        private static float ValueWidth(GameClock clock)
        {
            return clock.Style switch
            {
                ClockStyle.Countdown => 42f + 6f + 34f,
                ClockStyle.Segments  => Mathf.Max(0f, clock.Max * 15f - 4f),
                ClockStyle.Pie       => 24f + 8f + 42f,
                _ => 42f
            };
        }

        private static float BadgeWidth(GameClock clock)
        {
            return PadX + LabelWidth(clock) + MidGap + ValueWidth(clock) + PadX;
        }

        private static void DrawBadge(float x, float y, float w, GameClock clock)
        {
            var rect = new Rect(x, y, w, BadgeH);
            GUI.color = IMGUIStyles.Ink;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(rect, 1f, Outline);

            var labelStyle = new GUIStyle(IMGUIStyles.ClockLabel)
            {
                fontSize = IMGUIStyles.FontSize(17),
                alignment = TextAnchor.MiddleLeft
            };
            float labelW = labelStyle.CalcSize(new GUIContent(clock.Label)).x;
            GUI.Label(new Rect(x + PadX, y, labelW, BadgeH), clock.Label, labelStyle);

            float vx = x + PadX + labelW + MidGap;
            DrawValue(vx, y, clock);
        }

        private static void DrawValue(float vx, float y, GameClock clock)
        {
            if (clock.Style == ClockStyle.Countdown)
            {
                var box = new Rect(vx, y + 4f, 42f, BadgeH - 8f);
                GUI.color = Inactive;
                GUI.DrawTexture(box, Texture2D.whiteTexture);
                GUI.color = Color.white;
                IMGUIStyles.DrawOutline(box, 1f, Outline);

                var numStyle = new GUIStyle(IMGUIStyles.ClockValue) { fontSize = IMGUIStyles.FontSize(18), alignment = TextAnchor.MiddleCenter };
                GUI.Label(box, clock.Current.ToString(), numStyle);

                var maxStyle = new GUIStyle(IMGUIStyles.ClockValue)
                {
                    fontSize = IMGUIStyles.FontSize(16),
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = IMGUIStyles.TextSecondary }
                };
                GUI.Label(new Rect(box.xMax + 6f, y, 34f, BadgeH), $"/{clock.Max}", maxStyle);
            }
            else if (clock.Style == ClockStyle.Pie)
            {
                float radius = 12f;
                float cy = y + BadgeH * 0.5f;
                var pieRect = new Rect(vx, cy - radius, radius * 2f, radius * 2f);
                float pct = clock.Max > 0 ? Mathf.Clamp01((float)clock.Current / clock.Max) : 0f;
                PieDrawer.DrawPie(pieRect, pct, Active, Outline);

                var fracStyle = new GUIStyle(IMGUIStyles.ClockValue) { fontSize = IMGUIStyles.FontSize(17), alignment = TextAnchor.MiddleLeft };
                GUI.Label(new Rect(pieRect.xMax + 8f, y, 42f, BadgeH), $"{clock.Current}/{clock.Max}", fracStyle);
            }
            else // Segments
            {
                float seg = 11f, sp = 4f;
                float dy = y + (BadgeH - seg) * 0.5f;
                for (int i = 0; i < clock.Max; i++)
                {
                    var r = new Rect(vx + i * (seg + sp), dy, seg, seg);
                    if (i < clock.Current)
                    {
                        GUI.color = Active;
                        GUI.DrawTexture(r, Texture2D.whiteTexture);
                        GUI.color = Color.white;
                    }
                    else
                    {
                        GUI.color = Inactive;
                        GUI.DrawTexture(r, Texture2D.whiteTexture);
                        GUI.color = Color.white;
                        IMGUIStyles.DrawOutline(r, 1f, Outline);
                    }
                }
            }
        }
    }
}
