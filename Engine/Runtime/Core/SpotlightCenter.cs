#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace SSNoir.Core
{
    public sealed class SpotlightParagraph
    {
        public string Text { get; init; } = string.Empty;
        public string Image { get; init; } = string.Empty;
    }

    // 所有通知与插画事件共用一张纸；无图时就是标题与正文。
    public sealed class SpotlightCard
    {
        public string Title { get; init; } = string.Empty;
        public IReadOnlyList<SpotlightParagraph> Paragraphs { get; init; } = Array.Empty<SpotlightParagraph>();
        public IReadOnlyList<ActionEffectRecord> Effects { get; init; } = Array.Empty<ActionEffectRecord>();
        public string Body => string.Join("\n", Paragraphs.Select(p => p.Text));
        public static SpotlightCard Notice(string title, string body)
        {
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("spotlight title cannot be empty");
            return new SpotlightCard { Title = title, Paragraphs = string.IsNullOrWhiteSpace(body)
                ? Array.Empty<SpotlightParagraph>() : new[] { new SpotlightParagraph { Text = body } } };
        }
    }

    public sealed class SpotlightCenter
    {
        public SpotlightCard? Current { get; private set; }
        public bool HasSpotlight => Current != null;
        public void Show(SpotlightCard card) => Current = card ?? throw new ArgumentNullException(nameof(card));
        public void Dismiss() => Current = null;
    }
}
