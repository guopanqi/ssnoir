#nullable enable
using UnityEngine;

namespace SSNoir.IMGUI
{
    /// <summary>
    /// 帮助面板：把教程条目一页一页翻着看。
    ///
    /// 内容读的就是 <see cref="TutorialLibrary"/>——和主动弹出来的那些是同一份文案。
    /// 没触发过的条目这里也能看：demo 阶段不锁，人想先看看这游戏怎么玩是合理的。
    /// </summary>
    public static class HelpPanelDrawer
    {
        private const float PanelW = 440f;
        private const float PadX = 24f;
        private const float ParagraphGap = 12f;
        private const float ButtonH = 34f;
        private const float BottomPad = 20f;
        private const float FooterGap = 14f;

        private static bool _isOpen;
        private static int _page;

        public static bool IsOpen => _isOpen;

        public static void Close() => _isOpen = false;

        public static void Reset()
        {
            _isOpen = false;
            _page = 0;
        }

        public static (Rect ToggleRect, Rect PanelRect) GetRects(TopHudLayout topHud)
        {
            Rect safe = UIScale.SafeArea;
            float w = Mathf.Min(PanelW, safe.width - 32f);
            float h = Mathf.Min(MeasureHeight(w), safe.height - 24f);
            var panel = new Rect(safe.x + (safe.width - w) / 2f, safe.y + (safe.height - h) / 2f, w, h);
            return (topHud.HelpToggle, UIScale.PixelSnap(panel));
        }

        // 面板高度按**最长的一条**定，不按当前页——翻页时纸的大小跳来跳去，读的人会以为换了个东西。
        private static float MeasureHeight(float panelW)
        {
            var body = BodyStyle();
            float textW = panelW - PadX * 2f;
            float tallest = 0f;
            foreach (var entry in TutorialLibrary.Entries)
            {
                float h = 0f;
                foreach (var paragraph in entry.Paragraphs)
                    h += body.CalcHeight(new GUIContent(paragraph), textW) + ParagraphGap;
                tallest = Mathf.Max(tallest, h);
            }
            return IMGUIStyles.ModalContentTop + tallest + FooterGap + ButtonH + BottomPad;
        }

        private static GUIStyle BodyStyle() => new GUIStyle(IMGUIStyles.ModalBody)
        {
            fontSize = IMGUIStyles.FontSize(15),
            alignment = TextAnchor.UpperLeft,
            wordWrap = true,
        };

        public static void Draw(IMGUIInteractionContext ui, TopHudLayout topHud)
        {
            var (toggleRect, panel) = GetRects(topHud);

            if (IMGUIButton.DrawTopTextToggle(toggleRect, UiText.Get("帮助"), _isOpen, ui))
            {
                _isOpen = !_isOpen;
                if (_isOpen) _page = 0;
            }

            if (!_isOpen) return;

            var entries = TutorialLibrary.Entries;
            if (entries.Length == 0) { _isOpen = false; return; }
            _page = Mathf.Clamp(_page, 0, entries.Length - 1);
            var entry = entries[_page];

            if (IMGUIStyles.DrawModalChrome(panel, entry.Title, ui))
            {
                _isOpen = false;
                return;
            }

            var body = BodyStyle();
            float textW = panel.width - PadX * 2f;
            float y = panel.y + IMGUIStyles.ModalContentTop;
            foreach (var paragraph in entry.Paragraphs)
            {
                float h = body.CalcHeight(new GUIContent(paragraph), textW);
                IMGUIStyles.DrawLabel(new Rect(panel.x + PadX, y, textW, h), paragraph, body);
                y += h + ParagraphGap;
            }

            // 页脚：上一页 / 第几页 / 下一页。页码在正中，两个按钮贴着纸的两边。
            float footerY = panel.yMax - BottomPad - ButtonH;
            var prevRect = UIScale.PixelSnap(new Rect(panel.x + PadX, footerY, 96f, ButtonH));
            var nextRect = UIScale.PixelSnap(new Rect(panel.xMax - PadX - 96f, footerY, 96f, ButtonH));

            if (TutorialPaper.DrawPrimaryButton(prevRect, "上一页", ui, _page > 0))
                _page--;
            if (TutorialPaper.DrawPrimaryButton(nextRect, "下一页", ui, _page < entries.Length - 1))
                _page++;

            var pageStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                fontSize = IMGUIStyles.FontSize(13),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = IMGUIStyles.PaperTextSecondary },
            };
            IMGUIStyles.DrawLabel(
                new Rect(prevRect.xMax, footerY, nextRect.x - prevRect.xMax, ButtonH),
                $"{_page + 1} / {entries.Length}", pageStyle);
        }
    }
}
