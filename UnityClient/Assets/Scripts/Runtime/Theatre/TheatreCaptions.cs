#nullable enable
using System;
using System.Collections.Generic;
using SSNoir.Theatre;
using SSNoir.IMGUI;
using UnityEngine;

namespace SSNoir.UnityTheatre
{
    // Reads caption state only; no dialogue waits or independent animation clock.
    internal sealed class TheatreCaptions
    {
        private const int MaxCaptionRows = 2;
        private const float MinCaptionScale = .7f;
        private readonly Font _captionFont = Resources.Load<Font>("Fonts/SourceHanSerifCN-Regular") ?? throw new InvalidOperationException("theatre caption font missing");
        private readonly Font _nameFont = Resources.Load<Font>("Fonts/SourceHanSerifCN-SemiBold") ?? throw new InvalidOperationException("theatre name font missing");
        public void Draw(TheatreSession session, Rect stage, float fade)
        {
            var line = session.Caption;
            if (line == null) return;
            float unit = stage.height / 900f;
            var text = new GUIStyle(IMGUIStyles.ModalBody) { font = _captionFont, alignment = TextAnchor.UpperLeft, wordWrap = false,
                fontStyle = FontStyle.Normal, padding = new RectOffset(), fontSize = IMGUIStyles.FontSize(Mathf.RoundToInt(36.8f * unit)) };
            text.normal.textColor = new Color(.945f, .925f, .878f);
            var name = new GUIStyle(text) { font = _nameFont,
                fontSize = IMGUIStyles.FontSize(Mathf.RoundToInt(22.4f * unit)) };
            name.normal.textColor = new Color(line.CaptionColor.R, line.CaptionColor.G, line.CaptionColor.B);
            float width = stage.width * .86f, left = stage.x + stage.width * .07f, lineHeight = 36.8f * unit * 1.55f;
            // Layout the complete sentence first, so revealing letters cannot reflow already visible text.
            // At most two rows plus the name; longer lines must be split by the author.
            int originalSize = text.fontSize;
            var rows = LayoutRows(line.Text, text, width);
            float scale = 1f;
            while (rows.Count > MaxCaptionRows && scale > MinCaptionScale + 0.001f)
            {
                scale -= 0.05f;
                text.fontSize = Mathf.RoundToInt(originalSize * scale);
                rows = LayoutRows(line.Text, text, width);
            }
            if (rows.Count > MaxCaptionRows)
                throw new InvalidOperationException($"theatre caption exceeds {MaxCaptionRows} rows: " + line.Target);
            float nameHeight = 22.4f * unit * 1.55f;
            float bottom = stage.yMax - stage.height * .035f;
            float top = bottom - nameHeight - 8f * unit - rows.Count * lineHeight * scale;
            DrawSpaced(line.Target, left, top, width, nameHeight, name, .35f * 22.4f * unit, fade);
            int index = 0;
            foreach (string words in rows)
            {
                float rowWidth = WidthOf(words, text);
                float x = left + (width - rowWidth) / 2f;
                foreach (char c in words)
                {
                    float glyph = WidthOf(c.ToString(), text);
                    float alpha = session.CaptionIsRevealed ? 1f : index < session.VisibleCharacters ? Mathf.Clamp01((session.CaptionTime - index * .048f) / .3f) : 0f;
                    DrawGlyph(new Rect(x, top + nameHeight + 8f * unit, glyph + 2f, lineHeight * scale), c.ToString(), text, alpha * fade);
                    x += glyph; index++;
                }
                top += lineHeight * scale;
            }
        }
        private static List<string> LayoutRows(string content, GUIStyle style, float width)
        {
            var rows = new List<string>(); string row = ""; float used = 0;
            foreach (char c in content)
            {
                float glyph = WidthOf(c.ToString(), style);
                if (used + glyph > width && row.Length > 0) { rows.Add(row); row = ""; used = 0; }
                row += c; used += glyph;
            }
            rows.Add(row);
            return rows;
        }
        private static float WidthOf(string glyph, GUIStyle style)
            => style.CalcSize(new GUIContent(glyph)).x;
        private static void DrawSpaced(string words, float left, float top, float width, float height, GUIStyle style, float spacing, float fade)
        {
            float total = style.CalcSize(new GUIContent(words)).x + spacing * (words.Length - 1);
            float x = left + (width - total) / 2f;
            foreach (char c in words)
            {
                float glyph = style.CalcSize(new GUIContent(c.ToString())).x;
                DrawGlyph(new Rect(x, top, glyph + 2f, height), c.ToString(), style, fade); x += glyph + spacing;
            }
        }
        private static void DrawGlyph(Rect rect, string glyph, GUIStyle style, float alpha)
        {
            var color = style.normal.textColor;
            style.normal.textColor = Color.black; GUI.color = new Color(1, 1, 1, alpha * .9f);
            GUI.Label(new Rect(rect.x + 1, rect.y + 2, rect.width, rect.height), glyph, style);
            style.normal.textColor = color; GUI.color = new Color(1, 1, 1, alpha);
            GUI.Label(rect, glyph, style);
        }
    }
}
