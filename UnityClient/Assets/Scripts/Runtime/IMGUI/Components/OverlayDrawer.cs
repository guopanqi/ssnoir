#nullable enable
using UnityEngine;

namespace SSNoir.IMGUI
{
    public static class OverlayDrawer
    {
        public static void DrawToast(string message, float timer)
        {
            if (timer <= 0 || string.IsNullOrEmpty(message)) return;

            float toastH = 36f;
            float toastW = 400f;
            float toastX = (Screen.width - toastW) / 2f;
            float toastY = 20f;
            var toastRect = new Rect(toastX, toastY, toastW, toastH);

            GUI.color = IMGUIStyles.ToastBg;
            GUI.DrawTexture(toastRect, Texture2D.whiteTexture);
            GUI.color = new Color(0.9f, 0.25f, 0.25f, 1f);
            DrawOutline(toastRect, 1);
            GUI.color = Color.white;

            GUI.Label(toastRect, message, IMGUIStyles.ToastLabel);
        }

        public static void DrawCursorFollower(SSNoirGameManager gameManager)
        {
            var selected = gameManager.SelectedResource;
            if (selected == null) return;

            float overlayW = selected.Type == "die" ? 50f : 100f;
            float overlayH = 28f;
            var mousePos = Event.current.mousePosition;
            var rect = new Rect(mousePos.x + 15, mousePos.y + 15, overlayW, overlayH);

            GUI.color = new Color(0.9f, 0.75f, 0.2f, 0.9f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;

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
            GUI.color = new Color(0.5f, 0.5f, 0.65f, 1f);
            DrawOutline(modalRect, 2);
            GUI.color = Color.white;

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

            bool btnHover = btnRect.Contains(mousePos);
            Color btnBg = btnHover ? new Color(0.3f, 0.5f, 0.7f, 1f) : new Color(0.2f, 0.4f, 0.6f, 1f);
            Color btnBorder = btnHover ? new Color(0.5f, 0.7f, 0.9f, 1f) : new Color(0.4f, 0.6f, 0.8f, 1f);

            GUI.color = btnBg;
            GUI.DrawTexture(btnRect, Texture2D.whiteTexture);
            GUI.color = btnBorder;
            DrawOutline(btnRect, 1);
            GUI.color = Color.white;

            GUI.Label(btnRect, "确定", IMGUIStyles.ExecuteLabel);

            if (btnHover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                gameManager.OnRollAckClicked();
                Event.current.Use();
            }
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
