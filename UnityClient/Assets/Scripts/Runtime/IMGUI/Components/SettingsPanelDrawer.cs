#nullable enable
using UnityEngine;

namespace SSNoir.IMGUI
{
    /// <summary>
    /// 玩家设置面板。目前只有一项「减少动画」，但它是玩家设置而不是调试拨杆，
    /// 所以有自己的入口，不挂在 Debug 面板下面——那个面板迟早要拿掉。
    /// </summary>
    public static class SettingsPanelDrawer
    {
        private const float PanelWidth = 268f;
        private const float RowHeight = 26f;

        private static bool _isOpen = false;

        public static bool IsOpen => _isOpen;

        public static (Rect ToggleRect, Rect PanelRect) GetRects(TopHudLayout topHud)
        {
            var toggleRect = topHud.SettingsToggle;

            float panelX = toggleRect.xMax - PanelWidth;
            float panelY = toggleRect.yMax + 4f;
            // 8 上边距 + 分节标题 + 一行开关 + 两行说明 + 8 下边距。
            float panelH = 8f + 20f + RowHeight + 34f + 8f;
            return (toggleRect, new Rect(panelX, panelY, PanelWidth, panelH));
        }

        public static void Draw(IMGUIInteractionContext ui, TopHudLayout topHud)
        {
            var (toggleRect, panelRect) = GetRects(topHud);

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

            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                font = IMGUIStyles.ChineseFont,
                fontSize = IMGUIStyles.FontSize(13),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _isOpen ? IMGUIStyles.Gold : IMGUIStyles.TextPrimary }
            };
            GUI.Label(toggleRect, "设置", labelStyle);

            if (ui.WasClicked(toggleRect))
            {
                _isOpen = !_isOpen;
                Event.current.Use();
            }

            if (!_isOpen) return;

            GUI.color = new Color(IMGUIStyles.HudBg.r, IMGUIStyles.HudBg.g, IMGUIStyles.HudBg.b, IMGUIStyles.ModalOpacity);
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(panelRect, 1f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f));

            float panelX = panelRect.x;
            float panelW = panelRect.width;
            var mutedStyle = new GUIStyle(labelStyle)
            {
                fontSize = IMGUIStyles.FontSize(11),
                normal = { textColor = IMGUIStyles.TextDisabled }
            };

            float curY = panelRect.y + 8f;
            GUI.Label(new Rect(panelX + 8f, curY + 2f, panelW, 18f), "画面", mutedStyle);
            curY += 20f;

            bool reduceMotion = MotionSettings.ReduceMotion;
            var rowLabelStyle = new GUIStyle(labelStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = IMGUIStyles.TextPrimary }
            };
            GUI.Label(new Rect(panelX + 8f, curY + 2f, 120f, 22f), "减少动画", rowLabelStyle);

            var switchStyle = new GUIStyle(labelStyle)
            {
                normal = { textColor = reduceMotion ? IMGUIStyles.Gold : IMGUIStyles.TextSecondary }
            };
            var switchRect = new Rect(panelX + panelW - 8f - 64f, curY + 2f, 64f, 22f);
            if (IMGUIButton.Draw(switchRect, reduceMotion ? "已开启" : "已关闭", ui,
                    reduceMotion ? IMGUIStyles.Gold : new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f),
                    new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.08f), switchStyle))
            {
                MotionSettings.ReduceMotion = !reduceMotion;
                Event.current.Use();
            }

            curY += RowHeight;

            var hintStyle = new GUIStyle(mutedStyle)
            {
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
            GUI.Label(new Rect(panelX + 8f, curY + 2f, panelW - 16f, 34f),
                "镜头不再推近旋转，改为快速淡入淡出。", hintStyle);

            // Close when clicking outside
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && !ui.IsLocked)
            {
                if (!panelRect.Contains(ui.Mouse) && !toggleRect.Contains(ui.Mouse))
                {
                    _isOpen = false;
                    Event.current.Use();
                }
            }
        }

        public static void Reset() { _isOpen = false; }
    }
}
