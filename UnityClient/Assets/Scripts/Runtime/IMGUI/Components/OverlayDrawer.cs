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
                float cardX = UIScale.VW - cardW - 40f;
                float cardY = 75f + i * (cardH + 8f);
                var cardRect = new Rect(cardX, cardY, cardW, cardH);

                // 通知：黑底 HUD 语言。收益/成功=金文字，失败/损失=印章红，警告=赭黄，一般=纸白。
                Color accent;
                switch (notif.Kind)
                {
                    case NotificationKind.Success:
                        accent = IMGUIStyles.Gold;
                        break;
                    case NotificationKind.Error:
                        accent = IMGUIStyles.SealRed;
                        break;
                    case NotificationKind.Warning:
                        accent = IMGUIStyles.OddsNeutral;
                        break;
                    case NotificationKind.Info:
                    default:
                        accent = IMGUIStyles.Paper;
                        break;
                }

                GUI.color = new Color(IMGUIStyles.HudBg.r, IMGUIStyles.HudBg.g, IMGUIStyles.HudBg.b, alpha * IMGUIStyles.ModalOpacity);
                GUI.DrawTexture(cardRect, Texture2D.whiteTexture);
                GUI.color = Color.white;
                IMGUIStyles.DrawOutline(cardRect, 1f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f * alpha));

                var style = new GUIStyle(IMGUIStyles.ToastLabel);
                style.normal.textColor = new Color(accent.r, accent.g, accent.b, alpha);
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

            // 拖拽跟随物：暗场景里的白纸片（白底黑字反转）+ 硬投影 + 金描边（选中态）
            IMGUIStyles.DrawShadow(rect, new Vector2(2f, 2f), 0.45f);
            GUI.color = IMGUIStyles.Paper;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(rect, 1f, IMGUIStyles.Gold);

            string text = selected.Type == "die" ? $"D{selected.Value}" : selected.ItemName;
            var style = new GUIStyle(IMGUIStyles.CursorFollower);
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = IMGUIStyles.PaperInk;
            GUI.Label(rect, text, style);
        }

    }
}
