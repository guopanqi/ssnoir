#nullable enable
using UnityEngine;

namespace SSNoir.IMGUI
{
    // 非阻塞 banter 的世界气泡：解析说话人锚点 → 在其上方画气泡。
    // 阻塞 play-dialogue! 由 DialogueStageDrawer 使用立绘舞台呈现，不与这里共用外观。
    public static class DialogueBubbleDrawer
    {
        private const float BubbleWidth = 320f;

        // 旁白说话人：内容里用「世界」写不属于任何人的叙述句。它没有身体，也就没有锚点，
        // 画成一张不署名的叙述纸条贴在底部，而不是当作一个解析不到的角色报错。
        private const string NarratorSpeaker = "世界";
        private const float NarrationWidth = 560f;

        // 非阻塞:把 BanterPlayer 当前可见的每个气泡画在各自说话人锚点上方。
        public static void DrawBanter(
            BanterPlayer banter,
            DialogueAnchors anchors,
            System.Action<string> reportRemoteFallback)
        {
            foreach (var bubble in banter.Visible)
            {
                bool usedRemoteFallback = DrawBubble(
                    bubble.Line.Speaker,
                    bubble.Line.Text,
                    anchors,
                    bubble.AllowsRemoteParticipants,
                    fallsBackToRemoteParticipant: true);
                if (usedRemoteFallback && !bubble.RemoteFallbackWarningIssued)
                {
                    reportRemoteFallback(bubble.Line.Speaker);
                    bubble.RemoteFallbackWarningIssued = true;
                }
            }
        }

        private static bool DrawBubble(
            string speaker,
            string text,
            DialogueAnchors anchors,
            bool allowsRemoteParticipant = false,
            bool fallsBackToRemoteParticipant = false)
        {
            if (speaker == NarratorSpeaker)
            {
                DrawNarration(text);
                return false;
            }

            bool usedRemoteFallback = false;
            if (!anchors.TryResolve(speaker, out var anchor))
            {
                if (!allowsRemoteParticipant && !fallsBackToRemoteParticipant)
                {
                    throw new System.InvalidOperationException(
                        $"对话说话人无法解析到屏幕锚点: '{speaker}'"
                        + "(必须是在场队员、当前可见的场景节点,或你正身处其中的容器。"
                        + $"不属于任何人的叙述句用说话人「{NarratorSpeaker}」;"
                        + "确实不在场的人开口,内容里显式改用对应的 remote 调用!)");
                }

                usedRemoteFallback = !allowsRemoteParticipant;
                anchor = DrawRemoteParticipantCard(speaker);
            }

            // 对话气泡是"递到面前的一张纸"：Paper 底 + PaperInk 字 + 硬投影
            var bodyStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                wordWrap = true,
                fontSize = IMGUIStyles.FontSize(16),
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
                fontSize = IMGUIStyles.FontSize(14),
                alignment = TextAnchor.MiddleLeft,
            };
            IMGUIStyles.ApplyStrongFont(nameStyle);
            nameStyle.normal.textColor = IMGUIStyles.PaperTextSecondary;
            GUI.Label(new Rect(rect.x + 12f, rect.y + 6f, textW, 20f), speaker, nameStyle);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 26f, textW, textH), text, bodyStyle);
            return usedRemoteFallback;
        }

        // 旁白：不署名、不指向任何人，横向居中贴在底部手牌区上方。
        private static void DrawNarration(string text)
        {
            var bodyStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                wordWrap = true,
                fontSize = IMGUIStyles.FontSize(16),
                alignment = TextAnchor.UpperLeft,
            };
            bodyStyle.normal.textColor = IMGUIStyles.PaperInk;

            float textW = NarrationWidth - 32f;
            float textH = bodyStyle.CalcHeight(new GUIContent(text), textW);
            float h = 14f + textH + 14f;
            var rect = new Rect((UIScale.VW - NarrationWidth) / 2f, UIScale.VH - 175f - h - 16f, NarrationWidth, h);

            IMGUIStyles.DrawShadow(rect, new Vector2(5f, 6f), 0.50f);
            GUI.color = IMGUIStyles.Paper;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(rect.x + 16f, rect.y + 14f, textW, textH), text, bodyStyle);
        }

        // 显式场外对话/插话的未在场说话人，以一张临时侧边卡进入画面。
        // 这不是场景节点，不参与交互或导航，只提供清晰的对话锚点。
        private static Rect DrawRemoteParticipantCard(string speaker)
        {
            const float cardW = 176f;
            const float cardH = 148f;
            // 放在左侧中段，避开左下角的主角行动簇；气泡仍从卡片上方冒出。
            var card = new Rect(28f, UIScale.VH * 0.42f, cardW, cardH);

            IMGUIStyles.DrawShadow(card, new Vector2(5f, 6f), 0.50f);
            GUI.color = new Color(0.024f, 0.031f, 0.047f, 0.96f);
            GUI.DrawTexture(card, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawDoubleOutline(card, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.72f));

            var titleStyle = new GUIStyle(IMGUIStyles.CardTitle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = IMGUIStyles.FontSize(20),
            };
            IMGUIStyles.ApplyStrongFont(titleStyle);
            GUI.Label(new Rect(card.x + 12f, card.y + 42f, card.width - 24f, 42f), speaker, titleStyle);

            return card;
        }
    }
}
