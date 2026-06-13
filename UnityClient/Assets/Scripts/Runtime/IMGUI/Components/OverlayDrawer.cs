#nullable enable
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    public static class OverlayDrawer
    {
        public static void DrawNotifications(NotificationCenter notificationCenter)
        {
            var visibleNotifs = notificationCenter.GetVisible();
            for (int i = 0; i < visibleNotifs.Count; i++)
            {
                var notif = visibleNotifs[i];
                float remaining = notif.Duration - notif.ElapsedTime;
                float alpha = 1f;
                if (remaining < 0.5f)
                {
                    alpha = Mathf.Clamp01(remaining / 0.5f);
                }

                float cardW = 240f;
                float cardH = 34f;
                float cardX = Screen.width - cardW - 40f;
                float cardY = 75f + i * (cardH + 8f);
                var cardRect = new Rect(cardX, cardY, cardW, cardH);

                Color bg, border, textCol;
                switch (notif.Kind)
                {
                    case NotificationKind.Success:
                        bg = new Color(0.06f, 0.16f, 0.08f, alpha * 0.9f);
                        border = new Color(0.20f, 0.78f, 0.31f, alpha);
                        textCol = new Color(0.86f, 1.0f, 0.86f, alpha);
                        break;
                    case NotificationKind.Error:
                        bg = new Color(0.18f, 0.06f, 0.06f, alpha * 0.9f);
                        border = new Color(0.86f, 0.24f, 0.24f, alpha);
                        textCol = new Color(1.0f, 0.86f, 0.86f, alpha);
                        break;
                    case NotificationKind.Warning:
                        bg = new Color(0.16f, 0.12f, 0.06f, alpha * 0.9f);
                        border = new Color(0.86f, 0.63f, 0.16f, alpha);
                        textCol = new Color(1.0f, 0.94f, 0.78f, alpha);
                        break;
                    case NotificationKind.Info:
                    default:
                        bg = new Color(0.06f, 0.08f, 0.16f, alpha * 0.9f);
                        border = new Color(0.31f, 0.59f, 0.94f, alpha);
                        textCol = new Color(0.86f, 0.94f, 1.0f, alpha);
                        break;
                }

                GUI.color = bg;
                GUI.DrawTexture(cardRect, Texture2D.whiteTexture);
                GUI.color = Color.white;
                IMGUIStyles.DrawOutline(cardRect, 1f, border);

                var style = new GUIStyle(IMGUIStyles.ToastLabel);
                style.normal.textColor = textCol;
                GUI.Label(cardRect, notif.Text, style);
            }
        }

        public static void DrawCursorFollower(SSNoirGameManager gameManager)
        {
            var selected = gameManager.SelectedResource;
            var mousePos = Event.current.mousePosition;
            if (selected == null)
            {
                Cursor.visible = true;
                return;
            }

            Cursor.visible = true;
            float overlayW = selected.Type == "die" ? 50f : 100f;
            float overlayH = 28f;
            var rect = new Rect(mousePos.x + 15, mousePos.y + 15, overlayW, overlayH);

            GUI.color = IMGUIStyles.CardHoverBg;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(rect, 1f, IMGUIStyles.PrimaryColor);

            string text = selected.Type == "die" ? $"D{selected.Value}" : selected.ItemName;
            var style = new GUIStyle(IMGUIStyles.CursorFollower);
            style.alignment = TextAnchor.MiddleCenter;
            GUI.Label(rect, text, style);
        }

        public static void DrawRollResult(SSNoirGameManager gameManager, Vector2 mousePos)
        {
            var activeRoll = gameManager.ActiveRollResult;
            if (activeRoll == null) return;

            // Blocker
            GUI.color = IMGUIStyles.Blocker;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Modal
            float modalW = 380;
            float modalH = 240;
            float modalX = (Screen.width - modalW) / 2f;
            float modalY = (Screen.height - modalH) / 2f;
            var modalRect = new Rect(modalX, modalY, modalW, modalH);

            GUI.color = IMGUIStyles.ModalBg;
            GUI.DrawTexture(modalRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(modalRect, 1f, IMGUIStyles.PrimaryColor);

            float contentX = modalX + 20;
            float contentY = modalY + 20;
            float contentW = modalW - 40;

            // Title
            GUI.Label(new Rect(contentX, contentY, contentW, 24), $"[判定结果] {activeRoll.ActionName}", IMGUIStyles.ModalTitle);
            contentY += 28;

            // Details
            GUI.Label(new Rect(contentX, contentY, contentW, 20), $"投入骰子值: {activeRoll.ChosenDie}", IMGUIStyles.ModalBody);
            contentY += 22;

            string randText = activeRoll.RandomDice.Count > 0
                ? "附加掷骰: " + string.Join(", ", activeRoll.RandomDice)
                : "无附加掷骰 (技能等级为1)";
            GUI.Label(new Rect(contentX, contentY, contentW, 20), randText, IMGUIStyles.ModalBody);
            contentY += 22;

            GUI.Label(new Rect(contentX, contentY, contentW, 20), $"最终最大点数: {activeRoll.FinalValue}", IMGUIStyles.ModalBody);
            contentY += 26;

            // Outcome
            Color outcomeColor = activeRoll.Outcome == "成功" ? IMGUIStyles.OutcomeSuccess
                               : activeRoll.Outcome == "中性" ? IMGUIStyles.OutcomeNeutral
                               : IMGUIStyles.OutcomeFail;
            var outcomeStyle = new GUIStyle(IMGUIStyles.ModalTitle);
            outcomeStyle.fontSize = 16;
            outcomeStyle.normal.textColor = outcomeColor;
            GUI.Label(new Rect(contentX, contentY, contentW, 24), $"判定结果: {activeRoll.Outcome}", outcomeStyle);
            contentY += 32;

            // Button
            float btnW = 100;
            float btnH = 32;
            float btnX = modalX + (modalW - btnW) / 2f;
            float btnY = modalY + modalH - 48;
            var btnRect = new Rect(btnX, btnY, btnW, btnH);

            if (IMGUIStyles.DrawTechnicalButton(btnRect, "确定", mousePos, IMGUIStyles.PrimaryColor, IMGUIStyles.ExecuteBtnHover, IMGUIStyles.ExecuteLabel))
            {
                gameManager.OnRollAckClicked();
            }
        }
    }
}

