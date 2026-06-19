#nullable enable
using System;

namespace SSNoir.Core
{
    public enum OutcomePresentationMode
    {
        Light,
        Heavy
    }

    public sealed class OutcomePresentation
    {
        public string Title { get; init; } = string.Empty;
        public string Subtitle { get; init; } = string.Empty;
        public OutcomePresentationMode Mode { get; init; } = OutcomePresentationMode.Light;

        public bool HasText =>
            !string.IsNullOrWhiteSpace(Title) || !string.IsNullOrWhiteSpace(Subtitle);
    }

    public sealed class ActionOutcome
    {
        public OutcomePresentation Presentation { get; init; } = new OutcomePresentation();
        public Action? Effect { get; init; }

        public bool HasText => Presentation.HasText;
    }
}
