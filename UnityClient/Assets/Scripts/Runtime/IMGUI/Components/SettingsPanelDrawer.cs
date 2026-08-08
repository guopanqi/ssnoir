#nullable enable
using UnityEngine;

namespace SSNoir.IMGUI
{
    /// <summary>
    /// 玩家设置面板。目前只有一项「减少动画」，但它是玩家设置而不是调试拨杆，
    /// 所以有自己的入口，不挂在 Debug 面板下面——那个面板迟早要拿掉。
    /// 跟成长面板同一套「纸物件」形式：居中卡片 + 压暗背景，而不是挂在按钮下面的半透明下拉。
    /// </summary>
    public static class SettingsPanelDrawer
    {
        private const float PanelW = 380f;
        private const float PanelH = 200f;

        private static bool _isOpen = false;

        public static bool IsOpen => _isOpen;

        public static (Rect ToggleRect, Rect PanelRect) GetRects(TopHudLayout topHud)
        {
            var toggleRect = topHud.SettingsToggle;
            float panelX = (UIScale.VW - PanelW) / 2f;
            float panelY = (UIScale.VH - PanelH) / 2f;
            return (toggleRect, new Rect(panelX, panelY, PanelW, PanelH));
        }

        public static void Draw(IMGUIInteractionContext ui, TopHudLayout topHud)
        {
            var (toggleRect, panelRect) = GetRects(topHud);

            // 顶部常驻入口按钮，样式与位置不变。
            Color toggleBg = _isOpen
                ? new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.08f)
                : IMGUIStyles.HudBg;
            Color toggleBorder = _isOpen
                ? IMGUIStyles.Gold
                : new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f);
            GUI.color = toggleBg;
            GUI.DrawTexture(toggleRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(toggleRect, 1f, toggleBorder);

            var toggleLabelStyle = new GUIStyle(GUI.skin.label)
            {
                font = IMGUIStyles.ChineseFont,
                fontSize = IMGUIStyles.FontSize(13),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _isOpen ? IMGUIStyles.Gold : IMGUIStyles.TextPrimary }
            };
            GUI.Label(toggleRect, "设置", toggleLabelStyle);

            if (ui.WasClicked(toggleRect))
            {
                _isOpen = !_isOpen;
                Event.current.Use();
            }

            if (!_isOpen) return;

            // 先压暗世界背景，再摆一张接近不透明的纸——跟成长面板同一套模态惯例。
            GUI.color = IMGUIStyles.Blocker;
            GUI.DrawTexture(new Rect(0, 0, UIScale.VW, UIScale.VH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            IMGUIStyles.DrawShadow(panelRect, new Vector2(5f, 6f), 0.50f);
            GUI.color = IMGUIStyles.ModalBg;
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            float panelX = panelRect.x;
            float panelY = panelRect.y;
            float panelW = panelRect.width;

            // Main Title（纸上墨字，跟成长面板同一套排版）
            GUI.Label(new Rect(panelX + 24f, panelY + 20f, 160f, 28f), "设 置", IMGUIStyles.ModalTitle);

            // Close [X]：纸上次级按钮 = 1px 黑描边透明底
            float closeX = panelX + panelW - 44f;
            float closeY = panelY + 16f;
            var closeRect = new Rect(closeX, closeY, 28f, 28f);
            bool closeHover = ui.CanHover(closeRect);

            if (closeHover)
            {
                GUI.color = new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.08f);
                GUI.DrawTexture(closeRect, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            IMGUIStyles.DrawOutline(closeRect, 1f, closeHover
                ? IMGUIStyles.PaperInk
                : new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.55f));

            var closeStyle = new GUIStyle(IMGUIStyles.StatusLabel);
            closeStyle.alignment = TextAnchor.MiddleCenter;
            closeStyle.normal.textColor = closeHover ? IMGUIStyles.PaperInk : IMGUIStyles.PaperTextSecondary;
            GUI.Label(closeRect, "X", closeStyle);

            if (ui.WasClicked(closeRect))
            {
                _isOpen = false;
                Event.current.Use();
            }

            // Divider（纸上单发丝线）
            IMGUIStyles.DrawLine(new Vector2(panelX + 24f, panelY + 58f), new Vector2(panelX + panelW - 24f, panelY + 58f),
                new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.35f), 1f);

            // Section label（纸上次级字）
            var sectionStyle = new GUIStyle(IMGUIStyles.SectionLabel);
            sectionStyle.normal.textColor = IMGUIStyles.PaperTextSecondary;
            GUI.Label(new Rect(panelX + 24f, panelY + 72f, 200f, 22f), "画面", sectionStyle);

            // 减少动画 row（跟成长面板同一处理：正文级功能文字换 Semibold，不用发丝的 Regular）
            float rowY = panelY + 102f;
            var rowLabelStyle = new GUIStyle(IMGUIStyles.ModalBody);
            IMGUIStyles.ApplyStrongFont(rowLabelStyle);
            GUI.Label(new Rect(panelX + 24f, rowY, 160f, 26f), "减少动画", rowLabelStyle);

            bool reduceMotion = MotionSettings.ReduceMotion;
            var switchStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = reduceMotion ? new Color(0.62f, 0.47f, 0.10f, 1f) : IMGUIStyles.PaperTextSecondary }
            };
            IMGUIStyles.ApplyStrongFont(switchStyle);
            var switchRect = new Rect(panelX + panelW - 24f - 72f, rowY - 2f, 72f, 26f);
            // "已关闭"用不透明的 PaperTextDisabled，不用 PaperInk 降透明度——同一张纸上
            // 降透明度的字会被纸底冲淡到快看不见，这一路踩过好几次坑了。
            if (IMGUIButton.Draw(switchRect, reduceMotion ? "已开启" : "已关闭", ui,
                    reduceMotion ? new Color(0.62f, 0.47f, 0.10f, 1f) : IMGUIStyles.PaperTextDisabled,
                    new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.08f), switchStyle))
            {
                MotionSettings.ReduceMotion = !reduceMotion;
            }

            // 说明文字
            var hintStyle = new GUIStyle(IMGUIStyles.SectionLabel)
            {
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                normal = { textColor = IMGUIStyles.PaperTextDisabled }
            };
            GUI.Label(new Rect(panelX + 24f, rowY + 32f, panelW - 48f, 40f),
                "镜头不再推近旋转，改为快速淡入淡出。", hintStyle);
        }

        public static void Close() { _isOpen = false; }

        public static void Reset() { Close(); }
    }
}
