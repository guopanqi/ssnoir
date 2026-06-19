#nullable enable
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    public sealed class CardPresentationResidue
    {
        public string AnchorNodeName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public RollOutcome? RollOutcome { get; set; }
    }
}
