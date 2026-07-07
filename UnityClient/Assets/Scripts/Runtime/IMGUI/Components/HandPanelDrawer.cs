#nullable enable
using System.Collections.Generic;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    public static class HandPanelDrawer
    {
        private const float PanelHeight     = 150f;
        private const float StatusBarHeight = 25f;
        private const float BottomOffset    = 0f;

        private const int   MaxStress   = 6;
        private const float DieSize     = 54f;
        private const float DieSpacing  = 62f;
        private const float BlockPadX   = 10f;
        private const float BlockGap    = 8f;
        private const float DotSize     = 7f;
        private const float DotGap      = 10f;

        // 手牌黑方块（不透明纯黑一档，让方块从底栏上浮出来）
        private static readonly Color CardBlockBg = new Color(0.024f, 0.031f, 0.047f, 1f);
        private static readonly Color DisabledResourceBg = new Color(0.024f, 0.031f, 0.047f, 0.55f);
        private static readonly Color DisabledResourceText = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.35f);

        // ── Entry point ──────────────────────────────────────────────────

        public static void Draw(SSNoirGameManager gameManager, IMGUIInteractionContext ui, DialogueAnchors? anchors = null)
        {
            float handY   = UIScale.VH - PanelHeight - StatusBarHeight - BottomOffset;
            float statusY = UIScale.VH - StatusBarHeight - BottomOffset;

            // 黑底 HUD：HudBg 填充 + 1px Paper 40% 顶线
            GUI.color = IMGUIStyles.HudBg;
            GUI.DrawTexture(new Rect(0, handY, UIScale.VW, PanelHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawLine(
                new Vector2(0, handY),
                new Vector2(UIScale.VW, handY),
                new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f), 1f);

            float itemsStartX = DrawActorBlocks(handY, gameManager, ui, anchors);
            DrawItems(handY, itemsStartX, gameManager, ui);
            DrawEndTurnButton(handY, gameManager, ui);
            DrawStatusBar(statusY, gameManager);
        }

        // ── Actor blocks ─────────────────────────────────────────────────

        private static float DrawActorBlocks(float handY, SSNoirGameManager gameManager, IMGUIInteractionContext ui, DialogueAnchors? anchors)
        {
            var snapshot      = gameManager.DisplayedSnapshot;
            float blockX      = 8f;
            int flatDieOffset = 0;

            for (int i = 0; i < snapshot.Actors.Count; i++)
            {
                var actor = snapshot.Actors[i];
                if (actor.Status == "away")
                {
                    flatDieOffset += actor.ActionDice.Count;
                    continue;
                }

                // Theme color by party join order — the SAME source the card right-rail uses,
                // so a die's color matches its owner's ability chip on the cards.
                var (tr, tg, tb) = ActorTheme.ColorFor(i);
                Color themeColor = new Color(tr / 255f, tg / 255f, tb / 255f, 1f);

                float blockW = BlockWidth(actor.ActionDice.Count);
                var blockRect = new Rect(blockX, handY + 6, blockW, PanelHeight - 12f);
                anchors?.RegisterActor(actor.Id, actor.Name, blockRect);
                DrawActorBlock(actor, flatDieOffset, blockX, handY, blockW, gameManager, ui, themeColor);
                blockX += blockW + BlockGap;
                flatDieOffset += actor.ActionDice.Count;
            }

            return blockX + 4f;
        }

        private static float BlockWidth(int diceCount)
        {
            float diceW = diceCount > 0 ? (diceCount - 1) * DieSpacing + DieSize : 0f;
            return Mathf.Max(110f, BlockPadX * 2 + diceW);
        }

        private static void DrawActorBlock(
            ActorSnapshot actor,
            int flatDieOffset,
            float blockX, float handY,
            float blockW,
            SSNoirGameManager gameManager,
            IMGUIInteractionContext ui,
            Color themeColor)
        {
            float blockH  = PanelHeight - 12f;
            var blockRect = new Rect(blockX, handY + 6, blockW, blockH);

            // 黑底安静块：黑方块 + 主题色只留左侧色条与描边低调提示
            GUI.color = CardBlockBg;
            GUI.DrawTexture(blockRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(blockRect, 1f, new Color(themeColor.r, themeColor.g, themeColor.b, 0.45f));

            // Left accent stripe in the actor's theme color — the region's identity marker.
            GUI.color = themeColor;
            GUI.DrawTexture(new Rect(blockX, handY + 6, 3f, blockH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float cx = blockX + BlockPadX;
            float cw = blockW - BlockPadX * 2;

            // ── Name
            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                font      = IMGUIStyles.ChineseFont,
                fontSize  = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = themeColor },
            };
            GUI.Label(new Rect(cx, handY + 9, cw, 22), actor.Name, nameStyle);

            // ── Role tag
            if (!string.IsNullOrEmpty(actor.Role))
            {
                var roleStyle = new GUIStyle(nameStyle)
                {
                    fontSize  = 13,
                    fontStyle = FontStyle.Normal,
                    normal    = { textColor = IMGUIStyles.TextSecondary },
                };
                GUI.Label(new Rect(cx, handY + 31, cw, 18), actor.Role, roleStyle);
            }

            // ── Stress dots（分段离散：压力=印章红，空段=35% 纸白）
            float dotsY = handY + 52;
            for (int i = 0; i < MaxStress; i++)
            {
                GUI.color = i < actor.Stress
                    ? IMGUIStyles.SealRed
                    : new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.25f);
                GUI.DrawTexture(new Rect(cx + i * DotGap, dotsY, DotSize, DotSize), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            if (actor.Stress > 0)
            {
                var stressLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    font    = IMGUIStyles.ChineseFont,
                    fontSize = 12,
                    normal  = { textColor = new Color(IMGUIStyles.SealRed.r, IMGUIStyles.SealRed.g, IMGUIStyles.SealRed.b, 0.85f) },
                };
                GUI.Label(new Rect(cx + MaxStress * DotGap + 4, dotsY - 4, 56, 18),
                    $"压力{actor.Stress}", stressLabelStyle);
            }

            // ── Action dice
            float diceY = handY + 70;
            for (int d = 0; d < actor.ActionDice.Count; d++)
            {
                int   globalIdx = flatDieOffset + d;
                float dieX      = cx + d * DieSpacing;
                var   dieRect   = new Rect(dieX, diceY, DieSize, DieSize);
                int   val       = actor.ActionDice[d];

                bool isSlotted  = gameManager.IsDieSlotted(globalIdx);
                bool isSelected = gameManager.SelectedResource != null
                               && gameManager.SelectedResource.Type == "die"
                               && gameManager.SelectedResource.SourceIndex == globalIdx;
                bool hover = !isSlotted && ui.CanHover(dieRect);

                if (isSlotted)
                {
                    // 已放入卡槽的骰子：留位但整体降到禁用亮度
                    GUI.color = DisabledResourceBg;
                    GUI.DrawTexture(dieRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    IMGUIStyles.DrawOutline(dieRect, 1f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.20f));
                    var dimStyle = new GUIStyle(IMGUIStyles.SlotLabel) { fontSize = 22 };
                    dimStyle.normal.textColor = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.25f);
                    GUI.Label(dieRect, val.ToString(), dimStyle);
                }
                else
                {
                    bool disabled = ui.IsLocked;

                    // 选中：金描边 + 上浮几像素 + 金字；悬停：白描边变亮；默认：白线 70%
                    var drawRect = isSelected && !disabled
                        ? new Rect(dieRect.x, dieRect.y - 4f, dieRect.width, dieRect.height)
                        : dieRect;
                    Color border = disabled
                        ? new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.35f)
                        : isSelected
                            ? IMGUIStyles.Gold
                            : hover
                                ? new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 1f)
                                : new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.70f);

                    if (!disabled)
                    {
                        IMGUIStyles.DrawShadow(drawRect, new Vector2(2f, 2f), 0.45f);
                    }
                    GUI.color = disabled ? DisabledResourceBg : CardBlockBg;
                    GUI.DrawTexture(drawRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    IMGUIStyles.DrawOutline(drawRect, !disabled && (isSelected || hover) ? 2f : 1f, border);

                    var dieStyle = new GUIStyle(IMGUIStyles.SlotLabel) { fontSize = 24 };
                    dieStyle.normal.textColor = disabled
                        ? DisabledResourceText
                        : isSelected ? IMGUIStyles.Gold : IMGUIStyles.Paper;
                    GUI.Label(drawRect, val.ToString(), dieStyle);

                    if (ui.WasClicked(dieRect))
                    {
                        gameManager.BeginDieDrag(globalIdx, val, ui.Mouse);
                        Event.current.Use();
                    }
                }
            }
        }

        // ── Items section ─────────────────────────────────────────────────

        private static void DrawItems(float handY, float startX, SSNoirGameManager gameManager, IMGUIInteractionContext ui)
        {
            var snapshot = gameManager.DisplayedSnapshot;
            var items    = new List<(string Name, int Qty)>();
            foreach (var kvp in snapshot.Inventory)
            {
                if (kvp.Value > 0)
                    items.Add((kvp.Key, kvp.Value));
            }

            if (items.Count == 0) return;

            // Separator line between actors and items
            IMGUIStyles.DrawLine(
                new Vector2(startX - 4f, handY + 10),
                new Vector2(startX - 4f, handY + PanelHeight - 10),
                new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.35f), 1f);

            var sectionStyle = new GUIStyle(IMGUIStyles.SectionLabel);
            GUI.Label(new Rect(startX + 4, handY + 9, 64, 22), "物品", sectionStyle);

            const float ItemSize    = 60f;
            const float ItemSpacing = 68f;
            float itemY = handY + (PanelHeight - ItemSize) / 2f - 2f;

            for (int i = 0; i < items.Count; i++)
            {
                var   item     = items[i];
                float itemX    = startX + 4 + i * ItemSpacing;
                var   itemRect = new Rect(itemX, itemY, ItemSize, ItemSize);

                int  remaining  = gameManager.GetRemainingItemQty(item.Name);
                bool isSelected = gameManager.SelectedResource != null
                               && gameManager.SelectedResource.Type == "item"
                               && gameManager.SelectedResource.ItemName == item.Name;
                bool hover = ui.CanHover(itemRect);

                if (remaining <= 0)
                {
                    GUI.color = DisabledResourceBg;
                    GUI.DrawTexture(itemRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    IMGUIStyles.DrawOutline(itemRect, 1f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.20f));
                    var dimStyle = new GUIStyle(IMGUIStyles.SlotLabel) { fontSize = 15 };
                    dimStyle.normal.textColor = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.25f);
                    GUI.Label(itemRect, FormatItem(item.Name, 0), dimStyle);
                }
                else
                {
                    bool disabled = ui.IsLocked;

                    // 与骰子同规则：黑方块白字；悬停白描边变亮；选中金描边+上浮+金字
                    var drawRect = isSelected && !disabled
                        ? new Rect(itemRect.x, itemRect.y - 4f, itemRect.width, itemRect.height)
                        : itemRect;
                    Color border = disabled
                        ? new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.35f)
                        : isSelected
                            ? IMGUIStyles.Gold
                            : hover
                                ? new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 1f)
                                : new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.70f);

                    if (!disabled)
                    {
                        IMGUIStyles.DrawShadow(drawRect, new Vector2(2f, 2f), 0.45f);
                    }
                    GUI.color = disabled ? DisabledResourceBg : CardBlockBg;
                    GUI.DrawTexture(drawRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    IMGUIStyles.DrawOutline(drawRect, !disabled && (isSelected || hover) ? 2f : 1f, border);

                    var itemStyle = new GUIStyle(IMGUIStyles.SlotLabel) { fontSize = 15 };
                    itemStyle.normal.textColor = disabled
                        ? DisabledResourceText
                        : isSelected ? IMGUIStyles.Gold : IMGUIStyles.Paper;
                    GUI.Label(drawRect, FormatItem(item.Name, remaining), itemStyle);

                    if (ui.WasClicked(itemRect))
                    {
                        gameManager.OnItemClicked(item.Name, item.Qty);
                        Event.current.Use();
                    }
                }
            }
        }

        private static string FormatItem(string name, int qty)
            => name == "金钱" ? $"${qty}" : $"{name}\nx{qty}";

        // ── End turn button ───────────────────────────────────────────────

        private static void DrawEndTurnButton(float handY, SSNoirGameManager gameManager, IMGUIInteractionContext ui)
        {
            float restX = UIScale.VW - 130f;
            float restY = handY + (PanelHeight - 70f) / 2f;
            var restRect = new Rect(restX, restY, 100f, 70f);

            bool isInEncounter = !gameManager.SceneManager.CurrentSceneName.Equals("world", System.StringComparison.OrdinalIgnoreCase);
            // 标题类文字拉字距（两字插空格）
            string btnText = isInEncounter ? "休 息" : "回 家";

            // HUD 按钮：黑底白字，1px Paper 40% 描边，悬停提到全亮
            var style = new GUIStyle(IMGUIStyles.ExecuteLabel) { fontSize = 18 };
            if (IMGUIButton.Draw(restRect, btnText, ui,
                    new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f),
                    new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.08f),
                    style))
            {
                if (isInEncounter)
                {
                    gameManager.OnEndTurnClicked();
                }
                else
                {
                    gameManager.NavigateToHome();
                }
            }
        }

        // ── Status bar ────────────────────────────────────────────────────

        private static void DrawStatusBar(float statusY, SSNoirGameManager gameManager)
        {
            var snapshot = gameManager.DisplayedSnapshot;

            GUI.color = IMGUIStyles.HudBg;
            GUI.DrawTexture(new Rect(0, statusY, UIScale.VW, StatusBarHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawLine(
                new Vector2(0, statusY),
                new Vector2(UIScale.VW, statusY),
                new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.25f), 1f);

            var statusStyle = new GUIStyle(IMGUIStyles.StatusLabel) { fontSize = 16 };
            var helpStyle = new GUIStyle(IMGUIStyles.HelpTip) { fontSize = 14 };

            GUI.Label(new Rect(30, statusY + 3, 58, 22), "健康:", statusStyle);

            // 数值保持低调：正常段用主文字白，中段赭黄，危险段印章红
            float healthPct = snapshot.MaxHealth > 0 ? (float)snapshot.Health / snapshot.MaxHealth : 0f;
            var healthStyle = new GUIStyle(statusStyle)
            {
                normal = { textColor = healthPct >= 0.75f
                    ? IMGUIStyles.TextPrimary
                    : healthPct >= 0.4f
                        ? IMGUIStyles.OddsNeutral
                        : IMGUIStyles.SealRed }
            };
            GUI.Label(new Rect(84, statusY + 3, 80, 22), $"{snapshot.Health}/{snapshot.MaxHealth}", healthStyle);

            GUI.Label(new Rect(158, statusY + 3, 58, 22), "饱腹:", statusStyle);

            float satietyPct = snapshot.MaxSatiety > 0 ? (float)snapshot.Satiety / snapshot.MaxSatiety : 0f;
            var satietyStyle = new GUIStyle(statusStyle)
            {
                normal = { textColor = satietyPct >= 0.65f
                    ? IMGUIStyles.TextPrimary
                    : satietyPct >= 0.3f
                        ? IMGUIStyles.OddsNeutral
                        : IMGUIStyles.SealRed }
            };
            GUI.Label(new Rect(212, statusY + 3, 80, 22), $"{snapshot.Satiety}/{snapshot.MaxSatiety}", satietyStyle);

            GUI.Label(new Rect(292, statusY + 3, 58, 22), "场景:", statusStyle);
            var locStyle = new GUIStyle(statusStyle)
                { normal = { textColor = IMGUIStyles.TextSecondary } };
            GUI.Label(new Rect(346, statusY + 3, 130, 22), snapshot.Location.ToUpper(), locStyle);

            GUI.Label(new Rect(486, statusY + 3, 620, 22),
                "提示: 点击手牌选择，点击卡槽放入，右键取消选择。", helpStyle);
        }
    }
}
