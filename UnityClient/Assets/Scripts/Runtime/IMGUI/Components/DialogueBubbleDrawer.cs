#nullable enable
using UnityEngine;

namespace SSNoir.IMGUI
{
    // 非阻塞 banter 的世界气泡：解析说话人锚点 → 在其上方画气泡。
    // 阻塞 play-dialogue! 由 DialogueStageDrawer 使用立绘舞台呈现，不与这里共用外观。
    public static class DialogueBubbleDrawer
    {
        private const float BubbleWidth = 320f;
        private const float BubbleTailHalfWidth = 10f;

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

            // 尾巴不钉在气泡正中，而是指向说话者矩形与气泡之间最近的一对边界点。
            // 人物卡偏到气泡一侧、或气泡因顶边空间不足翻到人物下方时，它仍然准确地"说"自谁。
            GetTail(anchor, rect, out var tailBaseA, out var tailBaseB, out var tailTip);

            var shadowOffset = new Vector2(5f, 6f);
            IMGUIStyles.DrawShadow(rect, shadowOffset, 0.50f);
            DrawTriangle(tailBaseA + shadowOffset, tailBaseB + shadowOffset, tailTip + shadowOffset,
                new Color(0f, 0f, 0f, 0.50f));
            GUI.color = IMGUIStyles.Paper;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            DrawTriangle(tailBaseA, tailBaseB, tailTip, IMGUIStyles.Paper);
            Color outline = new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.68f);
            IMGUIStyles.DrawOutline(rect, 1f, outline);
            IMGUIStyles.DrawLine(tailBaseA, tailTip, outline);
            IMGUIStyles.DrawLine(tailTip, tailBaseB, outline);

            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                font = IMGUIStyles.ChineseFont,
                fontSize = IMGUIStyles.FontSize(14),
                alignment = TextAnchor.MiddleLeft,
            };
            IMGUIStyles.ApplyStrongFont(nameStyle);
            nameStyle.normal.textColor = IMGUIStyles.PaperTextSecondary;
            IMGUIStyles.DrawLabel(new Rect(rect.x + 12f, rect.y + 6f, textW, 20f), speaker, nameStyle);
            IMGUIStyles.DrawLabel(new Rect(rect.x + 12f, rect.y + 26f, textW, textH), text, bodyStyle);
            return usedRemoteFallback;
        }

        // 返回气泡边上的尾巴底边与落在说话人边缘的尖端。Rect.ClosestPoint 的语义不适合
        // "点在矩形内"的情形（它会返回点本身），因此这里明确投到四条边中距离最短的一条。
        private static void GetTail(Rect speaker, Rect bubble, out Vector2 baseA, out Vector2 baseB, out Vector2 tip)
        {
            tip = ClosestPointOnEdge(speaker, bubble.center);
            Vector2 baseCenter = ClosestPointOnEdge(bubble, tip);

            if (Mathf.Approximately(baseCenter.x, bubble.x) || Mathf.Approximately(baseCenter.x, bubble.xMax))
            {
                float y = Mathf.Clamp(baseCenter.y, bubble.y + BubbleTailHalfWidth, bubble.yMax - BubbleTailHalfWidth);
                baseA = new Vector2(baseCenter.x, y - BubbleTailHalfWidth);
                baseB = new Vector2(baseCenter.x, y + BubbleTailHalfWidth);
            }
            else
            {
                float x = Mathf.Clamp(baseCenter.x, bubble.x + BubbleTailHalfWidth, bubble.xMax - BubbleTailHalfWidth);
                baseA = new Vector2(x - BubbleTailHalfWidth, baseCenter.y);
                baseB = new Vector2(x + BubbleTailHalfWidth, baseCenter.y);
            }
        }

        private static Vector2 ClosestPointOnEdge(Rect rect, Vector2 point)
        {
            float left = Mathf.Abs(point.x - rect.x);
            float right = Mathf.Abs(point.x - rect.xMax);
            float top = Mathf.Abs(point.y - rect.y);
            float bottom = Mathf.Abs(point.y - rect.yMax);
            float min = Mathf.Min(left, right, top, bottom);

            if (Mathf.Approximately(min, top))
                return new Vector2(Mathf.Clamp(point.x, rect.x, rect.xMax), rect.y);
            if (Mathf.Approximately(min, bottom))
                return new Vector2(Mathf.Clamp(point.x, rect.x, rect.xMax), rect.yMax);
            if (Mathf.Approximately(min, left))
                return new Vector2(rect.x, Mathf.Clamp(point.y, rect.y, rect.yMax));
            return new Vector2(rect.xMax, Mathf.Clamp(point.y, rect.y, rect.yMax));
        }

        private static void DrawTriangle(Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            if (Event.current.type != EventType.Repaint || IMGUIStyles.PieMaterial == null)
                return;

            IMGUIStyles.PieMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);
            GL.Begin(GL.TRIANGLES);
            GL.Color(color);
            DrawVertex(a);
            DrawVertex(b);
            DrawVertex(c);
            GL.End();
            GL.PopMatrix();
        }

        private static void DrawVertex(Vector2 point)
        {
            Vector2 screenPoint = GUIUtility.GUIToScreenPoint(point);
            GL.Vertex3(screenPoint.x, screenPoint.y, 0f);
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

            // 窄屏上 560 的固定宽度会顶到两边；收进安全区，底边坐在手牌簇上方。
            Rect safe = UIScale.SafeArea;
            float width = Mathf.Min(NarrationWidth, safe.width - 32f);
            float textW = width - 32f;
            float textH = bodyStyle.CalcHeight(new GUIContent(text), textW);
            float h = 14f + textH + 14f;
            var rect = new Rect(
                safe.x + (safe.width - width) / 2f,
                safe.yMax - HandPanelDrawer.ReservedHeight - h - 16f,
                width, h);

            IMGUIStyles.DrawShadow(rect, new Vector2(5f, 6f), 0.50f);
            GUI.color = IMGUIStyles.Paper;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            IMGUIStyles.DrawLabel(new Rect(rect.x + 16f, rect.y + 14f, textW, textH), text, bodyStyle);
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

            // 有霓虹立绘就让人真的露面：卡片本来只写个名字，和场上其他人物的表现不一致。
            var neon = NeonPortraitLibrary.Load(speaker);
            bool hasPortrait = neon != null;
            if (hasPortrait)
            {
                var uv = NeonPortraitLibrary.BustCrop;
                float bustH = card.height - 36f;
                float bustW = bustH * (uv.width / uv.height);
                var bust = new Rect(card.center.x - bustW / 2f, card.y + 6f, bustW, bustH);

                var bleed = new Rect(bust.x - 4f, bust.y - 4f, bust.width + 8f, bust.height + 8f);
                GUI.color = new Color(0.30f, 0.58f, 1f, 0.18f);
                GUI.DrawTextureWithTexCoords(bleed, neon, uv, true);
                // 叠两遍：Alpha From Grayscale 下蓝管偏透，理由同 HUD 半身像。
                GUI.color = Color.white;
                GUI.DrawTextureWithTexCoords(bust, neon, uv, true);
                GUI.color = new Color(1f, 1f, 1f, 0.55f);
                GUI.DrawTextureWithTexCoords(bust, neon, uv, true);
                // 腰部硬切口抹回暗处，名字压在渐变上。
                var hem = new Rect(bust.x - 8f, bust.yMax - bust.height * 0.30f, bust.width + 16f, bust.height * 0.30f);
                GUI.color = new Color(0.024f, 0.031f, 0.047f, 0.96f);
                GUI.DrawTexture(hem, NeonPortraitLibrary.VerticalFade());
                GUI.color = Color.white;
            }

            var titleStyle = new GUIStyle(IMGUIStyles.CardTitle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = IMGUIStyles.FontSize(hasPortrait ? 16 : 20),
            };
            IMGUIStyles.ApplyStrongFont(titleStyle);
            float nameY = hasPortrait ? card.yMax - 30f : card.y + 42f;
            IMGUIStyles.DrawLabel(new Rect(card.x + 12f, nameY, card.width - 24f, 42f), speaker, titleStyle);

            return card;
        }
    }
}
