#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using SSNoir.Core;
using UnityEngine;

namespace SSNoir.IMGUI
{
    // 只管理呈现时间与缓存，不执行任何游戏结算。
    internal sealed class SpotlightSheetPlayer
    {
        // 与 UI Lab 的 sRGB 色值一致，进入 Linear 项目时只转换一次。
        private static readonly Color SheetPaper = new Color(232f/255, 221f/255, 187f/255).linear;
        private static readonly Color SheetInk = new Color(52f/255, 45f/255, 37f/255).linear;
        private static readonly Color SheetLine = new Color(102f/255, 82f/255, 56f/255).linear;
        private Font? _sheetFont;
        private Texture2D? _paperTexture;
        private SpotlightCard? _card;
        private float _start;
        private float _skip;
        private readonly Dictionary<string, Texture2D> _images = new();
        private float Elapsed => Time.unscaledTime - _start + _skip;
        private float Duration => (_card?.Paragraphs.Count ?? 0) * 1.35f;
        public void Reset() { _card = null; _skip = 0; }
        public bool Advance(SpotlightCard card)
        {
            Begin(card);
            if (Elapsed >= Duration) return true;
            _skip += Math.Max(0, Duration - Elapsed);
            return false;
        }
        private void Begin(SpotlightCard card)
        {
            // Begin 只由运行期 Draw/Advance 调用；构造及静态初始化禁止加载 Unity 资源。
            if (_sheetFont == null)
                _sheetFont = Resources.Load<Font>("Fonts/SourceHanSerifCN-Regular")
                    ?? throw new InvalidOperationException("单页缺少正文字体");
            if (ReferenceEquals(card, _card)) return;
            _card = card; _start = Time.unscaledTime; _skip = 0;
            foreach (var paragraph in card.Paragraphs)
                if (paragraph.Image.Length > 0 && !_images.ContainsKey(paragraph.Image))
                    _images.Add(paragraph.Image, Resources.Load<Texture2D>(paragraph.Image)
                        ?? throw new InvalidOperationException("Spotlight 图片不存在：" + paragraph.Image));
        }
        private GUIStyle Text(int size)
        {
            var style = new GUIStyle(IMGUIStyles.ModalBody)
                { fontSize = IMGUIStyles.FontSize(size), alignment = TextAnchor.UpperLeft, wordWrap = true, font = _sheetFont };
            style.normal.textColor = SheetInk;
            return style;
        }
        private static void Label(Rect rect, string text, GUIStyle style, float alpha = 1)
        {
            var old = GUI.color; GUI.color = new Color(1, 1, 1, alpha);
            IMGUIStyles.DrawLabel(rect, text, style); GUI.color = old;
        }
        private static string EffectText(ActionEffectRecord effect) => effect.Delta.HasValue
            ? (string.IsNullOrEmpty(effect.DisplayLabel) ? effect.Label : effect.DisplayLabel) + " " +
              (effect.Delta.Value > 0 ? "+" : "−") + Math.Abs(effect.Delta.Value)
            : effect.Text;
        private Texture2D PaperTexture()
        {
            if (_paperTexture != null) return _paperTexture;
            const int w = 900, h = 570;
            _paperTexture = new Texture2D(w, h, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Bilinear };
            var pixels = new Color[w * h];
            var baseColor = new Color(232f/255, 221f/255, 187f/255);
            var light = new Color(1, 249f/255, 222f/255);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float dx = (x / (float)w - .15f) / .9f, dy = (1 - y / (float)h - .1f);
                float glow = Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy) / .8f) * .45f;
                var color = Color.Lerp(baseColor, light, glow);
                float grain = y % 3 == 0 ? .987f : 1f;
                float edge = 1 - .035f * Mathf.Pow(Mathf.Abs(x / (float)w - .5f) * 2, 8);
                color.r *= grain * edge; color.g *= grain * edge; color.b *= grain * edge;
                pixels[y * w + x] = color.linear;
            }
            _paperTexture.SetPixels(pixels); _paperTexture.Apply(false, true);
            return _paperTexture;
        }
        public bool Draw(SpotlightCard card)
        {
            Begin(card);
            float unit = Mathf.Min(UIScale.VH / 720f, (UIScale.VW - 64) / 900f);
            var body = Text(Mathf.RoundToInt(20 * unit)); var title = Text(Mathf.RoundToInt(27 * unit)); var tag = Text(Mathf.RoundToInt(16 * unit));
            float width = 900 * unit, inner = width - 76 * unit;
            int imageCount = card.Paragraphs.Count(p => p.Image.Length > 0);
            float imageHeight = imageCount > 0 ? 250 * unit : 0;
            float contentHeight = card.Title.Length > 0 ? title.CalcHeight(new GUIContent(card.Title), inner) + 18 : 0;
            contentHeight += imageHeight > 0 ? imageHeight + 25 * unit : 0;
            foreach (var paragraph in card.Paragraphs) contentHeight += body.CalcHeight(new GUIContent(paragraph.Text), inner) + 9 * unit;
            // 标签按可用宽度换行，与正文一起测量，不挤占按钮。
            float tagX = 0, tagHeight = 0;
            foreach (var effect in card.Effects)
            {
                float w = Mathf.Min(inner, tag.CalcSize(new GUIContent(EffectText(effect))).x + 20 * unit);
                if (tagX > 0 && tagX + w > inner) { tagX = 0; tagHeight += 34 * unit; }
                if (tagHeight == 0) tagHeight = 34 * unit;
                tagX += w + 8 * unit;
            }
            float height = imageCount > 0 ? 570 * unit : Mathf.Max(320 * unit, contentHeight + tagHeight + 100 * unit);
            if (height > UIScale.VH - 40) throw new InvalidOperationException("Spotlight 内容超过单页容量，请缩短正文或拆分单页");
            var paper = UIScale.CenteredModal(width, height);
            var old = GUI.color;
            var oldMatrix = GUI.matrix;
            GUI.color = IMGUIStyles.Blocker; GUI.DrawTexture(new Rect(0, 0, UIScale.VW, UIScale.VH), Texture2D.whiteTexture);
            GUIUtility.RotateAroundPivot(-.35f, paper.center);
            IMGUIStyles.DrawShadow(paper, new Vector2(5, 7), .5f);
            GUI.color = Color.white; GUI.DrawTexture(paper, PaperTexture());
            IMGUIStyles.DrawOutline(paper, 1, new Color(SheetLine.r, SheetLine.g, SheetLine.b, .15f));
            float y = paper.y + 36 * unit, x = paper.x + 38 * unit;
            if (card.Title.Length > 0)
            {
                float h = title.CalcHeight(new GUIContent(card.Title), inner);
                Label(new Rect(x, y, inner, h), card.Title, title); y += h + 18;
            }
            if (imageCount > 0)
            {
                float w = (inner - (imageCount - 1) * 17 * unit) / imageCount;
                int imageIndex = 0;
                for (int i = 0; i < card.Paragraphs.Count; i++)
                {
                    var paragraph = card.Paragraphs[i]; if (paragraph.Image.Length == 0) continue;
                    float alpha = Mathf.Clamp01((Elapsed - i * 1.35f) / .45f);
                    var panel = new Rect(x + imageIndex * (w + 17 * unit), y, w, imageHeight);
                    IMGUIStyles.DrawOutline(panel, 1, new Color(SheetLine.r, SheetLine.g, SheetLine.b, alpha * .6f));
                    GUI.color = new Color(SheetInk.r, SheetInk.g, SheetInk.b, alpha);
                    GUI.DrawTexture(new Rect(x + imageIndex++ * (w + 17 * unit), y, w, imageHeight), _images[paragraph.Image], ScaleMode.ScaleToFit, true);
                }
                GUI.color = Color.white; y += imageHeight + 25 * unit;
            }
            for (int i = 0; i < card.Paragraphs.Count; i++)
            {
                var paragraph = card.Paragraphs[i]; float h = body.CalcHeight(new GUIContent(paragraph.Text), inner);
                if (Elapsed >= i * 1.35f + (paragraph.Image.Length > 0 ? .22f : 0))
                    Label(new Rect(x, y, inner, h), paragraph.Text, body);
                y += Mathf.Max(h, 34 * unit) + 9 * unit;
            }
            bool ready = Elapsed >= Duration;
            if (ready)
            {
                y += 4 * unit;
                float tx = 0;
                foreach (var effect in card.Effects)
                {
                    string text = EffectText(effect); float w = Mathf.Min(inner, tag.CalcSize(new GUIContent(text)).x + 20 * unit);
                    if (tx > 0 && tx + w > inner) { tx = 0; y += 34 * unit; }
                    var rect = new Rect(x + tx, y, w, 28 * unit);
                    GUI.color = SheetLine; GUI.DrawTexture(rect, Texture2D.whiteTexture);
                    GUI.color = SheetPaper; GUI.DrawTexture(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), Texture2D.whiteTexture);
                    GUI.color = Color.white; Label(new Rect(rect.x + 10 * unit, rect.y + 2 * unit, rect.width - 20 * unit, 26 * unit), text, tag); tx += w + 8 * unit;
                }
            }
            var button = new Rect(paper.center.x - 120 * unit, paper.yMax - 65 * unit, 240 * unit, 40 * unit);
            bool hovered = ready && button.Contains(Event.current.mousePosition);
            bool clicked = hovered && Event.current.type == EventType.MouseDown && Event.current.button == 0;
            var buttonStyle = Text(Mathf.RoundToInt(18 * unit));
            buttonStyle.alignment = TextAnchor.MiddleCenter;
            bool close = ready && IMGUIStyles.DrawTechnicalButton(button, "收 起", hovered, clicked, SheetInk,
                new Color(SheetInk.r, SheetInk.g, SheetInk.b, .08f), buttonStyle);
            if (!ready && Event.current.type == EventType.MouseDown && Event.current.button == 0 && paper.Contains(Event.current.mousePosition))
            { Advance(card); Event.current.Use(); }
            GUI.matrix = oldMatrix;
            GUI.color = old;
            return close;
        }
    }
}
