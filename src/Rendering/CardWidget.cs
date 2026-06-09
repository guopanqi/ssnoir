using System;
using Raylib_cs;

namespace SSNoir.Rendering
{
    public static class CardWidget
    {
        public static bool DrawCard(Rectangle bounds, string name, string typeLabel, bool isHovered)
        {
            Color bgColor = isHovered ? new Color(50, 50, 70, 255) : new Color(30, 30, 40, 255);
            Color outlineColor = isHovered ? new Color(130, 130, 220, 255) : new Color(60, 60, 80, 255);
            Color titleColor = isHovered ? Color.White : new Color(200, 200, 200, 255);
            Color typeColor = isHovered ? new Color(150, 150, 230, 255) : new Color(110, 110, 130, 255);

            // Draw Card Background
            Raylib.DrawRectangleRounded(bounds, 0.1f, 8, bgColor);
            Raylib.DrawRectangleRoundedLinesEx(bounds, 0.1f, 8, 2f, outlineColor);

            // Draw Title text (centered)
            int titleFontSize = 20;
            int titleWidth = FontManager.MeasureTextWidth(name, titleFontSize);
            float titleX = bounds.X + (bounds.Width - titleWidth) / 2f;
            float titleY = bounds.Y + (bounds.Height / 2f) - 15;
            FontManager.DrawText(name, titleX, titleY, titleFontSize, titleColor);

            // Draw Type text (bottom-center)
            int typeFontSize = 15;
            int typeWidth = FontManager.MeasureTextWidth(typeLabel, typeFontSize);
            float typeX = bounds.X + (bounds.Width - typeWidth) / 2f;
            float typeY = bounds.Y + bounds.Height - 22;
            FontManager.DrawText(typeLabel, typeX, typeY, typeFontSize, typeColor);

            // Return if clicked
            return isHovered && Raylib.IsMouseButtonPressed(MouseButton.Left);
        }
    }
}
