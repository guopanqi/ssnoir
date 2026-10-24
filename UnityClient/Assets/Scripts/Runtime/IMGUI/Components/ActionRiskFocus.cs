#nullable enable
using System;
using SSNoir.Core;
using UnityEngine;

namespace SSNoir.IMGUI
{
    /// <summary>UI-only temporary clock focus. Never changes gameplay or save data.</summary>
    internal static class ActionRiskFocus
    {
        private static GameNode? _focus;
        private static GameNode? _next;

        internal static void BeginFrame(GameNode? selected)
        {
            _focus = _next ?? selected;
            _next = selected;
        }
        internal static void Offer(GameNode node) => _next = node;
        internal static bool IsClockFocused(GameClock clock)
            => Matches(_focus?.RiskTarget, clock) || Matches(_focus?.ProgressTarget, clock);

        private static bool Matches(ActionTarget? target, GameClock clock)
            => target?.Kind == ActionTargetKind.Clock
               && string.Equals(clock.Label, target.Id, StringComparison.Ordinal);

        internal static void DrawClockFocus(Rect rect, GameClock clock)
        {
            if (!IsClockFocused(clock)) return;
            Color color = CardDrawer.ClockColor(clock);
            float breath = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
            // 独立于数值变化脉冲：持续框出受影响的钟，不预演增减格数。
            var outer = new Rect(rect.x - 5f, rect.y - 5f, rect.width + 10f, rect.height + 10f);
            IMGUIStyles.DrawOutline(outer, 2f, Color.Lerp(color, Color.white, .35f));
            outer = new Rect(outer.x - 3f, outer.y - 3f, outer.width + 6f, outer.height + 6f);
            color.a = .18f + .22f * breath;
            IMGUIStyles.DrawOutline(outer, 3f, color);
        }
    }
}
