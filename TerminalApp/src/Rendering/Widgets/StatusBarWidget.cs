using Raylib_cs;
using SSNoir.Core;

namespace SSNoir.Rendering
{
    public static class StatusBarWidget
    {
        public static void Draw(GameState gameState, float windowWidth, float windowHeight)
        {
            float statusY = windowHeight - 25;

            // Draw Status Bar background (height 25)
            Raylib.DrawRectangle(0, (int)statusY, (int)windowWidth, 25, new Color(10, 10, 15, 255));
            Raylib.DrawLineEx(new System.Numerics.Vector2(0, statusY), new System.Numerics.Vector2(windowWidth, statusY), 1.5f, new Color(30, 30, 40, 255));

            int health = gameState.Get<int>("health");
            string location = gameState.Get<string>("location");

            FontManager.DrawText("健康: ", 30, statusY + 5, 13, new Color(200, 200, 220, 255));
            FontManager.DrawText($"{health}%", 70, statusY + 5, 13, new Color(250, 100, 100, 255));

            FontManager.DrawText("场景: ", 160, statusY + 5, 13, new Color(200, 200, 220, 255));
            FontManager.DrawText(location.ToUpper(), 200, statusY + 5, 13, new Color(100, 220, 100, 255));

            // Help tip
            string tip = "提示: 点击手牌选择，点击卡槽放入，右键取消选择。";
            FontManager.DrawText(tip, 320, statusY + 5, 12, new Color(140, 140, 160, 255));
        }
    }
}
