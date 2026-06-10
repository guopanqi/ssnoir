#nullable enable
using System;
using System.Collections.Generic;

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
        // Dynamic difficulty modifiers: invoked at execution time to get current modifiers
        public Func<List<DifficultyModifierInfo>>? GetDifficultyModifiers { get; set; }
        public Action? OnFail { get; set; }
        public Action? OnNeutral { get; set; }
        public Action? OnSuccess { get; set; }

        // Observe
        public string ObserveText { get; set; } = string.Empty;
    }
}
