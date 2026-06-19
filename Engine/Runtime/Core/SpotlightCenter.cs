#nullable enable

namespace SSNoir.Core
{
    public sealed class SpotlightCard
    {
        public string Title { get; init; } = string.Empty;
        public string Subtitle { get; init; } = string.Empty;
    }

    public sealed class SpotlightCenter
    {
        public SpotlightCard? Current { get; private set; }

        public bool HasSpotlight => Current != null;

        public void Show(string title, string subtitle)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new System.ArgumentException("spotlight title cannot be empty");
            }

            Current = new SpotlightCard
            {
                Title = title,
                Subtitle = subtitle
            };
        }

        public void Dismiss()
        {
            Current = null;
        }
    }
}
