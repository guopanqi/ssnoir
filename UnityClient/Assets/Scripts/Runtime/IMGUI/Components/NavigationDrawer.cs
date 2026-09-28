#nullable enable
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    public static class NavigationDrawer
    {
        public static void Draw(SSNoirGameManager gameManager, IMGUIInteractionContext ui, TopHudLayout topHud)
        {
            // Return button
            if (gameManager.NavigationStack.Count > 0 || !string.IsNullOrEmpty(gameManager.FocusedNodeName))
            {
                var returnRect = topHud.Back;

                // 顶栏最高频的动作，用 DrawPrimary：常态纸白实底，比暗条上那排低频的
                // 查阅型开关（DrawTopTextToggle，常态只是次级灰字）扎眼，不用等悬停才被注意到。
                if (IMGUIButton.DrawPrimary(returnRect, UiText.Get("< 返 回"), ui))
                {
                    gameManager.GoBackNavigation();
                }
            }

            // Breadcrumb
            // 「当前位置:」是纯损耗——面包屑本身已经说明了它是什么。
            string breadcrumbText = string.Empty;
            string rootName = gameManager.DisplayedSnapshot.RootNode?.DisplayTitle
                ?? UiText.Get("未加载");
            if (gameManager.NavigationStack.Count == 0)
            {
                breadcrumbText += rootName;
            }
            else
            {
                breadcrumbText += rootName + " > " + string.Join(" > ",
                    gameManager.NavigationStack.ConvertAll(n => n.DisplayTitle));
            }

            var crumbStyle = new GUIStyle(IMGUIStyles.StatusLabel);
            crumbStyle.normal.textColor = IMGUIStyles.TextSecondary;
            crumbStyle.fontSize = IMGUIStyles.FontSize(14);
            breadcrumbText = FitTextWithEllipsis(breadcrumbText, topHud.Breadcrumb.width, crumbStyle);
            IMGUIStyles.DrawLabel(topHud.Breadcrumb, breadcrumbText, crumbStyle);

            var dayStyle = new GUIStyle(IMGUIStyles.StatusLabel);
            dayStyle.normal.textColor = IMGUIStyles.TextPrimary;
            dayStyle.fontSize = IMGUIStyles.FontSize(14);
            dayStyle.alignment = TextAnchor.MiddleCenter;
            IMGUIStyles.DrawLabel(topHud.Day,
                UiText.Day(gameManager.DisplayedSnapshot.WorldDay), dayStyle);
        }

        private static string FitTextWithEllipsis(string text, float maxWidth, GUIStyle style)
        {
            if (style.CalcSize(new GUIContent(text)).x <= maxWidth) return text;

            const string ellipsis = "…";
            int length = text.Length;
            while (length > 0 && style.CalcSize(new GUIContent(text.Substring(0, length) + ellipsis)).x > maxWidth)
                length--;
            return length > 0 ? text.Substring(0, length) + ellipsis : string.Empty;
        }

    }
}
