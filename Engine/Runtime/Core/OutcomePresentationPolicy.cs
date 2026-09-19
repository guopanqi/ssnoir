#nullable enable

namespace SSNoir.Core
{
    // 结算挂在宿主卡下面（结果停留）。只有没有动作名可锚定的演出——比如到达一个地点——
    // 才让判定退到居中弹窗。
    public static class OutcomePresentationPolicy
    {
        public static bool ShouldUseRollModal(ActionReport report, string anchorNodeName)
        {
            return report.Type == ActionType.Roll && string.IsNullOrEmpty(anchorNodeName);
        }
    }
}
