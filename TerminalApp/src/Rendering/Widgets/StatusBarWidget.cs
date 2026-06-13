using System;
using System.Linq;
using Raylib_cs;
using SSNoir.Core;

namespace SSNoir.Rendering
{
    public static class StatusBarWidget
    {
        public static void Draw(PresentationSnapshot snapshot, float windowWidth, float windowHeight)
        {
            float statusY = windowHeight - 25;

            Raylib.DrawRectangle(0, (int)statusY, (int)windowWidth, 25, new Color(10, 10, 15, 255));
            Raylib.DrawLineEx(new System.Numerics.Vector2(0, statusY), new System.Numerics.Vector2(windowWidth, statusY), 1.5f, new Color(30, 30, 40, 255));

            FontManager.DrawText("健康: ", 30, statusY + 4, 13, new Color(200, 200, 220, 255));
            FontManager.DrawText($"{snapshot.Health}/{snapshot.MaxHealth}", 70, statusY + 4, 13, new Color(250, 100, 100, 255));

            FontManager.DrawText("物资: ", 130, statusY + 4, 13, new Color(200, 200, 220, 255));
            FontManager.DrawText($"{snapshot.Supplies}/{snapshot.MaxSupplies}", 170, statusY + 4, 13, new Color(230, 200, 50, 255));

            FontManager.DrawText("场景: ", 230, statusY + 4, 13, new Color(200, 200, 220, 255));
            FontManager.DrawText(snapshot.Location.ToUpper(), 270, statusY + 4, 13, new Color(100, 220, 100, 255));

            string tip = "提示: 点击骰子/物品选择，点击卡牌对应卡槽放入，右键取消选择。";
            FontManager.DrawText(tip, 380, statusY + 4, 12, new Color(140, 140, 160, 255));
        }
    }
}
