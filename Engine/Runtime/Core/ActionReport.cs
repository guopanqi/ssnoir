#nullable enable
using System.Collections.Generic;

namespace SSNoir.Core
{
    public enum ActionType
    {
        Roll,
        Instant
    }

    public enum RollOutcome
    {
        Success,
        Fail,
        Neutral
    }

    public class ActionReport
    {
        public ActionType Type { get; set; }
        public int FinalRollValue { get; set; }
        public int ModifiedRollValue { get; set; }
        public RollOutcome Outcome { get; set; }
        public string AnimationTag { get; set; } = string.Empty;
        public IReadOnlyList<PresentationHint> PresentationHints { get; set; } = new List<PresentationHint>();

        // Difficulty modifiers applied to this roll (e.g., "监控在线", -1)
        public List<DifficultyModifierInfo> DifficultyModifiers { get; set; } = new List<DifficultyModifierInfo>();

        // Diagnostic / rendering metadata for the rolling details
        public int ChosenDieValue { get; set; } = 1;
        public List<int> RandomDice { get; set; } = new List<int>();
    }
}
