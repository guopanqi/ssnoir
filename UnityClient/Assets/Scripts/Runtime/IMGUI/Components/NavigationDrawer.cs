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

                // 顶栏最高频的动作，用 DrawPrimary：常态金底金描边，比旁边一排
                // 查阅型开关（DrawHudToggle）扎眼，不用等悬停才被注意到。
                if (IMGUIButton.DrawPrimary(returnRect, "< 返 回", ui))
                {
                    gameManager.GoBackNavigation();
                }
            }

            // Breadcrumb
            // 「当前位置:」是纯损耗——面包屑本身已经说明了它是什么。
            string breadcrumbText = string.Empty;
            string rootName = gameManager.DisplayedSnapshot.RootNode?.Name ?? "未加载";
            if (gameManager.NavigationStack.Count == 0)
            {
                breadcrumbText += rootName;
            }
            else
            {
                breadcrumbText += rootName + " > " + string.Join(" > ", gameManager.NavigationStack.ConvertAll(n => n.Name));
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
            IMGUIStyles.DrawLabel(topHud.Day, $"第 {gameManager.DisplayedSnapshot.WorldDay} 天", dayStyle);

            // Relation Panel
            DrawRelationPanel(gameManager, ui, topHud);

            // Divider
            IMGUIStyles.DrawLine(
                new Vector2(topHud.Bar.xMin, topHud.DividerY),
                new Vector2(topHud.Bar.xMax, topHud.DividerY),
                new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.25f), 1f);
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

        // 声望档位配色（序号 0..5 对应 RelationScale.BandNames：敌视/冷淡/中立/相识/信任/核心）。
        // 沉着版：敌视=印章红，冷淡=陶红，中立=纸白次级，正面三档赭黄→中间→苔绿逐级抬亮。
        private static readonly Color[] RelationBandColors =
        {
            IMGUIStyles.SealRed,                                                       // 敌视
            IMGUIStyles.OddsFail,                                                      // 冷淡
            new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.60f), // 中立
            IMGUIStyles.OddsNeutral,                                                   // 相识
            Color.Lerp(IMGUIStyles.OddsNeutral, IMGUIStyles.OddsSuccess, 0.5f),        // 信任
            IMGUIStyles.OddsSuccess,                                                   // 核心
        };

        // 某圈子在指定档位序号上的显示名：正面三档取内容层定制称呼，其余用通用档名。
        private static string BandDisplay(PresentationSnapshot snapshot, string faction, int band)
        {
            if (band < 3) return RelationScale.BandNames[band];
            string tier = RelationScale.PositiveTiers[band - 3];
            return snapshot.RelationBandNames.TryGetValue($"{faction}:{tier}", out string name) ? name : tier;
        }

        /// <summary>
        /// 圈内声誉这套东西暂时整个收起来：顶栏不出按钮，面板也不画。
        ///
        /// 不是删掉——数值仍在跑（关系工作的好结果照样加），只是**现在还兑现不出什么**：
        /// 能靠它打开的门就那么几扇，玩家看着一块四条进度条的面板却找不到它有什么用，
        /// 只会以为自己漏掉了一个系统。等它真的有东西可换，把这个常量翻回 true 就回来了。
        /// 顶栏按钮的位置也一起让出去（见 TopHudLayout），不留一块恒定的空位——
        /// 恒定预留是给"有时候不画"的控件用的，这个是"这一版根本没有"。
        /// </summary>
        // static readonly 而不是 const：const false 会让编译器把后面整段判成不可达代码，
        // 于是这个开关自己制造一条警告。它是一个开关，不是一个常量事实。
        public static readonly bool ShowRelationPanel = false;

        private static bool _relationExpanded;
        public static bool IsRelationExpanded => ShowRelationPanel && _relationExpanded;
        public static void CollapseRelation() => _relationExpanded = false;
        // 圈子名单以 GameState.Circles 为唯一来源——面板不再自己维护一份。
        private static readonly string[] Factions = GameState.Circles;

        // 收起态留在导航栏；展开态是一张完整的声誉进展图，放到导航线下方。
        private static void DrawRelationPanel(SSNoirGameManager gameManager, IMGUIInteractionContext ui, TopHudLayout topHud)
        {
            if (!ShowRelationPanel) return;
            var toggleRect = topHud.RelationToggle;
            // 按钮上放不下两条轨道，只做入口——数字在展开的进展图里看。
            // 和队伍 / 卷宗 / 设置共用同一份顶栏开关实现。
            if (IMGUIButton.DrawHudToggle(toggleRect, "声 誉", _relationExpanded, ui))
                _relationExpanded = !_relationExpanded;
        }

        public static void DrawRelationOverlay(PresentationSnapshot snapshot, TopHudLayout topHud)
        {
            if (!ShowRelationPanel || !_relationExpanded) return;

            Rect safe = UIScale.SafeArea;
            float panelW = Mathf.Min(620f, topHud.Bar.width);
            // 高度按剩余竖直空间收敛：窄屏上 398 会直接顶穿屏幕底。
            float headerH = 50f;
            float available = Mathf.Max(0f, safe.yMax - 16f - topHud.ContentTop);
            float rowH = Mathf.Clamp((available - headerH) / Factions.Length, 96f, 112f);
            float panelH = headerH + rowH * Factions.Length;
            float panelY = Mathf.Max(topHud.ContentTop - Mathf.Max(0f, panelH - available), topHud.Bar.yMax);
            var panel = new Rect(topHud.Bar.xMax - panelW, panelY, panelW, panelH);
            GUI.color = new Color(IMGUIStyles.HudBg.r, IMGUIStyles.HudBg.g, IMGUIStyles.HudBg.b, 0.98f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(panel, 1f,
                new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.55f));

            var title = new GUIStyle(IMGUIStyles.CardTitle) { alignment = TextAnchor.MiddleLeft, fontSize = IMGUIStyles.FontSize(20) };
            IMGUIStyles.DrawLabel(new Rect(panel.x + 18f, panel.y + 12f, 130f, 28f), "城市声望", title);
            var note = new GUIStyle(IMGUIStyles.StatusLabel) { alignment = TextAnchor.MiddleLeft, fontSize = IMGUIStyles.FontSize(11) };
            note.normal.textColor = IMGUIStyles.TextSecondary;
            IMGUIStyles.DrawLabel(new Rect(panel.x + 140f, panel.y + 15f, panel.width - 160f, 24f),
                "声望每上一档，打开这条路线专属的营生、人脉与门路", note);

            for (int i = 0; i < Factions.Length; i++)
                DrawFactionProgress(snapshot, Factions[i],
                    new Rect(panel.x + 16f, panel.y + headerH + i * rowH, panel.width - 32f, rowH - 10f));
        }

        private static string CompactSummary(PresentationSnapshot snapshot)
        {
            string text = "圈内声誉";
            foreach (string faction in Factions)
            {
                int value = snapshot.Relations.TryGetValue(faction, out int v) ? v : 0;
                text += $"  {faction}{value}";
            }
            return text;
        }

        private static void DrawFactionProgress(PresentationSnapshot snapshot, string faction, Rect rect)
        {
            int value = snapshot.Relations.TryGetValue(faction, out int v) ? v : 0;
            int band = RelationScale.BandIndex(value);
            Color active = RelationBandColors[band];
            GUI.color = new Color(0.07f, 0.08f, 0.11f, 0.92f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(rect, 1f,
                new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.22f));

            var factionStyle = new GUIStyle(IMGUIStyles.CardTitle) { alignment = TextAnchor.MiddleLeft, fontSize = IMGUIStyles.FontSize(16) };
            IMGUIStyles.DrawLabel(new Rect(rect.x + 12f, rect.y + 6f, 55f, 24f), faction, factionStyle);
            var bandStyle = new GUIStyle(IMGUIStyles.StatusLabel) { alignment = TextAnchor.MiddleLeft, fontSize = IMGUIStyles.FontSize(12) };
            bandStyle.normal.textColor = active;
            IMGUIStyles.DrawLabel(new Rect(rect.x + 68f, rect.y + 7f, 100f, 22f), $"{BandDisplay(snapshot, faction, band)}  {value}", bandStyle);

            float trackX = rect.x + 160f, trackY = rect.y + 20f, trackW = rect.width - 180f;
            const int pointCount = RelationScale.Max - RelationScale.Min + 1;
            const float cellGap = 1f;
            float cellW = (trackW - cellGap * (pointCount - 1)) / pointCount;
            for (int point = RelationScale.Min; point <= RelationScale.Max; point++)
            {
                int index = point - RelationScale.Min;
                Color bandColor = RelationBandColors[RelationScale.BandIndex(point)];
                bool traversed = value >= 0 ? point >= 0 && point <= value : point <= 0 && point >= value;
                GUI.color = new Color(bandColor.r, bandColor.g, bandColor.b, traversed ? 0.72f : 0.18f);
                var cell = new Rect(trackX + index * (cellW + cellGap), trackY - 4f, cellW, 8f);
                GUI.DrawTexture(cell, Texture2D.whiteTexture);
                if (point == value)
                    IMGUIStyles.DrawOutline(cell, 1.5f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.95f));
            }
            float currentX = trackX + (value - RelationScale.Min) * (cellW + cellGap) + cellW / 2f;
            GUI.color = active;
            GUI.DrawTexture(new Rect(currentX - 4f, trackY - 4f, 8f, 8f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            const float chipGap = 6f;
            const float chipTop = 41f;
            float chipW = (rect.width - 24f - chipGap * 2f) / 3f;
            // 解锁条按行高剩下的空间收敛，不写死 49——行高本身随屏幕高度变。
            float chipH = Mathf.Max(38f, rect.height - chipTop - 8f);
            for (int t = 0; t < RelationScale.PositiveTiers.Length; t++)
            {
                var chip = new Rect(rect.x + 12f + t * (chipW + chipGap), rect.y + chipTop, chipW, chipH);
                DrawUnlock(snapshot, faction, RelationScale.PositiveTiers[t],
                    RelationScale.PositiveThresholds[t], value, chip, trackX, trackY, trackW);
            }
        }

        private static void DrawUnlock(PresentationSnapshot snapshot, string faction, string tier, int threshold,
            int value, Rect chip, float trackX, float trackY, float trackW)
        {
            bool unlocked = value >= threshold;
            Color color = unlocked ? IMGUIStyles.OddsSuccess : IMGUIStyles.OddsNeutral;
            const int pointCount = RelationScale.Max - RelationScale.Min + 1;
            const float cellGap = 1f;
            float cellW = (trackW - cellGap * (pointCount - 1)) / pointCount;
            float nodeX = trackX + (threshold - RelationScale.Min) * (cellW + cellGap) + cellW / 2f;
            GUI.color = color;
            GUI.DrawTexture(new Rect(nodeX - 3f, trackY - 3f, 6f, 6f), Texture2D.whiteTexture);
            GUI.color = new Color(color.r, color.g, color.b, unlocked ? 0.20f : 0.10f);
            GUI.DrawTexture(chip, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(chip, 1f, new Color(color.r, color.g, color.b, 0.70f));

            var head = new GUIStyle(IMGUIStyles.StatusLabel) { alignment = TextAnchor.MiddleLeft, fontSize = IMGUIStyles.FontSize(11) };
            head.normal.textColor = color;
            string name = snapshot.RelationBandNames.TryGetValue($"{faction}:{tier}", out string disp) ? disp : tier;
            string state = unlocked ? "已解锁" : $"还差 {Mathf.Max(0, threshold - value)}";
            IMGUIStyles.DrawLabel(new Rect(chip.x + 8f, chip.y + 4f, chip.width - 16f, 18f),
                $"{name}  +{threshold}  ·  {state}", head);
            var body = new GUIStyle(IMGUIStyles.StatusLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = IMGUIStyles.FontSize(10),
                clipping = TextClipping.Clip
            };
            body.normal.textColor = IMGUIStyles.TextSecondary;
            string unlock = snapshot.RelationUnlocks.TryGetValue($"{faction}:{tier}", out string configured)
                ? configured : "当前无新增动作";
            IMGUIStyles.DrawLabel(new Rect(chip.x + 8f, chip.y + 23f, chip.width - 16f, 20f), unlock, body);
        }
    }
}
