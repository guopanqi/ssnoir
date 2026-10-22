#nullable enable
using System;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    /// <summary>UI-only temporary target focus. Never changes gameplay or save data.</summary>
    internal static class ActionRiskFocus
    {
        private static ActionTarget? _focus;
        private static ActionTarget? _next;

        internal static void BeginFrame(ActionTarget? selected)
        {
            _focus = _next ?? selected;
            _next = selected;
        }
        internal static void Offer(GameNode node)
        {
            if (node.Risk != null)
                _next = node.Risk.RiskTarget ?? node.Risk.ProgressTarget;
        }
        internal static bool IsClockFocused(GameClock clock)
            => _focus?.Kind == ActionTargetKind.Clock
               && string.Equals(clock.Label, _focus.Id, StringComparison.Ordinal);
        internal static bool IsComposureFocused()
            => _focus?.Kind == ActionTargetKind.Composure;
        internal static bool IsInjuryFocused()
            => _focus?.Kind == ActionTargetKind.Injury;
    }
}
