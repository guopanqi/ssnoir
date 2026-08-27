using UnityEngine;

namespace SSNoir.IMGUI
{
    public static class IMGUIButton
    {
        public static bool Draw(
            Rect rect,
            string label,
            IMGUIInteractionContext ui,
            Color outlineColor,
            Color hoverBgColor,
            GUIStyle style,
            bool enabled = true)
        {
            bool isInteractable = enabled && !ui.IsLocked;
            bool isHovered = isInteractable && ui.CanHover(rect);
            bool isClicked = isInteractable && ui.WasTapped(rect);

            // Buttons own the pointer event they report. Without consuming it
            // here, a caller that changes the view (for example, Back) leaves
            // the same MouseDown available to widgets drawn later in OnGUI.
            if (isClicked)
            {
                Event.current.Use();
            }

            return IMGUIStyles.DrawTechnicalButton(
                rect,
                label,
                isHovered,
                isClicked,
                outlineColor,
                hoverBgColor,
                style,
                isInteractable);
        }

        /// <summary>
        /// 顶栏上的面板开关（声誉 / 队伍 / 卷宗 / 设置）。四个必须长得一模一样，
        /// 所以只有这一份实现——之前队伍那个是另写的，用了 ExecuteLabel、也没铺 HUD 底，
        /// 排在其余三个旁边一眼就看得出不是一伙的。
        /// 打开时描边和字都转金：这是 HUD 上"这个面板开着"的统一说法。
        /// </summary>
        public static bool DrawHudToggle(Rect rect, string label, bool isOpen, IMGUIInteractionContext ui)
        {
            GUI.color = isOpen
                ? new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.08f)
                : IMGUIStyles.HudBg;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            bool hover = !ui.IsLocked && ui.CanHover(rect);
            Color accent = isOpen
                ? IMGUIStyles.Gold
                : new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, hover ? 0.75f : 0.40f);
            IMGUIStyles.DrawOutline(rect, 1f, accent);

            var style = new GUIStyle(IMGUIStyles.StatusLabel)
            {
                fontSize = IMGUIStyles.FontSize(13),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = isOpen ? IMGUIStyles.Gold : IMGUIStyles.TextPrimary },
            };
            IMGUIStyles.DrawLabel(rect, label, style);

            if (!ui.IsLocked && ui.WasTapped(rect))
            {
                Event.current.Use();
                return true;
            }
            return false;
        }

        // 默认 HUD 按钮：黑底白字，1px Paper 40% 描边，悬停提亮
        public static bool Draw(Rect rect, string label, IMGUIInteractionContext ui, bool enabled = true)
        {
            return Draw(rect, label, ui,
                new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f),
                new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.08f),
                IMGUIStyles.ExecuteLabel, enabled);
        }
    }
}
