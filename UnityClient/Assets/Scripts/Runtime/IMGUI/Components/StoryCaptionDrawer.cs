#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSNoir.IMGUI
{
    // 共用字体与排字；剧场无底，世界旁白贴底避让，Dialogue 用操作栏上方的矩形底。
    public static class StoryCaptionDrawer
    {
        public enum Variant { Theatre, Narration, Dialogue }
        private static Font? _bodyFont, _nameFont;
        public static readonly Color BodyColor = new(.945f, .925f, .878f);
        public static Color SpeakerColor(string speaker) => (speaker == "尼尔" || speaker == "Neil")
            ? new Color(.94f, .81f, .54f) : new Color(.83f, .86f, .91f);

        public static void DrawWorldNarration(string text, Rect bottomGap)
        {
            float unit = UIScale.VH / 900f;
            Rect area = UIScale.SafeArea;
            // 底栏几何由 HandPanelDrawer 提供，不在字幕里复制左右 UI 的宽度算法。
            if (bottomGap.width >= 240f * unit)
            {
                area.xMin = bottomGap.xMin + 8f * unit;
                area.xMax = bottomGap.xMax - 8f * unit;
                area.yMax = bottomGap.yMax;
            }
            else
            {
                // 同伴或物品确实占满底部时，才紧贴操作栏上沿显示。
                area.yMax -= HandPanelDrawer.ReservedHeight + 8f * unit;
            }
            Draw(area, text, maxRows: 6, variant: Variant.Narration);
        }

        // Dialogue 的调用者已隐藏世界操作栏，不为不存在的底栏留空。
        public static Rect DialogueArea => UIScale.SafeArea;

        public static void Draw(Rect area, string text, string speaker = "", Color? speakerColor = null,
            float opacity = 1f, int visibleCharacters = int.MaxValue, float elapsed = -1f,
            bool fullyRevealed = true, bool inverted = false, string hint = "", int maxRows = 3, Variant variant = Variant.Dialogue)
        {
            if (string.IsNullOrWhiteSpace(text) || area.width <= 0f || area.height <= 0f) return;
            _bodyFont ??= Resources.Load<Font>("Fonts/SourceHanSerifCN-Regular")
                ?? throw new InvalidOperationException("story caption font missing");
            _nameFont ??= Resources.Load<Font>("Fonts/SourceHanSerifCN-SemiBold")
                ?? throw new InvalidOperationException("story caption name font missing");
            Color previous = GUI.color;
            try
            {
                GUI.color = Color.white;
                float unit = UIScale.VH / 900f;
                float bodySize = variant == Variant.Narration ? 26f
                    : variant == Variant.Dialogue ? 30f : 36.8f;
                int size = Mathf.Max(1, Mathf.RoundToInt(bodySize * unit));
                var body = new GUIStyle(IMGUIStyles.ModalBody)
                {
                    font = _bodyFont, fontSize = IMGUIStyles.FontSize(size), fontStyle = FontStyle.Normal,
                    alignment = TextAnchor.UpperLeft, wordWrap = false, richText = false,
                    padding = new RectOffset(), margin = new RectOffset()
                };
                body.normal.textColor = inverted ? IMGUIStyles.Ink : BodyColor;
                float width = variant == Variant.Narration
                    ? Mathf.Max(1f, area.width - 32f * unit)
                    : variant == Variant.Theatre ? area.width * .86f : Mathf.Min(area.width * .90f, 1480f * unit);
                float left = area.center.x - width * .5f;
                var rows = Rows(text, body, width);
                float scale = 1f;
                while (rows.Count > maxRows && scale > .701f)
                {
                    scale = Mathf.Max(.7f, scale - .05f);
                    body.fontSize = IMGUIStyles.FontSize(Mathf.Max(1, Mathf.RoundToInt(size * scale)));
                    rows = Rows(text, body, width);
                }
                if (rows.Count > maxRows)
                    throw new InvalidOperationException($"story caption exceeds {maxRows} rows: {speaker} / {text}");
                float lineHeight = bodySize * unit * 1.55f * scale;
                float nameHeight = string.IsNullOrEmpty(speaker) ? 0f : 22.4f * unit * 1.55f + 8f * unit;
                float hintHeight = string.IsNullOrEmpty(hint) ? 0f : 24f * unit;
                float bottom = area.yMax - (variant == Variant.Theatre ? 31.5f : variant == Variant.Dialogue ? 20f : 8f) * unit;
                float top = bottom - rows.Count * lineHeight - nameHeight - hintHeight;
                if (variant != Variant.Theatre)
                {
                    // 世界中的字幕仅用平整的半透明矩形，不画柔边、阴影、描边或纸质装饰。
                    float pad = 12f * unit;
                    var panel = new Rect(left - pad, top - pad, width + 2f * pad,
                        bottom - top + 2f * pad);
                    GUI.color = inverted ? new Color(.96f, .94f, .89f, .64f * opacity)
                        : new Color(0f, 0f, 0f, .64f * opacity);
                    GUI.DrawTexture(panel, Texture2D.whiteTexture);
                }
                GUI.color = Color.white;
                if (nameHeight > 0f)
                {
                    var name = new GUIStyle(body) { font = _nameFont,
                        fontSize = IMGUIStyles.FontSize(Mathf.Max(1, Mathf.RoundToInt(22.4f * unit))) };
                    name.normal.textColor = inverted ? IMGUIStyles.Ink : speakerColor ?? SpeakerColor(speaker);
                    float spacing = .35f * 22.4f * unit;
                    float total = Width(speaker, name) + spacing * Math.Max(0, speaker.Length - 1);
                    float x = area.center.x - total * .5f;
                    foreach (char c in speaker)
                    {
                        float w = Width(c.ToString(), name);
                        Glyph(new Rect(x, top, w + 2, nameHeight), c.ToString(), name, opacity, inverted);
                        x += w + spacing;
                    }
                }
                int index = 0;
                for (int row = 0; row < rows.Count; row++)
                {
                    float x = left + (width - Width(rows[row], body)) * .5f;
                    foreach (char c in rows[row])
                    {
                        float w = Width(c.ToString(), body);
                        float alpha = index < visibleCharacters ? 1f : 0f;
                        if (!fullyRevealed && elapsed >= 0f)
                            alpha *= Mathf.Clamp01((elapsed - index * .048f) / .3f);
                        Glyph(new Rect(x, top + nameHeight + row * lineHeight, w + 2, lineHeight),
                            c.ToString(), body, alpha * opacity, inverted);
                        x += w; index++;
                    }
                }
                if (hintHeight > 0f)
                {
                    var tip = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter,
                        fontSize = IMGUIStyles.FontSize(Mathf.Max(1, Mathf.RoundToInt(15f * unit))) };
                    tip.normal.textColor = new Color(body.normal.textColor.r, body.normal.textColor.g,
                        body.normal.textColor.b, .55f);
                    GUI.color = new Color(1, 1, 1, opacity);
                    IMGUIStyles.DrawLabel(new Rect(left, bottom - hintHeight, width, hintHeight), hint, tip);
                }
            }
            finally { GUI.color = previous; }
        }

        private static List<string> Rows(string text, GUIStyle style, float width)
        {
            var rows = new List<string>(); string row = ""; float used = 0f;
            foreach (char c in text)
            {
                if (c == '\n') { rows.Add(row); row = ""; used = 0f; continue; }
                float w = Width(c.ToString(), style);
                if (used + w > width && row.Length > 0) { rows.Add(row); row = ""; used = 0f; }
                row += c; used += w;
            }
            rows.Add(row); return rows;
        }
        private static float Width(string text, GUIStyle style) => style.CalcSize(new GUIContent(text)).x;
        private static void Glyph(Rect rect, string text, GUIStyle style, float alpha, bool inverted)
        {
            if (alpha <= 0f) return;
            Color ink = style.normal.textColor;
            style.normal.textColor = inverted ? Color.white : Color.black;
            GUI.color = new Color(1, 1, 1, alpha * .85f);
            IMGUIStyles.DrawLabel(new Rect(rect.x + 1f, rect.y + 2f, rect.width, rect.height), text, style);
            style.normal.textColor = ink; GUI.color = new Color(1, 1, 1, alpha);
            IMGUIStyles.DrawLabel(rect, text, style);
        }
    }
}
