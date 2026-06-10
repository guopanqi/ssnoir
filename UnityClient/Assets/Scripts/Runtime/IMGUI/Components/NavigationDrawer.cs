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
                bool isHovered = returnRect.Contains(mousePos);

                Color bg = isHovered ? new Color(0.25f, 0.25f, 0.35f, 1f) : new Color(0.1f, 0.1f, 0.14f, 1f);
                Color border = isHovered ? new Color(0.85f, 0.7f, 0.3f, 1f) : new Color(0.3f, 0.3f, 0.4f, 1f);

                GUI.color = bg;
                GUI.DrawTexture(returnRect, Texture2D.whiteTexture);
                GUI.color = border;
                DrawOutline(returnRect, 2);
                GUI.color = Color.white;

                var textColor = isHovered ? Color.white : new Color(0.6f, 0.6f, 0.7f, 1f);
                var style = new GUIStyle(IMGUIStyles.StatusLabel);
                style.normal.textColor = textColor;
                style.alignment = TextAnchor.MiddleCenter;
                style.fontSize = 16;
                GUI.Label(returnRect, "< 返回", style);

                if (isHovered && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    gameManager.GoBackNavigation();
                    Event.current.Use();
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
            crumbStyle.normal.textColor = new Color(0.6f, 0.6f, 0.65f, 1f);
            crumbStyle.fontSize = 16;
            GUI.Label(new Rect(startX, startY + 8, 800, 26), breadcrumbText, crumbStyle);

            // Divider
            GUI.color = new Color(0.2f, 0.2f, 0.25f, 1f);
            GUI.DrawTexture(new Rect(40, 88, Screen.width - 80, 2), Texture2D.whiteTexture);
            GUI.color = Color.white;
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
