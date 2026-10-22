#nullable enable
namespace SSNoir.Core
{
    // Outcome probabilities are independent of risk. A risk is an authored promise
    // about possible consequences, not a second effect executor.
    public enum ActionRiskLevel { Safe, Risky, Dangerous }
    public enum ActionTargetKind { Clock, Composure, Injury, Item }

    public sealed class ActionTarget
    {
        public ActionTargetKind Kind { get; init; }
        public string Id { get; init; } = string.Empty;
        public string DisplayName => Kind switch
        {
            ActionTargetKind.Composure => "冷静",
            ActionTargetKind.Injury => "伤势",
            _ => Id,
        };
    }

    public sealed class ActionRisk
    {
        public ActionRiskLevel Level { get; init; }
        public ActionTarget? RiskTarget { get; init; }
        public ActionTarget? ProgressTarget { get; init; }
        public string Label => Level switch
        {
            ActionRiskLevel.Safe => "稳妥",
            ActionRiskLevel.Risky => "冒险",
            _ => "危险"
        };
    }
}
