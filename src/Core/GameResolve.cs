using System;

namespace SSNoir.Core
{
    public enum ResolveType
    {
        Instant,
        Roll,
        Observe
    }

    public class GameResolve
    {
        public ResolveType Type { get; set; }

        // Instant / Roll
        public Action? Effect { get; set; }

        // Roll
        public string SkillName { get; set; } = string.Empty;
        public Action? OnFail { get; set; }
        public Action? OnNeutral { get; set; }
        public Action? OnSuccess { get; set; }

        // Observe
        public string ObserveText { get; set; } = string.Empty;
    }
}
