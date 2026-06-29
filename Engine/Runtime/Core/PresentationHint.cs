#nullable enable

namespace SSNoir.Core
{
    public enum PresentationHintKind
    {
        ExecuteProgress,
        RollDice,
        ShowNotification
    }

    public sealed class PresentationHint
    {
        public PresentationHintKind Kind { get; init; }
        public string Text { get; init; } = string.Empty;
        public string Tag { get; init; } = string.Empty;
        public float DurationSeconds { get; init; } = 0.3f;
    }
}
