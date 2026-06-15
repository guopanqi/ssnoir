#nullable enable
using UnityEngine;

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
            if (gameManager.NavigationStack.Count == 0)
            {
                breadcrumbText += "根目录";
            }
            else
            {
                breadcrumbText += string.Join(" > ", gameManager.NavigationStack.ConvertAll(n => n.Name));
            }

            var crumbStyle = new GUIStyle(IMGUIStyles.StatusLabel);
            crumbStyle.normal.textColor = IMGUIStyles.OnSurfaceVariant;
            crumbStyle.fontSize = 16;
            GUI.Label(new Rect(startX, startY + 8, 800, 26), breadcrumbText, crumbStyle);

            // Reputation Panel
            DrawReputationPanel(gameManager);

            // Divider
            IMGUIStyles.DrawLine(new Vector2(40, 88), new Vector2(UIScale.VW - 40, 88), IMGUIStyles.OutlineVariantColor, 1f);
        }

        private static void DrawReputationPanel(SSNoirGameManager gameManager)
        {
            var snapshot = gameManager.DisplayedSnapshot;
            int repMayor = snapshot.Reputation.TryGetValue("mayor", out var mayor) ? mayor : 0;
            int repWorkers = snapshot.Reputation.TryGetValue("workers", out var workers) ? workers : 0;
            int repElites = snapshot.Reputation.TryGetValue("elites", out var elites) ? elites : 0;

            float panelW = 240f;
            float panelH = 32f;
            float panelX = UIScale.VW - 460f; // Left of the dropdown (which is at VW - 200)
            float panelY = 30f;

            var panelRect = new Rect(panelX, panelY, panelW, panelH);
            Color panelBg = new Color(0.098f, 0.110f, 0.133f, 0.8f); // matching DropdownBg
            Color panelBorder = IMGUIStyles.OutlineVariantColor;

            var oldColor = GUI.color;
            GUI.color = panelBg;
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(panelRect, 1.5f, panelBorder);
            GUI.color = oldColor;

            float cellW = panelW / 3f;
            string[] labels = { "市长", "工人", "权贵" };
            int[] values = { repMayor, repWorkers, repElites };

            for (int i = 0; i < 3; i++)
            {
                float cellX = panelX + i * cellW;

                // Draw vertical divider
                if (i > 0)
                {
                    IMGUIStyles.DrawLine(new Vector2(cellX, panelY + 6), new Vector2(cellX, panelY + panelH - 6), new Color(0.259f, 0.278f, 0.325f, 1f), 1f);
                }

                int val = values[i];
                string sign = val > 0 ? "+" : "";
                string txt = $"{labels[i]} {sign}{val}";

                Color txtColor = IMGUIStyles.OnSurfaceVariant;
                if (val >= 30)
                {
                    txtColor = new Color(0.392f, 0.863f, 0.392f, 1f); // Green
                }
                else if (val <= -30)
                {
                    txtColor = IMGUIStyles.HealthColor; // Red
                }

                var cellStyle = new GUIStyle(IMGUIStyles.StatusLabel);
                cellStyle.alignment = TextAnchor.MiddleCenter;
                cellStyle.normal.textColor = txtColor;
                cellStyle.fontSize = 13;

                GUI.Label(new Rect(cellX, panelY, cellW, panelH), txt, cellStyle);
            }
        }
    }
}

