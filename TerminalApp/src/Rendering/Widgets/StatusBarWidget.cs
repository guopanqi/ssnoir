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

            // Dynamic Health Color
            Color healthColor;
            float healthPct = snapshot.MaxHealth > 0 ? (float)snapshot.Health / snapshot.MaxHealth : 0f;
            if (healthPct >= 0.75f)
            {
                healthColor = new Color(80, 220, 120, 255); // Safe: Vibrant Emerald Green
            }
            else if (healthPct >= 0.4f)
            {
                healthColor = new Color(245, 175, 55, 255); // Warning: Warm Amber/Orange
            }
            else
            {
                healthColor = new Color(245, 80, 80, 255); // Urgent: Critical Red
            }

            // Dynamic Satiety Color
            Color satietyColor;
            float satietyPct = snapshot.MaxSatiety > 0 ? (float)snapshot.Satiety / snapshot.MaxSatiety : 0f;
            if (satietyPct >= 0.65f)
            {
                satietyColor = new Color(80, 220, 120, 255); // Safe: Vibrant Emerald Green
            }
            else if (satietyPct >= 0.3f)
            {
                satietyColor = new Color(245, 175, 55, 255); // Warning: Warm Amber/Orange
            }
            else
            {
                satietyColor = new Color(245, 80, 80, 255); // Urgent: Critical Red
            }

            FontManager.DrawText("健康: ", 30, statusY + 4, 13, new Color(200, 200, 220, 255));
            FontManager.DrawText($"{snapshot.Health}/{snapshot.MaxHealth}", 70, statusY + 4, 13, healthColor);

            FontManager.DrawText("饱腹: ", 130, statusY + 4, 13, new Color(200, 200, 220, 255));
            FontManager.DrawText($"{snapshot.Satiety}/{snapshot.MaxSatiety}", 170, statusY + 4, 13, satietyColor);

            FontManager.DrawText("场景: ", 230, statusY + 4, 13, new Color(200, 200, 220, 255));
            FontManager.DrawText(snapshot.Location.ToUpper(), 270, statusY + 4, 13, new Color(100, 220, 100, 255));
        }
    }
}
