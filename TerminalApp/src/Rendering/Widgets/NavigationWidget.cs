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
            float startY = 16f;
            const float returnX = 40f;
            const float breadcrumbX = 132f;
            // 顶栏右侧控件从约 x=478 开始；当前位置与日期必须在它之前占用固定槽位，
            // 否则后绘制的关系面板会把日期完整盖住。
            const float breadcrumbWidth = 242f;
            const float dayX = 390f;

            if (state.NavigationStack.Count > 0)
            {
                var returnRect = new Rectangle(returnX, startY, 78, 26);
                
                var btn = SSNoir.TerminalApp.Rendering.UiButton.Draw(returnRect, "< 返回", ui, true, 13,
                    TerminalPalette.SurfaceRaised, TerminalPalette.AccentDark, null,
                    TerminalPalette.Accent, TerminalPalette.AccentBright, null,
                    TerminalPalette.Text);

                if (btn.Clicked)
                {
                    interaction.GoBackClicked = true;
                }
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
            breadcrumbText = FitTextWithEllipsis(breadcrumbText, breadcrumbWidth, 13);
            FontManager.DrawText(breadcrumbText, breadcrumbX, startY + 6, 13, new Color(180, 180, 200, 255));
            FontManager.DrawText($"第 {state.DisplayedSnapshot.WorldDay} 天", dayX, startY + 6, 13,
                new Color(210, 205, 235, 255));

            Raylib.DrawLineEx(new System.Numerics.Vector2(40, 54), new System.Numerics.Vector2(windowWidth - 40, 54), 1.5f, new Color(50, 50, 60, 255));

            return interaction;
        }

        private static string FitTextWithEllipsis(string text, float maxWidth, int fontSize)
        {
            if (FontManager.MeasureTextWidth(text, fontSize) <= maxWidth) return text;

            const string ellipsis = "…";
            int length = text.Length;
            while (length > 0 && FontManager.MeasureTextWidth(text[..length] + ellipsis, fontSize) > maxWidth)
                length--;
            return length > 0 ? text[..length] + ellipsis : string.Empty;
        }
    }
}
