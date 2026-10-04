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
        /// 顶栏暗条上的文字开关（卷宗 / 成长 / 帮助 / 设置）。和地点牌同一族：
        /// 常态为微透暗底、细边与次级灰字；悬停或打开才提到主文字，
        /// 打开态多一层淡白底。点击仍按完整矩形判定。
        /// </summary>
        public static bool DrawTopTextToggle(Rect rect, string label, bool isOpen, IMGUIInteractionContext ui)
        {
            bool interactable = !ui.IsLocked;
            bool hover = interactable && ui.CanHover(rect);

            ContainerNodeDrawer.DrawPlateBase(rect);
            if (hover || isOpen)
            {
                IMGUIStyles.SetColor(new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b,
                    isOpen ? 0.12f : 0.08f));
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                IMGUIStyles.ResetColor();
            }

            var style = new GUIStyle(IMGUIStyles.StatusLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = IMGUIStyles.FontSize(14),
                wordWrap = false,
                clipping = TextClipping.Clip,
                normal = { textColor = (hover || isOpen) ? IMGUIStyles.TextPrimary : IMGUIStyles.TextSecondary },
            };
            int minSize = IMGUIStyles.FontSize(10);
            while (style.fontSize > minSize
                   && style.CalcSize(new GUIContent(label)).x > rect.width - 4f)
                style.fontSize--;
            IMGUIStyles.DrawLabel(rect, label, style);

            if (interactable && ui.WasTapped(rect))
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

        /// <summary>
        /// 顶栏主动作：返回。是玩家在这块 HUD 上最常点的按钮，
        /// 所以常态就铺实底——不用等悬停才现身，一眼要比旁边暗条上那排低频的
        /// 查阅型开关（DrawTopTextToggle，常态只是次级灰字）扎眼。
        /// 用纸白实底 + 墨字，不用金——金是留给"需要玩家关注/聚焦"的语义色
        /// （命中/结果/正在发生），这里只是导航动作，不该抢那个位置。
        /// </summary>
        public static bool DrawPrimary(Rect rect, string label, IMGUIInteractionContext ui, bool enabled = true)
        {
            bool isInteractable = enabled && !ui.IsLocked;
            bool isHovered = isInteractable && ui.CanHover(rect);
            bool isClicked = isInteractable && ui.WasTapped(rect);

            if (isClicked)
            {
                Event.current.Use();
            }

            Color bg = enabled
                ? new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, isHovered ? 0.92f : 0.85f)
                : new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.20f);
            IMGUIStyles.SetColor(bg);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            IMGUIStyles.ResetColor();

            var style = new GUIStyle(IMGUIStyles.StatusLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = IMGUIStyles.FontSize(14),
                normal = { textColor = enabled ? IMGUIStyles.PaperInk : IMGUIStyles.PaperTextDisabled },
            };
            IMGUIStyles.DrawLabel(rect, label, style);

            return enabled && isClicked;
        }
    }
}
