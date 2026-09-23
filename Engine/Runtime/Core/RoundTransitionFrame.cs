#nullable enable

namespace SSNoir.Core
{
    public enum RoundTransitionPhase
    {
        TimeTax,               // 结束回合本身的代价：这一手熬过去扣的冷静。和按键同一拍
        OpponentRules,
        RoundEndMaintenance,
        NewDice,
        ForcedAction,
        Finished,
    }

    /// <summary>
    /// 交锋回合转换中一次已经提交的原子结果。Snapshot 是这一批完成后的真实盘面；
    /// 客户端在旧盘面上播放 Report，结束后再采纳 Snapshot。
    /// </summary>
    public sealed class RoundTransitionFrame
    {
        public RoundTransitionPhase Phase { get; init; }
        public ActionReport Report { get; init; } = new ActionReport { Type = ActionType.Instant };
        public PresentationSnapshot Snapshot { get; init; } = new PresentationSnapshot();
        public bool IsFinished => Phase == RoundTransitionPhase.Finished;
    }
}
