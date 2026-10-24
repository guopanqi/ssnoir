#nullable enable
using System;
using System.Collections.Generic;

namespace SSNoir.Core
{
    /// <summary>
    /// Verify that risk previews reference existing and unambiguous clocks.
    /// Multiple renderings of the same clock are legal when all observable values agree.
    /// This NEVER applies an effect: authored Scheme outcomes remain authoritative.
    /// </summary>
    public static class ActionRiskContract
    {
        public static void Validate(GameNode root, IReadOnlyList<GameNode> carried)
        {
            var clocks = new Dictionary<string, GameClock>(StringComparer.Ordinal);
            var ambiguous = new HashSet<string>(StringComparer.Ordinal);
            Collect(root, clocks, ambiguous);
            Check(root, clocks, ambiguous);
            foreach (var carriedNode in carried) Check(carriedNode, clocks, ambiguous);
        }
        private static void Collect(GameNode node, Dictionary<string, GameClock> clocks, HashSet<string> ambiguous)
        {
            foreach (var clock in node.Clocks) Add(clock, clocks, ambiguous);
            if (node.Resolve?.Clock != null) Add(node.Resolve.Clock, clocks, ambiguous);
            foreach (var child in node.Children) Collect(child, clocks, ambiguous);
        }
        private static void Add(GameClock clock, Dictionary<string, GameClock> clocks, HashSet<string> ambiguous)
        {
            if (!clocks.TryGetValue(clock.Label, out var other)) clocks.Add(clock.Label, clock);
            else if (other.Current != clock.Current || other.Max != clock.Max ||
                     other.Style != clock.Style || other.BeneficialDirection != clock.BeneficialDirection)
                ambiguous.Add(clock.Label);
        }
        private static void Check(GameNode node, Dictionary<string, GameClock> clocks, HashSet<string> ambiguous)
        {
            CheckTarget(node, node.RiskTarget, clocks, ambiguous);
            CheckTarget(node, node.ProgressTarget, clocks, ambiguous);
            foreach (var child in node.Children) Check(child, clocks, ambiguous);
        }
        private static void CheckTarget(GameNode node, ActionTarget? target,
            Dictionary<string, GameClock> clocks, HashSet<string> ambiguous)
        {
            if (target?.Kind != ActionTargetKind.Clock) return;
            if (!clocks.ContainsKey(target.Id))
                throw new InvalidOperationException($"Action '{node.Id}' references missing clock '{target.Id}'.");
            if (ambiguous.Contains(target.Id))
                throw new InvalidOperationException($"Action '{node.Id}' references ambiguous clock '{target.Id}'.");
        }
    }
}
