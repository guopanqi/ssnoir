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

                if (IMGUIButton.Draw(returnRect, "< 返回", ui, IMGUIStyles.PrimaryColor, new Color(0.671f, 0.780f, 1.0f, 0.10f), style))
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
            crumbStyle.normal.textColor = IMGUIStyles.OnSurfaceVariant;
            crumbStyle.fontSize = 16;
            GUI.Label(new Rect(startX, startY + 8, 800, 26), breadcrumbText, crumbStyle);

            // Relation Panel
            DrawRelationPanel(gameManager);

            // Divider
            IMGUIStyles.DrawLine(new Vector2(40, 88), new Vector2(UIScale.VW - 40, 88), IMGUIStyles.OutlineVariantColor, 1f);
        }

        // 关系档位配色（序号 0..4 对应 RelationScale.BandNames：敌视/冷淡/中立/脸熟/自己人）。
        private static readonly Color[] RelationBandColors =
        {
            new Color(0.75f, 0.27f, 0.27f, 1f), // 敌视
            new Color(0.78f, 0.55f, 0.24f, 1f), // 冷淡
            new Color(0.43f, 0.44f, 0.51f, 1f), // 中立
            new Color(0.27f, 0.59f, 0.65f, 1f), // 脸熟
            new Color(0.31f, 0.73f, 0.45f, 1f), // 自己人
        };

        // 每个势力一条进度条：底色按档位分段，当前值放一个高亮标记。
        private static void DrawRelationPanel(SSNoirGameManager gameManager)
        {
            var snapshot = gameManager.DisplayedSnapshot;
            string[] factions = { "官僚", "劳工", "富商" };

            float pad = 8f, rowH = 20f, labelW = 34f, valueW = 26f, gap = 8f;
            float panelW = 250f;
            float panelH = 3 * rowH + pad * 2;
            float panelX = UIScale.VW - 460f;
            float panelY = 26f;

            var oldColor = GUI.color;
            var panelRect = new Rect(panelX, panelY, panelW, panelH);
            GUI.color = new Color(0.098f, 0.110f, 0.133f, 0.8f);
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(panelRect, 1.5f, IMGUIStyles.OutlineVariantColor);

            float barX = panelX + pad + labelW + gap;
            float barW = panelW - pad * 2 - labelW - valueW - gap * 2;

            var b = RelationScale.Boundaries;
            int[] edges = new int[b.Length + 2];
            edges[0] = RelationScale.Min;
            for (int k = 0; k < b.Length; k++) edges[k + 1] = b[k];
            edges[edges.Length - 1] = RelationScale.Max;

            var labelStyle = new GUIStyle(IMGUIStyles.StatusLabel) { alignment = TextAnchor.MiddleLeft, fontSize = 13 };
            labelStyle.normal.textColor = IMGUIStyles.OnSurfaceVariant;

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
                IMGUIStyles.DrawOutline(new Rect(barX, barY, barW, barH), 1f, new Color(0.24f, 0.24f, 0.31f, 1f));

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
