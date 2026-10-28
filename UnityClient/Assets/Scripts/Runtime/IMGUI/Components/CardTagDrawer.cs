#nullable enable
using System.Collections.Generic;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    // 节点的统一 Tag 区。Clock 和休息阻塞仍保留各自规则，只在这里统一呈现。
    public static class CardTagDrawer
    {
        private readonly struct Item
        {
            public readonly string Text;
            public readonly bool Attention;
            public readonly GameClock? Clock;
            public readonly Color? Accent;
            public Item(string text, bool attention = false, GameClock? clock = null, Color? accent = null)
            { Text = text; Attention = attention; Clock = clock; Accent = accent; }
        }

        private const float Padding = 8f;
        private const float Gap = 6f;
        private const float Inset = 10f;
        private const float RowGap = 5f;
        private const float CardGap = 6f;
        private static float Height => IMGUIStyles.FontSize(13) + 10f;
        private static GUIStyle Style() => new GUIStyle(GUI.skin.label)
        {
            font = IMGUIStyles.ChineseFont,
            fontSize = IMGUIStyles.FontSize(13),
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Clip,
            wordWrap = false,
            padding = new RectOffset(),
            margin = new RectOffset()
        };

        private static List<Item> BuildItems(GameNode node, bool mustHandle, List<GameClock>? clocks)
        {
            var items = new List<Item>();
            var labels = new HashSet<string>();
            bool hasRequired = false;
            if (node.Risk != null)
            {
                Color riskColor = node.Risk.Level switch
                {
                    ActionRiskLevel.Safe => IMGUIStyles.OddsSuccess,
                    ActionRiskLevel.Risky => IMGUIStyles.Gold,
                    _ => IMGUIStyles.OddsFail
                };
                items.Add(new Item(node.Risk.Label, accent: riskColor));
                labels.Add(node.Risk.Label);
            }
            foreach (var tag in node.Tags)
            {
                if (string.IsNullOrWhiteSpace(tag) || tag == "人物" || tag == "地点") continue;
                string shown = tag switch
                {
                    "工作" or "紧急" or "必须处理" or "高风险" or "低风险" or "Scenario" => UiText.Get(tag),
                    _ => tag,
                };
                if (tag == "必须处理") hasRequired = true;
                if (labels.Add(shown)) items.Add(new Item(shown, tag == "紧急" || tag == "必须处理"));
            }
            if (node.Resolve != null)
                foreach (var mod in node.Resolve.DifficultyModifiers)
                    items.Add(new Item($"{mod.Reason} {(mod.Value > 0 ? "+" : "")}{mod.Value}"));
            if (mustHandle && !hasRequired)
                items.Add(new Item(UiText.Get("必须处理"), attention: true));
            foreach (var clock in clocks ?? node.Clocks)
                if (clock.Priority == ClockPriority.Secondary)
                    items.Add(new Item($"{clock.ShownLabel} {clock.Current}/{clock.Max}", clock: clock));
            return items;
        }

        private static List<Rect> Layout(float width, List<Item> items, GUIStyle style)
        {
            var rects = new List<Rect>();
            float available = Mathf.Max(1f, width - Inset * 2f);
            float x = 0f, y = 0f;
            foreach (var item in items)
            {
                float iconWidth = item.Clock != null ? Height : 0f;
                float w = Mathf.Min(available, Mathf.Ceil(style.CalcSize(new GUIContent(item.Text)).x)
                    + Padding * 2f + iconWidth);
                if (x > 0f && x + w > available)
                { x = 0f; y += Height + RowGap; }
                rects.Add(new Rect(Inset + x, y, w, Height));
                x += w + Gap;
            }
            return rects;
        }

        public static float MeasureRise(GameNode node, float width, bool mustHandle = false,
            List<GameClock>? clocks = null)
        {
            var rects = Layout(width, BuildItems(node, mustHandle, clocks), Style());
            return rects.Count == 0 ? 0f : rects[rects.Count - 1].yMax + CardGap;
        }

        public static void Draw(Rect card, GameNode node, bool disabled, bool mustHandle = false,
            List<GameClock>? clocks = null)
        {
            var style = Style();
            var items = BuildItems(node, mustHandle, clocks);
            var rects = Layout(card.width, items, style);
            if (rects.Count == 0) return;
            float top = card.y - rects[rects.Count - 1].yMax - CardGap;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var rect = rects[i];
                rect.position += new Vector2(card.x, top);
                rect = UIScale.PixelSnap(rect);
                Color ink = disabled ? IMGUIStyles.TextSecondary : item.Accent ?? (item.Clock != null
                    ? CardDrawer.ClockColor(item.Clock) : item.Attention ? IMGUIStyles.Gold : IMGUIStyles.Paper);
                IMGUIStyles.DrawShadow(rect, new Vector2(2f, 2f), .35f);
                IMGUIStyles.SetColor(IMGUIStyles.CityPlateBg);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                IMGUIStyles.ResetColor();
                IMGUIStyles.DrawOutline(rect, 1f, disabled ? IMGUIStyles.CityPlateLine : item.Accent ?? (item.Attention ? IMGUIStyles.Gold : IMGUIStyles.CityPlateLine));
                if (item.Attention && !disabled)
                    IMGUIStyles.DrawGoldPulse(rect, baseAlpha: .3f, rings: 1, ringStep: 1.5f, speed: 1.5f);
                float iconWidth = item.Clock != null ? Height : 0f;
                if (item.Clock != null)
                {
                    ClockPulse.BeginHost(node.Id);
                    ClockPulse.Note(item.Clock);
                    ActionRiskFocus.DrawClockFocus(rect, item.Clock);
                    CardDrawer.DrawClockGraphic(new Rect(rect.x + 5f, rect.y + 5f,
                        Height - 10f, Height - 10f), item.Clock, ink, showValue: false);
                    IMGUIStyles.DrawOutline(rect, 1f, Color.Lerp(IMGUIStyles.CityPlateLine, ink, ClockPulse.Glow(item.Clock)));
                    ClockPulse.EndHost();
                }
                style.normal.textColor = ink;
                IMGUIStyles.DrawLabel(new Rect(rect.x + Padding + iconWidth, rect.y,
                    rect.width - Padding * 2f - iconWidth, rect.height), item.Text, style);
            }
        }
    }
}
