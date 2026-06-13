#nullable enable
using System.Collections.Generic;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    public static class HandPanelDrawer
    {
        private static readonly float PanelHeight = 140f;
        private static readonly float StatusBarHeight = 25f;
        private static readonly float BottomOffset = 0f;

        public static void Draw(SSNoirGameManager gameManager, IMGUIInteractionContext ui)
        {
            float handY = Screen.height - PanelHeight - StatusBarHeight - BottomOffset;
            float statusY = Screen.height - StatusBarHeight - BottomOffset;

            GUI.color = IMGUIStyles.PanelBg;
            GUI.DrawTexture(new Rect(0, handY, Screen.width, PanelHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(new Rect(-1, handY, Screen.width + 2, PanelHeight + 2), 1f, IMGUIStyles.OutlineVariantColor);

            DrawDice(handY, gameManager, ui);
            DrawItems(handY, gameManager, ui);
            DrawEndTurnButton(handY, gameManager, ui);
            DrawStatusBar(statusY, gameManager);
        }

        private static void DrawDice(float handY, SSNoirGameManager gameManager, IMGUIInteractionContext ui)
        {
            var snapshot = gameManager.DisplayedSnapshot;
            GUI.Label(new Rect(30, handY + 58, 100, 24), "手牌骰子: ", IMGUIStyles.SectionLabel);

            int flatDieIdx = 0;
            float dieXStart = 130f;

            foreach (var actor in snapshot.Actors)
            {
                if (actor.Status == "away")
                {
                    flatDieIdx += actor.ActionDice.Count;
                    continue;
                }

                for (int d = 0; d < actor.ActionDice.Count; d++)
                {
                    int i = flatDieIdx++;
                    float dieX = dieXStart + i * 96;
                    float dieY = handY + 30;
                    var dieRect = new Rect(dieX, dieY, 80, 80);
                    int val = actor.ActionDice[d];

                    bool isSlotted = gameManager.IsDieSlotted(i);
                    bool isSelected = gameManager.SelectedResource != null && gameManager.SelectedResource.Type == "die" && gameManager.SelectedResource.SourceIndex == i;
                    bool hover = !isSlotted && ui.CanHover(dieRect);

                    if (isSlotted)
                    {
                        GUI.color = IMGUIStyles.SlotEmpty;
                        GUI.DrawTexture(dieRect, Texture2D.whiteTexture);
                        GUI.color = Color.white;
                        IMGUIStyles.DrawOutline(dieRect, 1f, IMGUIStyles.OutlineVariantColor);

                        var dimStyle = new GUIStyle(IMGUIStyles.SlotLabel);
                        dimStyle.normal.textColor = new Color(0.549f, 0.565f, 0.620f, 0.3f);
                        dimStyle.fontSize = 24;
                        GUI.Label(dieRect, val.ToString(), dimStyle);
                    }
                    else
                    {
                        Color bg = isSelected ? IMGUIStyles.DieSelected : (hover ? IMGUIStyles.DieHover : IMGUIStyles.DieNormal);
                        Color border = isSelected ? IMGUIStyles.PrimaryColor : (hover ? IMGUIStyles.PrimaryColor : IMGUIStyles.SecondaryColor);
                        float thickness = (isSelected || hover) ? 2f : 1f;

                        GUI.color = bg;
                        GUI.DrawTexture(dieRect, Texture2D.whiteTexture);
                        GUI.color = Color.white;
                        IMGUIStyles.DrawOutline(dieRect, thickness, border);

                        var dieStyle = new GUIStyle(IMGUIStyles.SlotLabel);
                        dieStyle.fontSize = 28;
                        dieStyle.normal.textColor = Color.white;
                        GUI.Label(dieRect, val.ToString(), dieStyle);

                        if (!isSlotted && ui.WasClicked(dieRect))
                        {
                            gameManager.OnDieClicked(i, val);
                            Event.current.Use();
                        }
                    }
                }
            }
        }

        private static void DrawItems(float handY, SSNoirGameManager gameManager, IMGUIInteractionContext ui)
        {
            var snapshot = gameManager.DisplayedSnapshot;
            int diceCount = 0;
            foreach (var actor in snapshot.Actors)
            {
                if (actor.Status != "away")
                {
                    diceCount += actor.ActionDice.Count;
                }
            }

            float itemsStartX = Mathf.Max(520f, 140f + diceCount * 96f);
            GUI.Label(new Rect(itemsStartX, handY + 58, 100, 24), "手牌物品: ", IMGUIStyles.SectionLabel);

            var items = new List<(string Name, int Qty)>();
            foreach (var kvp in snapshot.Inventory)
            {
                if (kvp.Value > 0)
                {
                    items.Add((kvp.Key, kvp.Value));
                }
            }

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                float itemX = itemsStartX + 100 + i * 116;
                float itemY = handY + 30;
                var itemRect = new Rect(itemX, itemY, 100, 80);

                int remaining = gameManager.GetRemainingItemQty(item.Name);
                bool isSelected = gameManager.SelectedResource != null && gameManager.SelectedResource.Type == "item" && gameManager.SelectedResource.ItemName == item.Name;
                bool hover = ui.CanHover(itemRect);

                if (remaining <= 0)
                {
                    GUI.color = IMGUIStyles.SlotEmpty;
                    GUI.DrawTexture(itemRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    IMGUIStyles.DrawOutline(itemRect, 1f, IMGUIStyles.OutlineVariantColor);

                    string label = item.Name == "金钱" ? "$0" : $"{item.Name} x0";
                    var dimStyle = new GUIStyle(IMGUIStyles.SlotLabel);
                    dimStyle.normal.textColor = new Color(0.549f, 0.565f, 0.620f, 0.3f);
                    dimStyle.fontSize = 16;
                    GUI.Label(itemRect, label, dimStyle);
                }
                else
                {
                    Color bg = isSelected ? IMGUIStyles.DieSelected : (hover ? IMGUIStyles.ItemHover : IMGUIStyles.ItemNormal);
                    Color border = isSelected ? IMGUIStyles.PrimaryColor : (hover ? IMGUIStyles.PrimaryColor : IMGUIStyles.SecondaryColor);
                    float thickness = (isSelected || hover) ? 2f : 1f;

                    GUI.color = bg;
                    GUI.DrawTexture(itemRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    IMGUIStyles.DrawOutline(itemRect, thickness, border);

                    string label = item.Name == "金钱" ? $"${remaining}" : $"{item.Name} x{remaining}";
                    var itemStyle = new GUIStyle(IMGUIStyles.SlotLabel);
                    itemStyle.fontSize = 16;
                    itemStyle.normal.textColor = Color.white;
                    GUI.Label(itemRect, label, itemStyle);

                    if (ui.WasClicked(itemRect))
                    {
                        gameManager.OnItemClicked(item.Name, item.Qty);
                        Event.current.Use();
                    }
                }
            }
        }

        private static void DrawEndTurnButton(float handY, SSNoirGameManager gameManager, IMGUIInteractionContext ui)
        {
            float restX = Screen.width - 150;
            float restY = handY + 30;
            var restRect = new Rect(restX, restY, 110, 80);

            var style = new GUIStyle(IMGUIStyles.ExecuteLabel);
            style.fontSize = 18;

            if (IMGUIButton.Draw(restRect, "休息", ui, IMGUIStyles.TertiaryColor, new Color(1.0f, 0.714f, 0.576f, 0.10f), style))
            {
                gameManager.OnEndTurnClicked();
            }
        }

        private static void DrawStatusBar(float statusY, SSNoirGameManager gameManager)
        {
            var snapshot = gameManager.DisplayedSnapshot;

            GUI.color = IMGUIStyles.BottomBarBg;
            GUI.DrawTexture(new Rect(0, statusY, Screen.width, StatusBarHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(new Rect(-1, statusY, Screen.width + 2, StatusBarHeight + 2), 1f, IMGUIStyles.OutlineVariantColor);

            GUI.Label(new Rect(30, statusY + 5, 60, 22), "健康: ", IMGUIStyles.StatusLabel);
            var healthStyle = new GUIStyle(IMGUIStyles.StatusLabel);
            float healthPct = snapshot.MaxHealth > 0 ? (float)snapshot.Health / snapshot.MaxHealth : 0f;
            if (healthPct >= 0.75f)
            {
                healthStyle.normal.textColor = new Color(0.31f, 0.86f, 0.47f, 1f); // Green
            }
            else if (healthPct >= 0.4f)
            {
                healthStyle.normal.textColor = new Color(0.96f, 0.69f, 0.22f, 1f); // Amber/Orange
            }
            else
            {
                healthStyle.normal.textColor = new Color(0.96f, 0.31f, 0.31f, 1f); // Red
            }
            GUI.Label(new Rect(80, statusY + 5, 80, 22), $"{snapshot.Health}/{snapshot.MaxHealth}", healthStyle);

            GUI.Label(new Rect(180, statusY + 5, 60, 22), "场景: ", IMGUIStyles.StatusLabel);
            var locStyle = new GUIStyle(IMGUIStyles.StatusLabel);
            locStyle.normal.textColor = IMGUIStyles.MoneyColor;
            GUI.Label(new Rect(230, statusY + 5, 120, 22), snapshot.Location.ToUpper(), locStyle);

            GUI.Label(new Rect(380, statusY + 5, 500, 22), "提示: 点击手牌选择，点击卡槽放入，右键取消选择。", IMGUIStyles.HelpTip);
        }
    }
}
