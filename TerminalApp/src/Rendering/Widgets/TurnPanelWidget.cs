using Raylib_cs;

namespace SSNoir.Rendering
{
    public static class TurnPanelWidget
    {
        public struct TurnPanelInteraction
        {
            public bool RestClicked;
            public bool ShouldClose;
        }

        public static TurnPanelInteraction Draw(System.Numerics.Vector2 mousePos, float windowWidth, float windowHeight, bool skipOutsideClose = false)
        {
            var interaction = new TurnPanelInteraction
            {
                RestClicked = false,
                ShouldClose = false
            };

            float handY = windowHeight - 100;
            float panelW = 120f;
            float panelH = 72f;
            float panelX = windowWidth - panelW - 20f;
            float panelY = handY - panelH - 8f;
            var panelRect = new Rectangle(panelX, panelY, panelW, panelH);

            Raylib.DrawRectangleRounded(panelRect, 0.2f, 4, new Color(20, 20, 28, 255));
            Raylib.DrawRectangleRoundedLinesEx(panelRect, 0.2f, 4, 1.5f, new Color(70, 70, 95, 255));

            FontManager.DrawText("回合", panelX + 14, panelY + 10, 13, new Color(150, 150, 170, 255));

            float restX = panelX + 14;
            float restY = panelY + 32;
            var restRect = new Rectangle(restX, restY, panelW - 28, 28);
            bool restHover = Raylib.CheckCollisionPointRec(mousePos, restRect);

            Color restBg = restHover ? new Color(120, 50, 50, 255) : new Color(85, 30, 30, 255);
            Color restBorder = restHover ? new Color(220, 100, 100, 255) : new Color(140, 60, 60, 255);

            Raylib.DrawRectangleRounded(restRect, 0.2f, 4, restBg);
            Raylib.DrawRectangleRoundedLinesEx(restRect, 0.2f, 4, 1.5f, restBorder);

            string restText = "休息";
            int restW = FontManager.MeasureTextWidth(restText, 14);
            FontManager.DrawText(restText, restX + ((panelW - 28) - restW) / 2f, restY + 7, 14, Color.White);

            if (restHover && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                interaction.RestClicked = true;
            }

            if (!skipOutsideClose
                && Raylib.IsMouseButtonPressed(MouseButton.Left)
                && !Raylib.CheckCollisionPointRec(mousePos, panelRect))
            {
                interaction.ShouldClose = true;
            }

            return interaction;
        }
    }
}
