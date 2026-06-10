#nullable enable
using UnityEngine;

namespace SSNoir.IMGUI
{
    public static class NavigationDrawer
    {
        public static void Draw(SSNoirGameManager gameManager, Vector2 mousePos)
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

                if (IMGUIStyles.DrawTechnicalButton(returnRect, "< 返回", mousePos, IMGUIStyles.PrimaryColor, new Color(0.671f, 0.780f, 1.0f, 0.10f), style))
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

            // Divider
            IMGUIStyles.DrawLine(new Vector2(40, 88), new Vector2(Screen.width - 40, 88), IMGUIStyles.OutlineVariantColor, 1f);
        }
    }
}

