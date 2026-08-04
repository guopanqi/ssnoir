using System;
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

            // 伤势：完好时不报数字，只有真的带着伤才占位置。颜色按档位走，不按百分比。
            Color injuryColor;
            string injuryText;
            if (snapshot.InjurySeverity <= 0)
            {
                injuryColor = new Color(80, 220, 120, 255);  // 完好：Vibrant Emerald Green
                injuryText = "完好";
            }
            else if (!snapshot.InjuryCostsActionDie)
            {
                injuryColor = new Color(245, 175, 55, 255);  // 轻伤：Warm Amber/Orange
                injuryText = $"{snapshot.InjuryPart}伤 {snapshot.InjurySeverity}";
            }
            else
            {
                injuryColor = new Color(245, 80, 80, 255);   // 重伤：Critical Red
                injuryText = $"{snapshot.InjuryPart}伤 {snapshot.InjurySeverity}";
            }

            FontManager.DrawText("伤势: ", 30, statusY + 4, 13, new Color(200, 200, 220, 255));
            FontManager.DrawText(injuryText, 70, statusY + 4, 13, injuryColor);
            FontManager.DrawText("场景: ", 130, statusY + 4, 13, new Color(200, 200, 220, 255));
            FontManager.DrawText(snapshot.Location.ToUpper(), 170, statusY + 4, 13, new Color(100, 220, 100, 255));
        }
    }
}
