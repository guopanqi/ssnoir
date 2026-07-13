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

        public static NavigationInteraction Draw(RendererState state, SSNoir.TerminalApp.Rendering.UiInteractionContext ui, float windowWidth)
        {
            var interaction = new NavigationInteraction { GoBackClicked = false };
            float startX = 40f;
            float startY = 16f;

            if (state.NavigationStack.Count > 0)
            {
                var returnRect = new Rectangle(startX, startY, 78, 26);
                
                var btn = SSNoir.TerminalApp.Rendering.UiButton.Draw(returnRect, "< 返回", ui, true, 13,
                    new Color(40, 40, 50, 255), new Color(60, 60, 80, 255), null,
                    new Color(80, 80, 100, 255), new Color(80, 80, 100, 255), null,
                    new Color(180, 180, 200, 255));

                if (btn.Clicked)
                {
                    interaction.GoBackClicked = true;
                }

                startX += 92f;
            }

            string breadcrumbText = "当前位置: ";
            string rootName = state.DisplayedSnapshot.RootNode?.Name ?? "未加载";
            if (state.NavigationStack.Count == 0)
            {
                breadcrumbText += rootName;
            }
            else
            {
                breadcrumbText += rootName + " > " + string.Join(" > ", state.NavigationStack.ConvertAll(n => n.Name));
            }
            FontManager.DrawText(breadcrumbText, startX, startY + 6, 13, new Color(180, 180, 200, 255));

            Raylib.DrawLineEx(new System.Numerics.Vector2(40, 54), new System.Numerics.Vector2(windowWidth - 40, 54), 1.5f, new Color(50, 50, 60, 255));

            return interaction;
        }
    }
}
