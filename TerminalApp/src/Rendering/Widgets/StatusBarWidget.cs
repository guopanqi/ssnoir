using System;
using System.Linq;
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

            int health = gameState.Team.Health;
            int maxHealth = gameState.Team.MaxHealth;
            int supplies = gameState.Team.Supplies;
            int maxSupplies = gameState.Team.MaxSupplies;
            string location = gameState.Get<string>("location");

            // Line 1: Health, Supplies, Location, Tips
            FontManager.DrawText("健康: ", 30, statusY + 4, 13, new Color(200, 200, 220, 255));
            FontManager.DrawText($"{health}/{maxHealth}", 70, statusY + 4, 13, new Color(250, 100, 100, 255));

            FontManager.DrawText("物资: ", 130, statusY + 4, 13, new Color(200, 200, 220, 255));
            FontManager.DrawText($"{supplies}/{maxSupplies}", 170, statusY + 4, 13, new Color(230, 200, 50, 255));

            FontManager.DrawText("场景: ", 230, statusY + 4, 13, new Color(200, 200, 220, 255));
            FontManager.DrawText(location.ToUpper(), 270, statusY + 4, 13, new Color(100, 220, 100, 255));

            string tip = "提示: 点击骰子/物品选择，点击卡牌对应卡槽放入，右键取消选择。";
            FontManager.DrawText(tip, 380, statusY + 4, 12, new Color(140, 140, 160, 255));
        }

        private static string MapStatLabel(string stat)
        {
            switch (stat.ToLowerInvariant())
            {
                case "force": return "暴";
                case "wit": return "智";
                case "charm": return "魅";
                case "agility": return "敏";
                default: return stat;
            }
        }
    }
}
