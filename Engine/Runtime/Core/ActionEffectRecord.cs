#nullable enable

namespace SSNoir.Core
{
    public enum ActionEffectKind
    {
        Item,
        Injury,
        Composure,
        Relation,
        Growth,
        Clock,
        Note
    }

    public enum ActionEffectTone
    {
        Positive,
        Negative,
        Neutral
    }

    public sealed class ActionEffectRecord
    {
        public ActionEffectKind Kind { get; init; }
        public string Label { get; init; } = string.Empty;
        public int? Delta { get; init; }
        public string Text { get; init; } = string.Empty;
        public ActionEffectTone Tone { get; init; } = ActionEffectTone.Neutral;
    }
}
