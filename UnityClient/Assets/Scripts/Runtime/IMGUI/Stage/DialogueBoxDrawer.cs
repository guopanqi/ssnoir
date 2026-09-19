#nullable enable
using UnityEngine;

namespace SSNoir.IMGUI.Stage
{
    // 对白框、说话人名牌、缺图人物牌：舞台上"字"的那一层。
    // 对白框属于"句子更新"：每句新台词短促上弹一次并回落，文字随后以打字机逐字显现。
    public static class DialogueBoxDrawer
    {
        public const float BoxBottomMargin = 14f;
        public const float BoxMinHeight = 104f;
        public const float BoxHeightRatio = 0.26f;

        public static void DrawMissingPortrait(Rect rect, string speaker, float reveal)
        {
            GUI.color = new Color(IMGUIStyles.Ink.r, IMGUIStyles.Ink.g, IMGUIStyles.Ink.b, 0.94f * reveal);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var nameStyle = new GUIStyle(IMGUIStyles.CardTitle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = IMGUIStyles.FontSize(34),
                normal = { textColor = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.72f * reveal) }
            };
            IMGUIStyles.DrawLabel(new Rect(rect.x + 24f, rect.center.y - 38f, rect.width - 48f, 76f), speaker, nameStyle);
        }

        public static void Draw(
            string speaker,
            string visibleText,
            string fullText,
            bool isTyping,
            bool isNarration,
            bool isInner,
            bool portraitOnLeft,
            bool isNeon,
            float reveal,
            float negative,
            Color glowColor)
        {
            // 负片里对白框也翻过来：墨底纸字，不然纸框会消失在纸白的环境里。
            bool inverted = negative >= 0.5f;
            var paper = inverted ? IMGUIStyles.Ink : IMGUIStyles.Paper;
            var ink = inverted ? IMGUIStyles.Paper : IMGUIStyles.PaperInk;
            float boxWidth = Mathf.Min(1180f, Mathf.Max(320f, UIScale.SafeArea.width - 80f));
            var bodyStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                wordWrap = true,
                alignment = isNarration ? TextAnchor.MiddleCenter : TextAnchor.UpperLeft,
                fontSize = IMGUIStyles.FontSize(isNarration ? 20 : 22),
                normal = { textColor = new Color(ink.r, ink.g, ink.b, reveal) }
            };
            float textWidth = boxWidth - 72f;
            float textHeight = bodyStyle.CalcHeight(new GUIContent(fullText), textWidth);
            float boxHeight = Mathf.Clamp(
                textHeight + (isNarration ? 62f : 86f),
                BoxMinHeight,
                Mathf.Min(230f, UIScale.VH * BoxHeightRatio));
            // 每句都短促上弹一次以提示文本已更新；人物立绘使用独立计时，不跟着重复淡入。
            float sentenceBounce = (1f - reveal) * 16f - Mathf.Sin(reveal * Mathf.PI) * 5f;
            Rect safe = UIScale.SafeArea;
            float boxY = safe.yMax - boxHeight - BoxBottomMargin + sentenceBounce;
            var box = new Rect(safe.x + (safe.width - boxWidth) / 2f, boxY, boxWidth, boxHeight);

            // 心里话没有框：字直接落在夜色上（负片里落在纸上），没有名牌——没人在说，是他在想。
            if (isInner)
            {
                var thought = inverted ? IMGUIStyles.Ink : IMGUIStyles.Paper;
                bodyStyle.alignment = TextAnchor.MiddleCenter;
                bodyStyle.fontStyle = FontStyle.Italic;
                bodyStyle.normal.textColor = new Color(thought.r, thought.g, thought.b, 0.82f * reveal);
                IMGUIStyles.DrawLabel(new Rect(box.x + 36f, box.y, textWidth, box.height), visibleText, bodyStyle);
                ink = thought;
            }
            else
            {
                IMGUIStyles.DrawShadow(box, new Vector2(8f, 10f), 0.64f * reveal);
                GUI.color = new Color(paper.r, paper.g, paper.b, reveal);
                GUI.DrawTexture(box, Texture2D.whiteTexture);
                GUI.color = Color.white;
                IMGUIStyles.DrawOutline(box, 1.5f, new Color(ink.r, ink.g, ink.b, 0.68f * reveal));

                float textY = box.y + (isNarration ? 28f : 48f);
                IMGUIStyles.DrawLabel(
                    new Rect(box.x + 36f, textY, textWidth, box.yMax - textY - 32f),
                    visibleText,
                    bodyStyle);

                if (!isNarration)
                    DrawSpeakerTab(box, speaker, portraitOnLeft, isNeon, reveal, inverted, glowColor);
            }

            var continueStyle = new GUIStyle(IMGUIStyles.StatusLabel)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = IMGUIStyles.FontSize(11),
                normal = { textColor = new Color(ink.r, ink.g, ink.b, 0.55f * reveal) }
            };
            IMGUIStyles.DrawLabel(
                new Rect(box.xMax - 170f, box.yMax - 26f, 138f, 18f),
                isTyping ? "点击显示全文" : "点击继续",
                continueStyle);
        }

        private static void DrawSpeakerTab(Rect box, string speaker, bool onLeft, bool isNeon, float reveal, bool inverted, Color glowColor)
        {
            var tabFill = inverted ? IMGUIStyles.Paper : IMGUIStyles.Ink;
            var tabText = inverted ? IMGUIStyles.Ink : IMGUIStyles.Paper;
            const float tabWidth = 210f;
            const float tabHeight = 42f;
            float x = onLeft ? box.x + 28f : box.xMax - tabWidth - 28f;
            var tab = new Rect(x, box.y - 22f, tabWidth, tabHeight);

            IMGUIStyles.DrawShadow(tab, new Vector2(4f, 5f), 0.46f * reveal);
            GUI.color = new Color(tabFill.r, tabFill.g, tabFill.b, 0.98f * reveal);
            GUI.DrawTexture(tab, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 霓虹说话人的名牌也当灯管处理：同色描边 + 一圈溢光，和立绘是同一盏灯。负片里灯不发光，只描一道墨边。
            var edge = inverted ? IMGUIStyles.Ink : isNeon ? glowColor : IMGUIStyles.Gold;
            if (isNeon && !inverted)
            {
                float brightness = LampPainter.Brightness(reveal);
                for (int i = 3; i >= 1; i--)
                {
                    var ring = new Rect(tab.x - i * 2f, tab.y - i * 2f, tab.width + i * 4f, tab.height + i * 4f);
                    IMGUIStyles.DrawOutline(ring, 1f, new Color(edge.r, edge.g, edge.b, 0.16f / i * brightness));
                }
                IMGUIStyles.DrawOutline(tab, 1.5f, new Color(edge.r, edge.g, edge.b, 0.92f * brightness));
            }
            else
            {
                IMGUIStyles.DrawOutline(tab, 1.5f, new Color(edge.r, edge.g, edge.b, 0.86f * reveal));
            }

            var nameStyle = new GUIStyle(IMGUIStyles.CardTitle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = IMGUIStyles.FontSize(20),
                normal = { textColor = new Color(tabText.r, tabText.g, tabText.b, reveal) }
            };
            IMGUIStyles.DrawLabel(tab, speaker, nameStyle);
        }
    }
}
