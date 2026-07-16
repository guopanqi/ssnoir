#nullable enable
using System;
using System.Collections.Generic;

namespace SSNoir.Core
{
    public enum ResolveType
    {
        Instant,
        Roll,
        Observe,
        Clock
    }

    public class GameResolve
    {
        public ResolveType Type { get; set; }

        // Clock
        public GameClock? Clock { get; set; }

        // Instant
        public ActionOutcome? Outcome { get; set; }

        // Roll
        public string SkillName { get; set; } = string.Empty;
        public List<DifficultyModifierInfo> DifficultyModifiers { get; set; } = new List<DifficultyModifierInfo>();
        public ActionOutcome? FailOutcome { get; set; }
        public ActionOutcome? NeutralOutcome { get; set; }
        public ActionOutcome? SuccessOutcome { get; set; }

        // Observe
        public string ObserveText { get; set; } = string.Empty;
    }
}
