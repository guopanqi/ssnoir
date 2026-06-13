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

        public static TurnPanelInteraction Draw(SSNoir.TerminalApp.Rendering.UiInteractionContext ui, float windowWidth, float windowHeight, bool skipOutsideClose = false)
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

            var restBtn = SSNoir.TerminalApp.Rendering.UiButton.Draw(restRect, "休息", ui, true, 14,
                new Color((byte)85, (byte)30, (byte)30, (byte)255), new Color((byte)120, (byte)50, (byte)50, (byte)255), null,
                new Color((byte)140, (byte)60, (byte)60, (byte)255), new Color((byte)220, (byte)100, (byte)100, (byte)255), null,
                Color.White, null);

            if (restBtn.Clicked)
            {
                interaction.RestClicked = true;
            }

            if (!skipOutsideClose
                && Raylib.IsMouseButtonPressed(MouseButton.Left)
                && !ui.IsLocked
                && !Raylib.CheckCollisionPointRec(ui.Mouse, panelRect))
            {
                interaction.ShouldClose = true;
            }

            return interaction;
        }
    }
}
