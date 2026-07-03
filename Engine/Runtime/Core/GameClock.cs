#nullable enable
using System;

namespace SSNoir.Core
{
    public enum ClockStyle
    {
        Segments,
        Countdown,
        Pie
    }

    public class GameClock
    {
        public string Label { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public int Current { get; set; }
        public int Max { get; set; }
        public ClockStyle Style { get; set; } = ClockStyle.Segments;
    }
}
