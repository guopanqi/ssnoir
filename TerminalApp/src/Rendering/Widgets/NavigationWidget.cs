using System.Collections.Generic;
using Raylib_cs;

namespace SSNoir.Rendering
{
    public static class NavigationWidget
    {
        public struct NavigationInteraction
        {
            public bool GoBackClicked;
        }

        public static NavigationInteraction Draw(RendererState state, System.Numerics.Vector2 mousePos, float windowWidth)
        {
            var interaction = new NavigationInteraction { GoBackClicked = false };
            float startX = 40f;
            float startY = 30f;

            if (state.NavigationStack.Count > 0)
            {
                var returnRect = new Rectangle(startX, startY, 90, 32);
                bool isHovered = Raylib.CheckCollisionPointRec(mousePos, returnRect);
                
                Color btnColor = isHovered ? new Color(60, 60, 80, 255) : new Color(40, 40, 50, 255);
                Color textColor = isHovered ? Color.White : new Color(180, 180, 200, 255);
                
                Raylib.DrawRectangleRounded(returnRect, 0.2f, 4, btnColor);
                Raylib.DrawRectangleRoundedLinesEx(returnRect, 0.2f, 4, 1.5f, new Color(80, 80, 100, 255));
                FontManager.DrawText("< 返回", startX + 18, startY + 8, 16, textColor);

                if (isHovered && Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    interaction.GoBackClicked = true;
                }

                startX += 110f;
            }

            string breadcrumbText = "当前位置: ";
            if (state.NavigationStack.Count == 0)
            {
                breadcrumbText += "根目录";
            }
            else
            {
                breadcrumbText += string.Join(" > ", state.NavigationStack.ConvertAll(n => n.Name));
            }
            FontManager.DrawText(breadcrumbText, startX, startY + 8, 16, new Color(180, 180, 200, 255));

            Raylib.DrawLineEx(new System.Numerics.Vector2(40, 80), new System.Numerics.Vector2(windowWidth - 40, 80), 1.5f, new Color(50, 50, 60, 255));

            return interaction;
        }
    }
}
