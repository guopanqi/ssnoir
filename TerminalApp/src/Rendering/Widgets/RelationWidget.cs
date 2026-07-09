using System;
using Raylib_cs;
using SSNoir.Core;

namespace SSNoir.Rendering
{
    public static class RelationWidget
    {
        // 关系档位配色（序号 0..4 对应 RelationScale.BandNames：敌视/冷淡/中立/脸熟/自己人）。
        private static readonly Color[] RelationBandColors =
        {
            new Color((byte)190, (byte)70,  (byte)70,  (byte)255), // 敌视
            new Color((byte)200, (byte)140, (byte)60,  (byte)255), // 冷淡
            new Color((byte)110, (byte)112, (byte)130, (byte)255), // 中立
            new Color((byte)70,  (byte)150, (byte)165, (byte)255), // 脸熟
            new Color((byte)80,  (byte)185, (byte)115, (byte)255), // 自己人
        };

        private const float CollapsedWidth = 190f;
        private const float ExpandedWidth = 250f;
        private const float CollapsedHeight = 32f;
        private const float ExpandedHeight = 98f;

        /// <summary>
        /// 绘制右上角关系面板，右边缘对齐 <paramref name="rightEdge"/>。
        /// 返回面板当前左边缘 x 坐标，便于左侧控件继续向右排列。
        /// </summary>
        public static float Draw(RendererState state, SSNoir.TerminalApp.Rendering.UiInteractionContext ui, float rightEdge, float topY)
        {
            var snapshot = state.DisplayedSnapshot;
            string[] factions = { "官僚", "劳工", "富商" };

            bool expanded = state.IsRelationExpanded;
            float panelW = expanded ? ExpandedWidth : CollapsedWidth;
            float panelH = expanded ? ExpandedHeight : CollapsedHeight;
            float panelX = rightEdge - panelW;
            float panelY = topY;
            var panelRect = new Rectangle(panelX, panelY, panelW, panelH);

            bool hovered = ui.CanHover(panelRect);
            var borderColor = hovered
                ? new Color((byte)90, (byte)120, (byte)180, (byte)255)
                : new Color((byte)55, (byte)60, (byte)80, (byte)255);

            Raylib.DrawRectangleRounded(panelRect, 0.15f, 4, new Color((byte)20, (byte)22, (byte)30, (byte)220));
            Raylib.DrawRectangleRoundedLinesEx(panelRect, 0.15f, 4, 1.0f, borderColor);

            if (ui.WasClicked(panelRect))
            {
                state.IsRelationExpanded = !expanded;
            }

            float pad = 8f;

            if (expanded)
            {
                float rowH = 27f;
                float rowGap = 3f;
                float labelX = panelX + pad;
                float bandX = panelX + 62f;
                float valueW = 24f;
                float valueX = panelX + panelW - pad - valueW;
                float barX = bandX;
                float barW = valueX - barX - 8f;

                var b = RelationScale.Boundaries;
                int[] edges = new int[b.Length + 2];
                edges[0] = RelationScale.Min;
                for (int k = 0; k < b.Length; k++) edges[k + 1] = b[k];
                edges[edges.Length - 1] = RelationScale.Max;

                for (int i = 0; i < factions.Length; i++)
                {
                    int value = snapshot.Relations.TryGetValue(factions[i], out var v) ? v : 0;
                    int bi = RelationScale.BandIndex(value);
                    float rowY = panelY + pad + i * (rowH + rowGap);
                    float titleY = rowY;
                    float barY = rowY + 17f;
                    float barH = 5f;
                    string bandName = RelationScale.BandNames[bi];

                    FontManager.DrawText(factions[i], labelX, titleY, 12, new Color((byte)190, (byte)194, (byte)214, (byte)255));
                    FontManager.DrawText(bandName, bandX, titleY, 12, RelationBandColors[bi]);

                    var outlineRect = new Rectangle(barX, barY, barW, barH);
                    Raylib.DrawRectangleRoundedLinesEx(outlineRect, 0.5f, 4, 1.0f, new Color((byte)50, (byte)53, (byte)70, (byte)255));

                    float mx = barX + RelationScale.Fraction(value) * barW;

                    for (int s = 0; s < edges.Length - 1; s++)
                    {
                        float x0 = barX + RelationScale.Fraction(edges[s]) * barW;
                        float x1 = barX + RelationScale.Fraction(edges[s + 1]) * barW;
                        var c = RelationBandColors[s];

                        if (s < bi)
                        {
                            var col = new Color(c.R, c.G, c.B, (byte)35);
                            Raylib.DrawRectangle((int)x0, (int)barY, (int)Math.Max(1f, x1 - x0), (int)barH, col);
                        }
                        else if (s == bi)
                        {
                            if (mx > x0)
                            {
                                var activeCol = new Color(c.R, c.G, c.B, (byte)75);
                                Raylib.DrawRectangle((int)x0, (int)barY, (int)Math.Max(1f, mx - x0), (int)barH, activeCol);
                            }
                            if (x1 > mx)
                            {
                                var inactiveCol = new Color(c.R, c.G, c.B, (byte)12);
                                Raylib.DrawRectangle((int)mx, (int)barY, (int)Math.Max(1f, x1 - mx), (int)barH, inactiveCol);
                            }
                        }
                        else
                        {
                            var col = new Color(c.R, c.G, c.B, (byte)12);
                            Raylib.DrawRectangle((int)x0, (int)barY, (int)Math.Max(1f, x1 - x0), (int)barH, col);
                        }
                    }

                    for (int s = 1; s < edges.Length - 1; s++)
                    {
                        float segX = barX + RelationScale.Fraction(edges[s]) * barW;
                        Raylib.DrawLineEx(
                            new System.Numerics.Vector2(segX, barY - 1f),
                            new System.Numerics.Vector2(segX, barY + barH + 1f),
                            1.0f,
                            new Color((byte)55, (byte)58, (byte)75, (byte)255)
                        );
                    }

                    var activeColor = RelationBandColors[bi];
                    Raylib.DrawCircle((int)mx, (int)(barY + barH / 2f), 3.5f, activeColor);
                    Raylib.DrawCircleLines((int)mx, (int)(barY + barH / 2f), 4.5f, new Color(255, 255, 255, 180));

                    string vs = value.ToString();
                    int vw = FontManager.MeasureTextWidth(vs, 12);
                    FontManager.DrawText(vs, valueX + (valueW - vw) / 2f, titleY, 12, RelationBandColors[bi]);
                }
            }
            else
            {
                float usableW = panelW - pad * 2;
                float itemW = usableW / factions.Length;
                float textY = panelY + (panelH - 12f) / 2f + 1f;

                for (int i = 0; i < factions.Length; i++)
                {
                    int value = snapshot.Relations.TryGetValue(factions[i], out var v) ? v : 0;
                    int bi = RelationScale.BandIndex(value);
                    float itemX = panelX + pad + i * itemW;

                    float labelW = FontManager.MeasureTextWidth(factions[i], 12);
                    float valueW = FontManager.MeasureTextWidth(value.ToString(), 12);
                    float groupW = labelW + 6f + valueW;
                    float groupX = itemX + (itemW - groupW) / 2f;

                    FontManager.DrawText(factions[i], groupX, textY, 12, new Color((byte)170, (byte)175, (byte)195, (byte)255));

                    string vs = value.ToString();
                    FontManager.DrawText(vs, groupX + labelW + 6f, textY, 12, RelationBandColors[bi]);
                }
            }

            return panelX;
        }
    }
}
