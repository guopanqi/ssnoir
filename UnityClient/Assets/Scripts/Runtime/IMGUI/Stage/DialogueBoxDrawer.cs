#nullable enable
using UnityEngine;

namespace SSNoir.IMGUI.Stage
{
    // 缺图人物牌仍属立绘舞台；台词交给与剧场、旁白共用的字幕组件。
    public static class DialogueBoxDrawer
    {
        public const float BoxBottomMargin = 14f;
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
            float reveal,
            float negative)
        {
            StoryCaptionDrawer.Draw(StoryCaptionDrawer.DialogueArea, fullText,
                isNarration || isInner ? "" : speaker,
                StoryCaptionDrawer.SpeakerColor(speaker), reveal, visibleText.Length,
                inverted: negative >= .5f,
                hint: isTyping ? "点击显示全文" : "点击继续");
        }
    }
}
