#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    // 手牌 HUD。新组织（2026-07 迭代）：不再是底部一整条 HUD 栏，而是拆成两个
    // 独立、无外框的角落簇——左下「人物 / 行动池」，右下「物品 / 功能」。
    //
    // 统一语言：悬浮靠阴影，操作靠描边。要点/要拖的器物（骰子、物品、按钮）才描一圈
    // 安静的边并加硬投影托起；名字、压力、生命体征、标题等直接落在场景上（Paper 亮字，
    // 对深蓝图纸对比足够）。没有底栏、没有把两簇统一在一起的外框。
    //
    // 每个行动者是一个「靠间距聚拢的簇」，自底向上：行动骰 → 名字 → 压力。
    // 所有行动者共享同一条压力/名字/骰池基线；只有主角（最左第一个）从压力线往上
    // 多长出队伍生命体征（健康 / 饱腹，均为快照级属性）。
    public static class HandPanelDrawer
    {
        private const int   MaxStress   = 6;
        private const float DotSize     = 8f;
        private const float DotGap      = 12f;

        // 手牌方块：骰子与物品共用同一族方块（同尺寸、同底纹、同交互状态），只是内容不同。
        private const float TokenSize   = 56f;
        private const float TokenSpacing = 64f;    // TokenSize + 间隙

        private const float BottomMargin = 22f;   // 底边到屏幕底的留白
        private const float LeftStartX   = 40f;
        private const float ClusterGap   = 28f;    // 两个行动者簇之间的间距

        private const float NameRowH   = 20f;
        private const float StressRowH = 20f;
        private const float VitalRowH  = 18f;

        // 手牌黑方块：不透明纯黑一档 + 描边 + 硬投影，让它从深蓝图纸上浮起来。
        private static readonly Color CardBlockBg = new Color(0.024f, 0.031f, 0.047f, 1f);
        private static readonly Color DisabledResourceBg = new Color(0.024f, 0.031f, 0.047f, 0.55f);
        private static readonly Color DisabledResourceText = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.35f);
        private static readonly Color Paper70 = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.70f);
        private static readonly Color Paper35 = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.35f);
        private static readonly Color Paper25 = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.25f);
        private static readonly Color Paper14 = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.14f);

        // ── 入口 ───────────────────────────────────────────────────────

        public static void Draw(SSNoirGameManager gameManager, IMGUIInteractionContext ui, DialogueAnchors? anchors = null)
        {
            float baseline = UIScale.VH - BottomMargin;   // 行动骰底边
            DrawCharacters(baseline, gameManager, ui, anchors);
            DrawItemsAndFunctions(baseline, gameManager, ui);
        }

        // ── 左下：人物簇 ───────────────────────────────────────────────

        private static void DrawCharacters(float baseline, SSNoirGameManager gameManager, IMGUIInteractionContext ui, DialogueAnchors? anchors)
        {
            var snapshot = gameManager.DisplayedSnapshot;
            float x = LeftStartX;
            int flatDieOffset = 0;
            bool leadDrawn = false;

            for (int i = 0; i < snapshot.Actors.Count; i++)
            {
                var actor = snapshot.Actors[i];
                if (actor.Status == "away")
                {
                    flatDieOffset += actor.ActionDice.Count;
                    continue;
                }

                bool isLead = !leadDrawn;   // 最左第一个在场角色 = 主角，头顶挂队伍生命体征
                leadDrawn = true;

                float clusterW = DrawCluster(x, baseline, actor, flatDieOffset, isLead, snapshot, gameManager, ui, anchors);
                x += clusterW + ClusterGap;
                flatDieOffset += actor.ActionDice.Count;
            }
        }

        // 一个行动者簇：自底向上 行动骰 → 名字 → 压力（→ 主角再往上 饱腹/健康）。返回簇宽度。
        private static float DrawCluster(
            float x, float baseline, ActorSnapshot actor, int flatDieOffset, bool isLead,
            PresentationSnapshot snapshot, SSNoirGameManager gameManager, IMGUIInteractionContext ui, DialogueAnchors? anchors)
        {
            int diceCount = actor.ActionDice.Count;
            float diceW = diceCount > 0 ? (diceCount - 1) * TokenSpacing + TokenSize : 0f;
            float clusterW = Mathf.Max(isLead ? 176f : 120f, diceW);

            float diceY   = baseline - TokenSize;
            float nameY   = diceY - NameRowH - 4f;
            float stressY = nameY - StressRowH;
            float topY    = stressY;

            // ── 名字 + 职业（自由文字，直接落在场景上）
            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                font = IMGUIStyles.ChineseFont,
                fontSize = 18,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = IMGUIStyles.TextPrimary },
            };
            IMGUIStyles.ApplyStrongFont(nameStyle);
            GUI.Label(new Rect(x, nameY, clusterW, NameRowH), actor.Name, nameStyle);
            if (!isLead && !string.IsNullOrEmpty(actor.Role))
            {
                float nameW = nameStyle.CalcSize(new GUIContent(actor.Name)).x;
                var roleStyle = new GUIStyle(nameStyle)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Normal,
                    normal = { textColor = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.78f) },
                };
                GUI.Label(new Rect(x + nameW + 8f, nameY + 1f, clusterW + 80f, NameRowH), actor.Role, roleStyle);
            }

            // ── 压力（每人一份；印章红 = 已承压，空段 25% 纸白）
            var stressLabelStyle = new GUIStyle(GUI.skin.label)
            {
                font = IMGUIStyles.ChineseFont,
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.78f) },
            };
            GUI.Label(new Rect(x, stressY, 30f, StressRowH), "压力", stressLabelStyle);
            float dotsX = x + 34f;
            float dotY = stressY + (StressRowH - DotSize) / 2f;
            for (int s = 0; s < MaxStress; s++)
            {
                GUI.color = s < actor.Stress ? IMGUIStyles.SealRed : Paper25;
                GUI.DrawTexture(new Rect(dotsX + s * DotGap, dotY, DotSize, DotSize), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            // ── 主角：压力线往上再挂 饱腹 / 健康（队伍级生命体征）
            if (isLead)
            {
                float satietyY = stressY - VitalRowH - 2f;
                float healthY  = satietyY - VitalRowH - 2f;
                DrawVitalBar(x, satietyY, 172f, "饱腹", snapshot.Satiety, snapshot.MaxSatiety, 0.65f, 0.30f);
                DrawVitalBar(x, healthY, 172f, "健康", snapshot.Health, snapshot.MaxHealth, 0.75f, 0.40f);
                topY = healthY;
                clusterW = Mathf.Max(clusterW, 172f);
            }

            // ── 行动骰（手牌方块；选中金描边+上浮+金字；已放入卡槽降为禁用亮度）
            for (int d = 0; d < diceCount; d++)
            {
                float dieX = x + d * TokenSpacing;
                DrawDie(new Rect(dieX, diceY, TokenSize, TokenSize), actor.ActionDice[d], flatDieOffset + d, gameManager, ui);
            }

            anchors?.RegisterActor(actor.Id, actor.Name, new Rect(x, topY, clusterW, baseline - topY));
            return clusterW;
        }

        private static void DrawDie(Rect dieRect, int val, int globalIdx, SSNoirGameManager gameManager, IMGUIInteractionContext ui)
        {
            bool isSlotted  = gameManager.IsDieSlotted(globalIdx);
            bool isSelected = gameManager.SelectedResource != null
                           && gameManager.SelectedResource.Type == "die"
                           && gameManager.SelectedResource.SourceIndex == globalIdx;
            bool hover = !isSlotted && ui.CanHover(dieRect);

            if (isSlotted)
            {
                GUI.color = DisabledResourceBg;
                GUI.DrawTexture(dieRect, Texture2D.whiteTexture);
                GUI.color = Color.white;
                IMGUIStyles.DrawOutline(dieRect, 1f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.20f));
                var dimStyle = new GUIStyle(IMGUIStyles.SlotLabel) { fontSize = 22 };
                dimStyle.normal.textColor = Paper25;
                GUI.Label(dieRect, val.ToString(), dimStyle);
                return;
            }

            bool disabled = ui.IsLocked;
            // 骰子与物品同一族方块：骰值居中（无下方标签）。
            DrawHandBlock(dieRect, val.ToString(), null, isSelected, hover, disabled);

            if (!disabled && ui.WasClicked(dieRect))
            {
                gameManager.BeginDieDrag(globalIdx, val, ui.Mouse);
                Event.current.Use();
            }
        }

        // 手牌方块（骰子 / 物品共用）：黑方块 + 描边 + 硬投影；大字（骰值或物品符号）在上/中，
        // 下方可选小标签（物品的数量/金额）。选中：金描边 + 上浮 + 金字；禁用：整体降到 35%。
        private static void DrawHandBlock(Rect rect, string big, string? small, bool selected, bool hover, bool disabled)
        {
            var drawRect = selected && !disabled
                ? new Rect(rect.x, rect.y - 4f, rect.width, rect.height)
                : rect;
            Color border = disabled
                ? Paper35
                : selected
                    ? IMGUIStyles.Gold
                    : hover
                        ? IMGUIStyles.Paper
                        : Paper70;

            if (!disabled)
                IMGUIStyles.DrawShadow(drawRect, new Vector2(2f, 2f), 0.45f);
            GUI.color = disabled ? DisabledResourceBg : CardBlockBg;
            GUI.DrawTexture(drawRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(drawRect, !disabled && (selected || hover) ? 2f : 1f, border);

            Color content = disabled ? DisabledResourceText : selected ? IMGUIStyles.Gold : IMGUIStyles.Paper;
            bool hasSmall = !string.IsNullOrEmpty(small);

            var bigStyle = new GUIStyle(IMGUIStyles.SlotLabel)
            {
                fontSize = 24,
                alignment = hasSmall ? TextAnchor.UpperCenter : TextAnchor.MiddleCenter,
                normal = { textColor = content }
            };
            IMGUIStyles.ApplyStrongFont(bigStyle);
            var bigRect = hasSmall ? new Rect(drawRect.x, drawRect.y + 6f, drawRect.width, 30f) : drawRect;
            GUI.Label(bigRect, big, bigStyle);

            if (hasSmall)
            {
                var smallStyle = new GUIStyle(IMGUIStyles.SlotLabel)
                {
                    fontSize = 13,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = disabled ? DisabledResourceText : (selected ? IMGUIStyles.Gold : IMGUIStyles.Paper) }
                };
                IMGUIStyles.ApplyStrongFont(smallStyle);
                GUI.Label(new Rect(drawRect.x, drawRect.y + 34f, drawRect.width, 18f), small, smallStyle);
            }
        }

        // 生命体征细条：标签 + 底槽 + 按阈值上色的填充 + 数值。正常白、中段赭黄、危险印章红。
        private static void DrawVitalBar(float x, float y, float w, string label, int cur, int max, float highT, float midT)
        {
            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                font = IMGUIStyles.ChineseFont,
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.78f) },
            };
            GUI.Label(new Rect(x, y, 30f, VitalRowH), label, labelStyle);

            float pct = max > 0 ? Mathf.Clamp01((float)cur / max) : 0f;
            Color fill = pct >= highT ? IMGUIStyles.TextPrimary : pct >= midT ? IMGUIStyles.OddsNeutral : IMGUIStyles.SealRed;

            float barX = x + 34f;
            float barW = w - 34f - 36f;
            float barH = 9f;
            float barY = y + (VitalRowH - barH) / 2f;
            GUI.color = Paper14;
            GUI.DrawTexture(new Rect(barX, barY, barW, barH), Texture2D.whiteTexture);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(barX, barY, barW * pct, barH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var valStyle = new GUIStyle(GUI.skin.label)
            {
                font = IMGUIStyles.ChineseFont,
                fontSize = 13,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = fill },
            };
            GUI.Label(new Rect(barX + barW + 2f, y, 34f, VitalRowH), $"{cur}/{max}", valStyle);
        }

        // ── 右下：物品 + 功能 ──────────────────────────────────────────

        private static void DrawItemsAndFunctions(float baseline, SSNoirGameManager gameManager, IMGUIInteractionContext ui)
        {
            // 功能键（回家 / 休息）钉在最右
            float restW = 110f, restH = 64f;
            float restX = UIScale.VW - 24f - restW;
            float restY = baseline - restH;
            var restRect = new Rect(restX, restY, restW, restH);
            var sectionStyle = new GUIStyle(IMGUIStyles.SectionLabel)
            {
                fontSize = 17,
                normal = { textColor = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.82f) }
            };
            IMGUIStyles.ApplyStrongFont(sectionStyle);
            GUI.Label(new Rect(restX, restY - 20f, restW, 18f), "功能", sectionStyle);

            bool isInEncounter = !gameManager.SceneManager.CurrentSceneName.Equals("world", StringComparison.OrdinalIgnoreCase);
            string btnText = isInEncounter ? "休 息" : "回 家";

            // 实心黑方块（与骰子/物品同语言：填充 + 描边 + 硬投影），不用空心描边，避免「不明确」。
            bool locked = ui.IsLocked;
            bool fnHover = ui.CanHover(restRect);
            if (!locked)
                IMGUIStyles.DrawShadow(restRect, new Vector2(2f, 2f), 0.45f);
            GUI.color = locked ? DisabledResourceBg : CardBlockBg;
            GUI.DrawTexture(restRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            Color fnBorder = locked ? Paper35 : (fnHover ? IMGUIStyles.Paper : Paper70);
            IMGUIStyles.DrawOutline(restRect, !locked && fnHover ? 2f : 1f, fnBorder);
            var btnStyle = new GUIStyle(IMGUIStyles.ExecuteLabel)
            {
                fontSize = 18,
                normal = { textColor = locked ? DisabledResourceText : IMGUIStyles.Paper }
            };
            GUI.Label(restRect, btnText, btnStyle);
            if (ui.WasClicked(restRect))
            {
                if (isInEncounter) gameManager.OnEndTurnClicked();
                else gameManager.NavigateToHome();
                Event.current.Use();
            }

            // 物品：黑方块 + 白色符号大字 + 下方小字标签，排在功能键左侧、从右往左贴住。
            var snapshot = gameManager.DisplayedSnapshot;
            var items = new List<(string Name, int Qty)>();
            foreach (var kvp in snapshot.Inventory)
                if (kvp.Value > 0) items.Add((kvp.Key, kvp.Value));
            if (items.Count == 0) return;

            float itemsW = (items.Count - 1) * TokenSpacing + TokenSize;
            float itemsRightEdge = restX - 28f;
            float startX = itemsRightEdge - itemsW;
            float itemY = baseline - TokenSize;
            GUI.Label(new Rect(startX, itemY - 20f, 80f, 18f), "物品", sectionStyle);

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var itemRect = new Rect(startX + i * TokenSpacing, itemY, TokenSize, TokenSize);
                int remaining = gameManager.GetRemainingItemQty(item.Name);
                bool isSelected = gameManager.SelectedResource != null
                               && gameManager.SelectedResource.Type == "item"
                               && gameManager.SelectedResource.ItemName == item.Name;
                bool hover = ui.CanHover(itemRect);

                if (remaining <= 0)
                {
                    DrawItemBlock(itemRect, item.Name, isSelected, hover, remaining, disabled: true);
                }
                else
                {
                    bool disabled = ui.IsLocked;
                    DrawItemBlock(itemRect, item.Name, isSelected, hover, remaining, disabled);
                    if (!disabled && ui.WasClicked(itemRect))
                    {
                        gameManager.BeginItemDrag(item.Name, item.Qty, ui.Mouse);
                        Event.current.Use();
                    }
                }
            }
        }

        // 物品：与骰子共用手牌方块，大字=类别符号，小标签=数量/金额（金钱用 $，其它取首字）。
        private static void DrawItemBlock(Rect itemRect, string name, bool isSelected, bool hover, int remaining, bool disabled)
        {
            string smallLabel = name == "金钱" ? $"${remaining}" : $"x{remaining}";
            DrawHandBlock(itemRect, ItemSymbol(name), smallLabel, isSelected, hover, disabled);
        }

        private static string ItemSymbol(string name)
        {
            return name switch
            {
                "金钱" => "$",
                "酒" => "酒",
                "药品" => "药",
                "食物" => "食",
                _ => name.Length > 0 ? name.Substring(0, 1) : "?"
            };
        }
    }
}
