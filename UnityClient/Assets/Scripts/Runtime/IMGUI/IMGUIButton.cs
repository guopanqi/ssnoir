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
