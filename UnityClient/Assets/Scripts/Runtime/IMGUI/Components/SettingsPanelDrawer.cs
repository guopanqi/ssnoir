#nullable enable
using UnityEngine;

namespace SSNoir.IMGUI
{
    /// <summary>
    /// 玩家设置面板：界面尺寸、镜头动画、教程提示、背景音乐、音效、对白六项设置。
    /// 跟成长面板同一套「纸物件」形式：居中卡片 + 压暗背景，而不是挂在按钮下面的半透明下拉。
    /// </summary>
    public static class SettingsPanelDrawer
    {
        private const float PanelW = 420f;

        // ── 版面常量 ───────────────────────────────────────────────
        // 每一项设置只保留标题和控件。画布读数属于调试信息，不应该出现在玩家设置里；
        // 其他说明也不参与选择，删掉后面板更紧凑，各项设置仍保持同一套流式布局。
        private const float HeaderH = IMGUIStyles.ModalContentTop;
        private const float BlockTitleH = 20f;
        private const float TitleToControl = 8f;
        private const float BlockGap = 22f;
        private const float BottomPad = 24f;

        private static float ControlH => UIScale.TouchHeight(30f);
        private static float BlockH =>
            BlockTitleH + TitleToControl + ControlH;
        private static float PanelH => HeaderH + BlockH * 6f + BlockGap * 5f + BottomPad;

        private static bool _isOpen = false;

        public static bool IsOpen => _isOpen;

        public static (Rect ToggleRect, Rect PanelRect) GetRects(TopHudLayout topHud)
        {
            var toggleRect = topHud.SettingsToggle;
            Rect safe = UIScale.SafeArea;
            float panelW = Mathf.Min(PanelW, safe.width - 32f);
            float panelH = Mathf.Min(PanelH, safe.height - 24f);
            float panelX = safe.x + (safe.width - panelW) / 2f;
            float panelY = safe.y + (safe.height - panelH) / 2f;
            return (toggleRect, new Rect(panelX, panelY, panelW, panelH));
        }

        public static void Draw(IMGUIInteractionContext ui, TopHudLayout topHud)
        {
            var (toggleRect, panelRect) = GetRects(topHud);

            // 顶栏开关和卷宗 / 成长 / 帮助 / 调试共用同一份实现，五个长得一模一样。
            if (IMGUIButton.DrawTopTextToggle(toggleRect, "设置", _isOpen, ui))
                _isOpen = !_isOpen;

            if (!_isOpen) return;

            if (IMGUIStyles.DrawModalChrome(panelRect, "设 置", ui))
            {
                _isOpen = false;
                return;
            }

            float panelX = panelRect.x;
            float panelW = panelRect.width;
            float panelY = panelRect.y;

            // 面板内是自上而下的流式布局：每一项都从上一项的底边往下接，不各写各的绝对 y。
            float contentX = panelX + 24f;
            float contentW = panelW - 48f;
            float y = panelY + HeaderH;

            // ── 界面尺寸 ──
            var presets = new[] { UISizePreset.Compact, UISizePreset.Standard, UISizePreset.Large };
            int presetIndex = System.Array.IndexOf(presets, UIScale.SizePreset);
            int pickedPreset = DrawSetting(
                new Rect(contentX, y, contentW, BlockH),
                "界面尺寸",
                new[] { "紧凑", "标准", "放大" }, Mathf.Max(0, presetIndex), ui);
            if (pickedPreset >= 0)
                UIScale.SizePreset = presets[pickedPreset];
            y += BlockH + BlockGap;

            // ── 镜头动画 ──
            // 原来这一项叫「减少动画」，控件上写「已开启」——标题说的是要做的事，
            // 按钮说的却是当前状态，两句话方向相反，读的人得在脑子里绕一圈。
            // 现在和上面一项同构：标题是这项设置的名字，控件直接摆出两个可选的档。
            bool reduceMotion = MotionSettings.ReduceMotion;
            int pickedMotion = DrawSetting(
                new Rect(contentX, y, contentW, BlockH),
                "镜头动画",
                new[] { "正常", "减少" }, reduceMotion ? 1 : 0, ui);
            if (pickedMotion >= 0)
                MotionSettings.ReduceMotion = pickedMotion == 1;
            y += BlockH + BlockGap;

            // ── 教程提示 ──
            // 「关」之后什么都不会弹（帮助面板照样能翻）。从关切回开会把「看过」的记录清掉，
            // 于是换一个人坐下来试玩，教程会从头再走一遍——demo 阶段这是最常用的一个动作。
            int pickedTutorial = DrawSetting(
                new Rect(contentX, y, contentW, BlockH),
                "教程提示",
                new[] { "开", "关" }, TutorialState.Enabled ? 0 : 1, ui);
            if (pickedTutorial >= 0)
                TutorialState.Enabled = pickedTutorial == 0;
            y += BlockH + BlockGap;

            // ── 背景音乐 / 音效 / 对白 ──
            // 四档分段，值走 AudioVolumes，跟着存档，和 reduceMotion 同一套规矩。
            // 默认：背景音乐低、音效低、对白关。
            string[] volumeOptions = { "关", "低", "中", "高" };
            int pickedMusic = DrawSetting(
                new Rect(contentX, y, contentW, BlockH),
                "背景音乐", volumeOptions, AudioVolumes.MusicLevelIndex, ui);
            if (pickedMusic >= 0)
                AudioVolumes.SetMusicLevel(pickedMusic);
            y += BlockH + BlockGap;

            int pickedSfx = DrawSetting(
                new Rect(contentX, y, contentW, BlockH),
                "音效", volumeOptions, AudioVolumes.SfxLevelIndex, ui);
            if (pickedSfx >= 0)
                AudioVolumes.SetSfxLevel(pickedSfx);
            y += BlockH + BlockGap;

            int pickedDialogue = DrawSetting(
                new Rect(contentX, y, contentW, BlockH),
                "对白", volumeOptions, AudioVolumes.DialogueLevelIndex, ui);
            if (pickedDialogue >= 0)
                AudioVolumes.SetDialogueLevel(pickedDialogue);
        }

        // 一项设置：标题 / 一排分段控件。返回被点中的段序号，没点中返回 -1。
        // 各项设置共用它，所以它们在版面上长得一模一样。
        private static int DrawSetting(
            Rect rect, string title, string[] options, int selected,
            IMGUIInteractionContext ui)
        {
            var titleStyle = new GUIStyle(IMGUIStyles.ModalBody);
            IMGUIStyles.ApplyStrongFont(titleStyle);
            IMGUIStyles.DrawLabel(
                new Rect(rect.x, rect.y, rect.width, BlockTitleH), title, titleStyle);

            float controlY = rect.y + BlockTitleH + TitleToControl;
            return DrawSegmented(
                new Rect(rect.x, controlY, rect.width, ControlH), options, selected, ui);
        }

        // 分段控件：等宽格子拼成一条，选中那格是实心墨底。
        private static int DrawSegmented(
            Rect rect, string[] options, int selected, IMGUIInteractionContext ui)
        {
            const float gap = 8f;
            float cellW = (rect.width - gap * (options.Length - 1)) / options.Length;
            int picked = -1;
            for (int i = 0; i < options.Length; i++)
            {
                var cell = new Rect(rect.x + i * (cellW + gap), rect.y, cellW, rect.height);
                if (DrawPaperButton(cell, options[i], ui, i == selected))
                    picked = i;
            }
            return picked;
        }

        // 纸上按钮。选中＝实心墨底 + 纸白字，未选中＝细描边 + 次级墨字。
        //
        // 原来两种状态都是"金字配细框"，而金 #E8C35A 压在纸白 #EFEAE0 上对比只有约 1.6:1——
        // 整张面板最该一眼看见的当前选项，反倒是最虚的两处。改成实心还顺手解决另一件事：
        // 这张纸上原本没有任何成片的深色，眼睛没有锚点，通篇就读成一片灰雾。
        private static bool DrawPaperButton(
            Rect rect, string label, IMGUIInteractionContext ui, bool selected)
        {
            bool interactable = !ui.IsLocked;
            bool hover = interactable && ui.CanHover(rect);
            var snapped = UIScale.PixelSnap(rect);

            if (selected)
            {
                GUI.color = IMGUIStyles.PaperInk;
                GUI.DrawTexture(snapped, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            else if (hover)
            {
                GUI.color = new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.08f);
                GUI.DrawTexture(snapped, Texture2D.whiteTexture);
                GUI.color = Color.white;
                IMGUIStyles.DrawOutline(snapped, 1f, IMGUIStyles.PaperInk);
            }
            else
            {
                IMGUIStyles.DrawOutline(snapped, 1f,
                    new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.35f));
            }

            var style = new GUIStyle(IMGUIStyles.ModalBody)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = selected ? IMGUIStyles.Paper : IMGUIStyles.PaperTextSecondary },
            };
            IMGUIStyles.ApplyStrongFont(style);
            IMGUIStyles.DrawLabel(rect, label, style);

            if (interactable && ui.WasTapped(rect))
            {
                Event.current.Use();
                return true;
            }
            return false;
        }

        public static void Close() { _isOpen = false; }

        public static void Reset() { Close(); }
    }
}
