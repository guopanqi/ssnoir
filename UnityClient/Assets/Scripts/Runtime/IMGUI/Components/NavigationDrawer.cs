#nullable enable
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    public static class NavigationDrawer
    {
        public static void Draw(SSNoirGameManager gameManager, IMGUIInteractionContext ui)
        {
            float startX = 40f;
            float startY = 30f;

            // Return button
            if (gameManager.NavigationStack.Count > 0 || !string.IsNullOrEmpty(gameManager.FocusedNodeName))
            {
                var returnRect = new Rect(startX, startY, 110, 40);

                var style = new GUIStyle(IMGUIStyles.StatusLabel);
                style.alignment = TextAnchor.MiddleCenter;
                style.fontSize = 16;

                // HUD 按钮：黑底白字，1px Paper 40% 描边，悬停提亮
                if (IMGUIButton.Draw(returnRect, "< 返 回", ui,
                        new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f),
                        new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.08f), style))
                {
                    gameManager.GoBackNavigation();
                }

                startX += 130f;
            }

            // Breadcrumb
            string breadcrumbText = "当前位置: ";
            string rootName = gameManager.DisplayedSnapshot.RootNode?.Name ?? "未加载";
            if (gameManager.NavigationStack.Count == 0)
            {
                breadcrumbText += rootName;
            }
            else
            {
                breadcrumbText += rootName + " > " + string.Join(" > ", gameManager.NavigationStack.ConvertAll(n => n.Name));
            }

            var crumbStyle = new GUIStyle(IMGUIStyles.StatusLabel);
            crumbStyle.normal.textColor = IMGUIStyles.TextSecondary;
            crumbStyle.fontSize = 16;
            GUI.Label(new Rect(startX, startY + 8, 800, 26), breadcrumbText, crumbStyle);

            // Relation Panel
            DrawRelationPanel(gameManager);

            // Divider
            IMGUIStyles.DrawLine(new Vector2(40, 88), new Vector2(UIScale.VW - 40, 88),
                new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.25f), 1f);
        }

        // 关系档位配色（序号 0..4 对应 RelationScale.BandNames：敌视/冷淡/中立/脸熟/自己人）。
        // 沉着版：敌视=印章红，冷淡=陶红，中立=纸白次级，脸熟=赭黄，自己人=苔绿。
        private static readonly Color[] RelationBandColors =
        {
            IMGUIStyles.SealRed,                                                       // 敌视
            IMGUIStyles.OddsFail,                                                      // 冷淡
            new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.60f), // 中立
            IMGUIStyles.OddsNeutral,                                                   // 脸熟
            IMGUIStyles.OddsSuccess,                                                   // 自己人
        };

        // 每个势力一条进度条：底色按档位分段，当前值放一个高亮标记。
        private static void DrawRelationPanel(SSNoirGameManager gameManager)
        {
            var snapshot = gameManager.DisplayedSnapshot;
            string[] factions = { "官僚", "劳工", "富商" };

            // 压扁到顶栏分割线（y=88）以内，不再越界；右侧给成长/队伍按钮留位。
            float pad = 5f, rowH = 18f, labelW = 34f, valueW = 26f, gap = 8f;
            float panelW = 250f;
            float panelH = 3 * rowH + pad * 2;
            float panelX = UIScale.VW - 470f;
            float panelY = 14f;

            var oldColor = GUI.color;
            var panelRect = new Rect(panelX, panelY, panelW, panelH);
            GUI.color = IMGUIStyles.HudBg;
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(panelRect, 1f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f));

            float barX = panelX + pad + labelW + gap;
            float barW = panelW - pad * 2 - labelW - valueW - gap * 2;

            var b = RelationScale.Boundaries;
            int[] edges = new int[b.Length + 2];
            edges[0] = RelationScale.Min;
            for (int k = 0; k < b.Length; k++) edges[k + 1] = b[k];
            edges[edges.Length - 1] = RelationScale.Max;

            var labelStyle = new GUIStyle(IMGUIStyles.StatusLabel) { alignment = TextAnchor.MiddleLeft, fontSize = 13 };
            labelStyle.normal.textColor = IMGUIStyles.TextSecondary;

            for (int i = 0; i < factions.Length; i++)
            {
                int value = snapshot.Relations.TryGetValue(factions[i], out var v) ? v : 0;
                int bi = RelationScale.BandIndex(value);
                float rowY = panelY + pad + i * rowH;
                float barY = rowY + rowH * 0.5f - 3.5f;
                float barH = 7f;

                GUI.Label(new Rect(panelX + pad, rowY, labelW, rowH), factions[i], labelStyle);

                // 分段底色，显示每个档位的区间
                for (int s = 0; s < edges.Length - 1; s++)
                {
                    float x0 = barX + RelationScale.Fraction(edges[s]) * barW;
                    float x1 = barX + RelationScale.Fraction(edges[s + 1]) * barW;
                    var c = RelationBandColors[s];
                    GUI.color = new Color(c.r, c.g, c.b, 0.28f);
                    GUI.DrawTexture(new Rect(x0, barY, Mathf.Max(1f, x1 - x0), barH), Texture2D.whiteTexture);
                }
                GUI.color = Color.white;
                IMGUIStyles.DrawOutline(new Rect(barX, barY, barW, barH), 1f,
                    new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.25f));

                // 当前值高亮标记
                float mx = barX + RelationScale.Fraction(value) * barW;
                GUI.color = RelationBandColors[bi];
                GUI.DrawTexture(new Rect(mx - 1.5f, barY - 2f, 3f, barH + 4f), Texture2D.whiteTexture);
                GUI.color = Color.white;

                var valStyle = new GUIStyle(IMGUIStyles.StatusLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 13 };
                valStyle.normal.textColor = RelationBandColors[bi];
                GUI.Label(new Rect(barX + barW + gap, rowY, valueW, rowH), value.ToString(), valStyle);
            }

            GUI.color = oldColor;
        }
    }
}
