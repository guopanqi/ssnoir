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
        /// 顶栏上的面板开关（声誉 / 队伍 / 卷宗 / 设置 / 成长 / Debug / 帮助）。这些是低频、
        /// 查阅型的入口，故意画得比"返回"轻：常态不铺底，只留一圈淡描边，
        /// 不在视觉上跟"返回"抢注意力。悬停/展开时才提亮，给出"这里能点"的反馈。
        /// 打开时描边和字都转金：这是 HUD 上"这个面板开着"的统一说法。
        /// </summary>
        public static bool DrawHudToggle(Rect rect, string label, bool isOpen, IMGUIInteractionContext ui)
        {
            bool hover = !ui.IsLocked && ui.CanHover(rect);

            // 常态透明、不铺底；悬停给一点提示性的浅底；打开态维持原有的金色浅底。
            Color bg = isOpen
                ? new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.08f)
                : (hover ? new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.06f) : Color.clear);
            if (bg.a > 0f)
            {
                IMGUIStyles.SetColor(bg);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                IMGUIStyles.ResetColor();
            }

            Color accent = isOpen
                ? IMGUIStyles.Gold
                : new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, hover ? 0.55f : 0.22f);
            IMGUIStyles.DrawOutline(rect, 1f, accent);

            var style = new GUIStyle(IMGUIStyles.StatusLabel)
            {
                fontSize = IMGUIStyles.FontSize(13),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = isOpen ? IMGUIStyles.Gold : (hover ? IMGUIStyles.TextPrimary : IMGUIStyles.TextSecondary) },
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

        /// <summary>
        /// 顶栏主动作：返回。是玩家在这块 HUD 上最常点的按钮，
        /// 所以常态就铺实底——不用等悬停才现身，一眼要比旁边那排低频的
        /// 查阅型开关（DrawHudToggle，常态透明只描边）扎眼。
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
