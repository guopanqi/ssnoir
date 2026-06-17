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

        // ── Entry point ──────────────────────────────────────────────────

        public static void Draw(SSNoirGameManager gameManager, IMGUIInteractionContext ui)
        {
            float handY   = UIScale.VH - PanelHeight - StatusBarHeight - BottomOffset;
            float statusY = UIScale.VH - StatusBarHeight - BottomOffset;

            GUI.color = IMGUIStyles.PanelBg;
            GUI.DrawTexture(new Rect(0, handY, UIScale.VW, PanelHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(
                new Rect(-1, handY, UIScale.VW + 2, PanelHeight + 2),
                1f, IMGUIStyles.OutlineVariantColor);

            float itemsStartX = DrawActorBlocks(handY, gameManager, ui);
            DrawItems(handY, itemsStartX, gameManager, ui);
            DrawEndTurnButton(handY, gameManager, ui);
            DrawStatusBar(statusY, gameManager);
        }

        // ── Actor blocks ─────────────────────────────────────────────────

        private static float DrawActorBlocks(float handY, SSNoirGameManager gameManager, IMGUIInteractionContext ui)
        {
            var snapshot      = gameManager.DisplayedSnapshot;
            float blockX      = 8f;
            int flatDieOffset = 0;

            foreach (var actor in snapshot.Actors)
            {
                if (actor.Status == "away")
                {
                    flatDieOffset += actor.ActionDice.Count;
                    continue;
                }

                float blockW = BlockWidth(actor.ActionDice.Count);
                DrawActorBlock(actor, flatDieOffset, blockX, handY, blockW, gameManager, ui);
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
            IMGUIInteractionContext ui)
        {
            float blockH  = PanelHeight - 12f;
            var blockRect = new Rect(blockX, handY + 6, blockW, blockH);

            GUI.color = new Color(0.09f, 0.10f, 0.14f, 0.55f);
            GUI.DrawTexture(blockRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(blockRect, 1f, IMGUIStyles.OutlineVariantColor);

            float cx = blockX + BlockPadX;
            float cw = blockW - BlockPadX * 2;

            // ── Name
            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                font      = IMGUIStyles.ChineseFont,
                fontSize  = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = IMGUIStyles.OnSurface },
            };
            GUI.Label(new Rect(cx, handY + 9, cw, 22), actor.Name, nameStyle);

            // ── Role tag
            if (!string.IsNullOrEmpty(actor.Role))
            {
                var roleStyle = new GUIStyle(nameStyle)
                {
                    fontSize  = 13,
                    fontStyle = FontStyle.Normal,
                    normal    = { textColor = IMGUIStyles.OnSurfaceVariant },
                };
                GUI.Label(new Rect(cx, handY + 31, cw, 18), actor.Role, roleStyle);
            }

            // ── Stress dots
            float dotsY = handY + 52;
            for (int i = 0; i < MaxStress; i++)
            {
                GUI.color = i < actor.Stress
                    ? new Color(1.0f, 0.50f, 0.38f, 1f)
                    : IMGUIStyles.OutlineVariantColor;
                GUI.DrawTexture(new Rect(cx + i * DotGap, dotsY, DotSize, DotSize), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            if (actor.Stress > 0)
            {
                var stressLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    font    = IMGUIStyles.ChineseFont,
                    fontSize = 12,
                    normal  = { textColor = new Color(1.0f, 0.50f, 0.38f, 0.75f) },
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
                    GUI.color = IMGUIStyles.SlotEmpty;
                    GUI.DrawTexture(dieRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    IMGUIStyles.DrawOutline(dieRect, 1f, IMGUIStyles.OutlineVariantColor);
                    var dimStyle = new GUIStyle(IMGUIStyles.SlotLabel) { fontSize = 22 };
                    dimStyle.normal.textColor = new Color(0.549f, 0.565f, 0.620f, 0.3f);
                    GUI.Label(dieRect, val.ToString(), dimStyle);
                }
                else
                {
                    Color bg     = isSelected ? IMGUIStyles.DieSelected : (hover ? IMGUIStyles.DieHover : IMGUIStyles.DieNormal);
                    Color border = (isSelected || hover) ? IMGUIStyles.PrimaryColor : IMGUIStyles.SecondaryColor;

                    GUI.color = bg;
                    GUI.DrawTexture(dieRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    IMGUIStyles.DrawOutline(dieRect, (isSelected || hover) ? 2f : 1f, border);

                    var dieStyle = new GUIStyle(IMGUIStyles.SlotLabel) { fontSize = 24 };
                    dieStyle.normal.textColor = Color.white;
                    GUI.Label(dieRect, val.ToString(), dieStyle);

                    if (ui.WasClicked(dieRect))
                    {
                        gameManager.OnDieClicked(globalIdx, val);
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
                IMGUIStyles.OutlineVariantColor, 1f);

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
                    GUI.color = IMGUIStyles.SlotEmpty;
                    GUI.DrawTexture(itemRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    IMGUIStyles.DrawOutline(itemRect, 1f, IMGUIStyles.OutlineVariantColor);
                    var dimStyle = new GUIStyle(IMGUIStyles.SlotLabel) { fontSize = 15 };
                    dimStyle.normal.textColor = new Color(0.549f, 0.565f, 0.620f, 0.3f);
                    GUI.Label(itemRect, FormatItem(item.Name, 0), dimStyle);
                }
                else
                {
                    Color bg     = isSelected ? IMGUIStyles.DieSelected : (hover ? IMGUIStyles.ItemHover : IMGUIStyles.ItemNormal);
                    Color border = (isSelected || hover) ? IMGUIStyles.PrimaryColor : IMGUIStyles.SecondaryColor;

                    GUI.color = bg;
                    GUI.DrawTexture(itemRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    IMGUIStyles.DrawOutline(itemRect, (isSelected || hover) ? 2f : 1f, border);

                    var itemStyle = new GUIStyle(IMGUIStyles.SlotLabel) { fontSize = 15 };
                    itemStyle.normal.textColor = Color.white;
                    GUI.Label(itemRect, FormatItem(item.Name, remaining), itemStyle);

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
            string btnText = isInEncounter ? "休息" : "回家";

            var style = new GUIStyle(IMGUIStyles.ExecuteLabel) { fontSize = 18 };
            if (IMGUIButton.Draw(restRect, btnText, ui,
                    IMGUIStyles.TertiaryColor,
                    new Color(1.0f, 0.714f, 0.576f, 0.10f),
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

            GUI.color = IMGUIStyles.BottomBarBg;
            GUI.DrawTexture(new Rect(0, statusY, UIScale.VW, StatusBarHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(
                new Rect(-1, statusY, UIScale.VW + 2, StatusBarHeight + 2),
                1f, IMGUIStyles.OutlineVariantColor);

            var statusStyle = new GUIStyle(IMGUIStyles.StatusLabel) { fontSize = 16 };
            var helpStyle = new GUIStyle(IMGUIStyles.HelpTip) { fontSize = 14 };

            GUI.Label(new Rect(30, statusY + 3, 58, 22), "健康:", statusStyle);

            float healthPct = snapshot.MaxHealth > 0 ? (float)snapshot.Health / snapshot.MaxHealth : 0f;
            var healthStyle = new GUIStyle(statusStyle)
            {
                normal = { textColor = healthPct >= 0.75f
                    ? new Color(0.31f, 0.86f, 0.47f, 1f)
                    : healthPct >= 0.4f
                        ? new Color(0.96f, 0.69f, 0.22f, 1f)
                        : new Color(0.96f, 0.31f, 0.31f, 1f) }
            };
            GUI.Label(new Rect(84, statusY + 3, 80, 22), $"{snapshot.Health}/{snapshot.MaxHealth}", healthStyle);

            GUI.Label(new Rect(158, statusY + 3, 58, 22), "物资:", statusStyle);

            float suppliesPct = snapshot.MaxSupplies > 0 ? (float)snapshot.Supplies / snapshot.MaxSupplies : 0f;
            var suppliesStyle = new GUIStyle(statusStyle)
            {
                normal = { textColor = suppliesPct >= 0.65f
                    ? new Color(0.31f, 0.86f, 0.47f, 1f)
                    : suppliesPct >= 0.3f
                        ? new Color(0.96f, 0.69f, 0.22f, 1f)
                        : new Color(0.96f, 0.31f, 0.31f, 1f) }
            };
            GUI.Label(new Rect(212, statusY + 3, 80, 22), $"{snapshot.Supplies}/{snapshot.MaxSupplies}", suppliesStyle);

            GUI.Label(new Rect(292, statusY + 3, 58, 22), "场景:", statusStyle);
            var locStyle = new GUIStyle(statusStyle)
                { normal = { textColor = IMGUIStyles.MoneyColor } };
            GUI.Label(new Rect(346, statusY + 3, 130, 22), snapshot.Location.ToUpper(), locStyle);

            GUI.Label(new Rect(486, statusY + 3, 620, 22),
                "提示: 点击手牌选择，点击卡槽放入，右键取消选择。", helpStyle);
        }
    }
}
