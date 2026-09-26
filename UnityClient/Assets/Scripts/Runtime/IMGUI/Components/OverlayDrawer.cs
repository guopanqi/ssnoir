#nullable enable
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    public static class OverlayDrawer
    {
        /// <summary>通知摞在左上角，顶在场景标注带下面——右上角是钉住条的地盘。</summary>
        public static void DrawNotifications(NotificationCenter notificationCenter, float topY)
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
                float cardX = UIScale.SafeArea.x + 24f;
                float cardY = topY + i * (cardH + 8f);
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
                IMGUIStyles.DrawLabel(cardRect, notif.Text, style);
            }
        }

        public static void DrawCursorFollower(SSNoirGameManager gameManager)
        {
            var selected = gameManager.SelectedResource;
            var mousePos = Event.current.mousePosition;
            if (selected == null || !gameManager.IsDraggingResource)
            {
                Cursor.visible = true;
                return;
            }

            Cursor.visible = true;
            // 跟在指针上的那块和手牌里拿起来的那块必须一样大，否则拖起来会「变形」。
            float tokenSize = HandPanelDrawer.TokenSize;
            var rect = new Rect(mousePos.x - tokenSize * 0.5f, mousePos.y - tokenSize * 0.5f, tokenSize, tokenSize);

            // 拖拽 ghost 使用与手牌/slot 同一族方块，不退回成文字标签。
            IMGUIStyles.DrawShadow(rect, new Vector2(2f, 2f), 0.45f);
            GUI.color = new Color(0.024f, 0.031f, 0.047f, 0.92f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(rect, 2f, IMGUIStyles.Gold);

            if (selected.Type == "die")
            {
                var style = new GUIStyle(IMGUIStyles.SlotLabel)
                {
                    fontSize = IMGUIStyles.FontSize(24),
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = IMGUIStyles.Gold }
                };
                IMGUIStyles.ApplyStrongFont(style);
                IMGUIStyles.DrawLabel(rect, selected.Value.ToString(), style);
            }
            else
            {
                string small = selected.ItemName == "金钱"
                    ? $"${selected.Value}"
                    : $"x{selected.Value}";
                var bigStyle = new GUIStyle(IMGUIStyles.SlotLabel)
                {
                    fontSize = IMGUIStyles.FontSize(24),
                    alignment = TextAnchor.UpperCenter,
                    normal = { textColor = IMGUIStyles.Gold }
                };
                IMGUIStyles.ApplyStrongFont(bigStyle);
                var bigRect = new Rect(rect.x, rect.y + 3f, rect.width, 28f);
                if (!ItemIconLibrary.TryDraw(bigRect, selected.ItemName, IMGUIStyles.Gold, 0.80f))
                    IMGUIStyles.DrawLabel(bigRect, ItemSymbol(selected.ItemName), bigStyle);

                var smallStyle = new GUIStyle(IMGUIStyles.SlotLabel)
                {
                    fontSize = IMGUIStyles.FontSize(13),
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = IMGUIStyles.Gold }
                };
                IMGUIStyles.DrawLabel(new Rect(rect.x, rect.y + 31f, rect.width, 16f), small, smallStyle);

                // 手牌格底下写着名字，拖起来的这块也得写——否则一拿起来就不知道拿的是哪一件。
                var captionStyle = new GUIStyle(IMGUIStyles.SlotLabel)
                {
                    fontSize = IMGUIStyles.FontSize(11),
                    alignment = TextAnchor.UpperCenter,
                    normal = { textColor = IMGUIStyles.Gold }
                };
                IMGUIStyles.DrawLabel(
                    new Rect(rect.x - 4f, rect.yMax + 1f, rect.width + 8f, 15f),
                    ItemDisplayName.Short(selected.ItemName), captionStyle);
            }
        }

        private static string ItemSymbol(string name)
        {
            return name switch
            {
                "金钱" => "$",
                "酒" => "酒",
                "香烟" => "烟",
                "药品" => "药",
                _ => name.Length > 0 ? name.Substring(0, 1) : "?"
            };
        }
    }
}
