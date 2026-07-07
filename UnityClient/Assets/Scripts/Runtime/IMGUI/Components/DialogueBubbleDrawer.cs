#nullable enable
using UnityEngine;

namespace SSNoir.IMGUI
{
    // 无状态气泡绘制:解析说话人锚点 → 在其上方画气泡。Banter 与 Conversation 共用同一外观。
    public static class DialogueBubbleDrawer
    {
        private const float BubbleWidth = 320f;

        // 非阻塞:把 BanterPlayer 当前可见的每个气泡画在各自说话人锚点上方。
        public static void DrawBanter(BanterPlayer banter, DialogueAnchors anchors)
        {
            foreach (var bubble in banter.Visible)
                DrawBubble(bubble.Line.Speaker, bubble.Line.Text, anchors);
        }

        // 阻塞:画当前对话行的气泡(点击推进由渲染器 overlay 负责,这里只管外观)。
        public static void DrawConversationLine(string speaker, string text, DialogueAnchors anchors)
        {
            DrawBubble(speaker, text, anchors);
        }

        private static void DrawBubble(string speaker, string text, DialogueAnchors anchors)
        {
            if (!anchors.TryResolve(speaker, out var anchor))
                throw new System.InvalidOperationException(
                    $"对话说话人无法解析到屏幕锚点: '{speaker}'(必须是在场队员或当前可见的场景节点)");

            // 对话气泡是"递到面前的一张纸"：Paper 底 + PaperInk 字 + 硬投影
            var bodyStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                wordWrap = true,
                fontSize = 16,
                alignment = TextAnchor.UpperLeft,
            };
            bodyStyle.normal.textColor = IMGUIStyles.PaperInk;

            float textW = BubbleWidth - 24f;
            float textH = bodyStyle.CalcHeight(new GUIContent(text), textW);
            float h = 26f + textH + 12f;

            float x = Mathf.Clamp(anchor.center.x - BubbleWidth / 2f, 8f, UIScale.VW - BubbleWidth - 8f);
            float y = anchor.y - h - 10f;
            if (y < 8f)
                y = anchor.yMax + 10f;   // 上方没空间就画到锚点下方
            var rect = new Rect(x, y, BubbleWidth, h);

            IMGUIStyles.DrawShadow(rect, new Vector2(5f, 6f), 0.50f);
            GUI.color = IMGUIStyles.Paper;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                font = IMGUIStyles.ChineseFont,
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
            };
            IMGUIStyles.ApplyStrongFont(nameStyle);
            nameStyle.normal.textColor = IMGUIStyles.PaperTextSecondary;
            GUI.Label(new Rect(rect.x + 12f, rect.y + 6f, textW, 20f), speaker, nameStyle);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 26f, textW, textH), text, bodyStyle);
        }
    }
}
