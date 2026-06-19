#nullable enable

namespace SSNoir.Core
{
    public static class OutcomePresentationPolicy
    {
        public static bool ShouldUseOutcomeModal(ActionReport report, string anchorNodeName)
        {
            return report.Type == ActionType.Instant
                && report.OutcomePresentation?.HasText == true
                && (report.OutcomePresentation.Mode == OutcomePresentationMode.Heavy
                    || string.IsNullOrEmpty(anchorNodeName));
        }

        public static bool ShouldUseRollModal(ActionReport report, string anchorNodeName)
        {
            return report.Type == ActionType.Roll
                && (string.IsNullOrEmpty(anchorNodeName)
                    || report.OutcomePresentation?.Mode == OutcomePresentationMode.Heavy);
        }
    }
}
