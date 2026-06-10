#nullable enable
using System.Collections.Generic;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    public static class HandPanelDrawer
    {
        private static readonly float PanelHeight = 140f;
        private static readonly float StatusBarHeight = 30f;
        private static readonly float BottomOffset = 0f;

        public static void Draw(SSNoirGameManager gameManager, Vector2 mousePos)
        {
            float handY = Screen.height - PanelHeight - StatusBarHeight - BottomOffset;
            float statusY = Screen.height - StatusBarHeight - BottomOffset;

            // 1. Hand Panel Background
            GUI.color = IMGUIStyles.PanelBg;
            GUI.DrawTexture(new Rect(0, handY, Screen.width, PanelHeight), Texture2D.whiteTexture);
            GUI.color = new Color(0.2f, 0.2f, 0.25f, 1f);
            GUI.DrawTexture(new Rect(0, handY, Screen.width, 2), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 2. Dice
            DrawDice(handY, gameManager, mousePos);

            // 3. Items
            DrawItems(handY, gameManager, mousePos);

            // 4. End Turn Button
            DrawEndTurnButton(handY, gameManager, mousePos);

            // 5. Status Bar
            DrawStatusBar(statusY, gameManager);
        }

        private static void DrawDice(float handY, SSNoirGameManager gameManager, Vector2 mousePos)
        {
            var dice = gameManager.GameState.Get<List<object>>("action-dice");
            if (dice == null) return;

            GUI.Label(new Rect(30, handY + 58, 100, 24), "手牌骰子: ", IMGUIStyles.SectionLabel);

            for (int i = 0; i < dice.Count; i++)
            {
                float dieX = 130 + i * 96;
                float dieY = handY + 30;
                var dieRect = new Rect(dieX, dieY, 80, 80);

                int val = 0;
                if (dice[i] is double d) val = (int)d;
                else if (dice[i] is long l) val = (int)l;
                else if (dice[i] is int valInt) val = valInt;

                bool isSlotted = gameManager.IsDieSlotted(i);
                bool isSelected = gameManager.SelectedResource != null && gameManager.SelectedResource.Type == "die" && gameManager.SelectedResource.SourceIndex == i;
                bool hover = !isSlotted && dieRect.Contains(mousePos);

                if (isSlotted)
                {
                    GUI.color = new Color(0.08f, 0.08f, 0.1f, 0.5f);
                    GUI.DrawTexture(dieRect, Texture2D.whiteTexture);
                    GUI.color = new Color(0.12f, 0.12f, 0.15f, 0.5f);
                    DrawOutline(dieRect, 2);
                    GUI.color = Color.white;

                    var dimStyle = new GUIStyle(IMGUIStyles.SlotLabel);
                    dimStyle.normal.textColor = new Color(0.25f, 0.25f, 0.3f, 0.5f);
                    dimStyle.fontSize = 24;
                    GUI.Label(dieRect, val.ToString(), dimStyle);
                }
                else
                {
                    Color bg = isSelected ? IMGUIStyles.DieSelected : (hover ? IMGUIStyles.DieHover : IMGUIStyles.DieNormal);
                    Color border = hover ? new Color(0.85f, 0.7f, 0.3f, 1f) : new Color(0.3f, 0.3f, 0.4f, 1f);

                    GUI.color = bg;
                    GUI.DrawTexture(dieRect, Texture2D.whiteTexture);
                    GUI.color = border;
                    DrawOutline(dieRect, 2);
                    GUI.color = Color.white;

                    var dieStyle = new GUIStyle(IMGUIStyles.SlotLabel);
                    dieStyle.fontSize = 28;
                    GUI.Label(dieRect, val.ToString(), dieStyle);

                    if (hover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                    {
                        gameManager.OnDieClicked(i, val);
                        Event.current.Use();
                    }
                }
            }
        }

        private static void DrawItems(float handY, SSNoirGameManager gameManager, Vector2 mousePos)
        {
            var dice = gameManager.GameState.Get<List<object>>("action-dice");
            int diceCount = dice != null ? dice.Count : 0;
            float itemsStartX = Mathf.Max(520f, 140f + diceCount * 96f);

            GUI.Label(new Rect(itemsStartX, handY + 58, 100, 24), "手牌物品: ", IMGUIStyles.SectionLabel);

            var items = new List<(string Name, int Qty)>();
            int money = gameManager.GameState.Get<int>("money");
            if (money > 0)
            {
                items.Add(("金钱", money));
            }

            foreach (var kvp in gameManager.GameState.GetAllStates())
            {
                if (kvp.Key.StartsWith("item:"))
                {
                    string name = kvp.Key.Substring(5);
                    int qty = 0;
                    if (kvp.Value is double d) qty = (int)d;
                    else if (kvp.Value is long l) qty = (int)l;
                    else if (kvp.Value is int valInt) qty = valInt;

                    if (qty > 0)
                    {
                        items.Add((name, qty));
                    }
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
                bool hover = itemRect.Contains(mousePos);

                if (remaining <= 0)
                {
                    GUI.color = new Color(0.08f, 0.08f, 0.1f, 0.5f);
                    GUI.DrawTexture(itemRect, Texture2D.whiteTexture);
                    GUI.color = new Color(0.12f, 0.12f, 0.15f, 0.5f);
                    DrawOutline(itemRect, 2);
                    GUI.color = Color.white;

                    string label = item.Name == "金钱" ? "$0" : $"{item.Name} x0";
                    var dimStyle = new GUIStyle(IMGUIStyles.SlotLabel);
                    dimStyle.normal.textColor = new Color(0.25f, 0.25f, 0.3f, 0.5f);
                    dimStyle.fontSize = 16;
                    GUI.Label(itemRect, label, dimStyle);
                }
                else
                {
                    Color bg = isSelected ? IMGUIStyles.DieSelected : (hover ? IMGUIStyles.ItemHover : IMGUIStyles.ItemNormal);
                    Color border = hover ? new Color(0.85f, 0.7f, 0.3f, 1f) : new Color(0.3f, 0.4f, 0.35f, 1f);

                    GUI.color = bg;
                    GUI.DrawTexture(itemRect, Texture2D.whiteTexture);
                    GUI.color = border;
                    DrawOutline(itemRect, 2);
                    GUI.color = Color.white;

                    string label = item.Name == "金钱" ? $"${remaining}" : $"{item.Name} x{remaining}";
                    var itemStyle = new GUIStyle(IMGUIStyles.SlotLabel);
                    itemStyle.fontSize = 16;
                    GUI.Label(itemRect, label, itemStyle);

                    if (hover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                    {
                        gameManager.OnItemClicked(item.Name, item.Qty);
                        Event.current.Use();
                    }
                }
            }
        }

        private static void DrawEndTurnButton(float handY, SSNoirGameManager gameManager, Vector2 mousePos)
        {
            float restX = Screen.width - 150;
            float restY = handY + 30;
            var restRect = new Rect(restX, restY, 110, 80);
            bool restHover = restRect.Contains(mousePos);

            Color bg = restHover ? new Color(0.65f, 0.2f, 0.2f, 1f) : new Color(0.5f, 0.15f, 0.15f, 1f);
            Color border = restHover ? new Color(0.9f, 0.4f, 0.4f, 1f) : new Color(0.65f, 0.25f, 0.25f, 1f);

            GUI.color = bg;
            GUI.DrawTexture(restRect, Texture2D.whiteTexture);
            GUI.color = border;
            DrawOutline(restRect, 2);
            GUI.color = Color.white;

            var style = new GUIStyle(IMGUIStyles.ExecuteLabel);
            style.fontSize = 18;
            GUI.Label(restRect, "休息", style);

            if (restHover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                gameManager.OnEndTurnClicked();
                Event.current.Use();
            }
        }

        private static void DrawStatusBar(float statusY, SSNoirGameManager gameManager)
        {
            GUI.color = IMGUIStyles.BottomBarBg;
            GUI.DrawTexture(new Rect(0, statusY, Screen.width, StatusBarHeight), Texture2D.whiteTexture);
            GUI.color = new Color(0.15f, 0.15f, 0.2f, 1f);
            GUI.DrawTexture(new Rect(0, statusY, Screen.width, 2), Texture2D.whiteTexture);
            GUI.color = Color.white;

            int health = gameManager.GameState.Get<int>("health");
            string location = gameManager.GameState.Get<string>("location");

            GUI.Label(new Rect(30, statusY + 5, 60, 22), "健康: ", IMGUIStyles.StatusLabel);
            var healthStyle = new GUIStyle(IMGUIStyles.StatusLabel);
            healthStyle.normal.textColor = IMGUIStyles.HealthColor;
            GUI.Label(new Rect(80, statusY + 5, 60, 22), $"{health}%", healthStyle);

            GUI.Label(new Rect(180, statusY + 5, 60, 22), "场景: ", IMGUIStyles.StatusLabel);
            var locStyle = new GUIStyle(IMGUIStyles.StatusLabel);
            locStyle.normal.textColor = IMGUIStyles.MoneyColor;
            GUI.Label(new Rect(230, statusY + 5, 120, 22), location.ToUpper(), locStyle);

            GUI.Label(new Rect(380, statusY + 5, 500, 22), "提示: 点击手牌选择，点击卡槽放入，右键取消选择。", IMGUIStyles.HelpTip);
        }

        private static void DrawOutline(Rect rect, int thickness)
        {
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        }
    }
}
