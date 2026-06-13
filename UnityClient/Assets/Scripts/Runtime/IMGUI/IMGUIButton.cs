using UnityEngine;
using SSNoir.UnityClient.IMGUI.Styles;

namespace SSNoir.UnityClient.IMGUI
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
            bool isClicked = isInteractable && ui.WasClicked(rect);
            
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

        public static bool Draw(Rect rect, string label, IMGUIInteractionContext ui, bool enabled = true)
        {
            return Draw(rect, label, ui, IMGUIStyles.PrimaryColor, IMGUIStyles.ExecuteBtnHover, IMGUIStyles.ExecuteLabel, enabled);
        }
    }
}
