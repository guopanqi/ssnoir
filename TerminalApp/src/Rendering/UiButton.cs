using System.Numerics;
using Raylib_cs;
using SSNoir.Rendering;

namespace SSNoir.TerminalApp.Rendering
{
    public readonly struct UiButtonResult
    {
        public bool Hovered { get; init; }
        public bool Clicked { get; init; }
    }

    public static class UiButton
    {
        public static UiButtonResult Draw(
            Rectangle rect,
            string label,
            UiInteractionContext ui,
            bool enabled = true,
            int fontSize = 13,
            Color? baseBg = null,
            Color? hoverBg = null,
            Color? disabledBg = null,
            Color? baseBorder = null,
            Color? hoverBorder = null,
            Color? disabledBorder = null,
            Color? textCol = null,
            Color? disabledTextCol = null)
        {
            bool isInteractable = enabled && !ui.IsLocked;
            bool hover = ui.CanHover(rect) && isInteractable;
            bool clicked = ui.WasClicked(rect) && isInteractable;

            Color bg, border, text;
            if (!isInteractable)
            {
                bg = disabledBg ?? new Color(30, 30, 35, 255);
                border = disabledBorder ?? new Color(50, 50, 55, 255);
                text = disabledTextCol ?? new Color(90, 90, 100, 255);
            }
            else if (hover)
            {
                bg = hoverBg ?? new Color(40, 40, 55, 255);
                border = hoverBorder ?? Color.White;
                text = textCol ?? Color.White;
            }
            else
            {
                bg = baseBg ?? new Color(25, 25, 35, 255);
                border = baseBorder ?? new Color(50, 50, 70, 255);
                text = textCol ?? Color.White;
            }

            Raylib.DrawRectangleRounded(rect, 0.2f, 4, bg);
            Raylib.DrawRectangleRoundedLinesEx(rect, 0.2f, 4, 1.5f, border);

            int textW = FontManager.MeasureTextWidth(label, fontSize);
            FontManager.DrawText(label, rect.X + (rect.Width - textW) / 2f, rect.Y + (rect.Height - fontSize) / 2f + 2f, fontSize, text);

            return new UiButtonResult { Hovered = hover, Clicked = clicked };
        }
    }
}
