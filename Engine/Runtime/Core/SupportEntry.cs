#nullable enable
namespace SSNoir.Core
{
    /// <summary>
    /// 成长面板上的一条关系支援：谁给的、叫什么、干什么用、这一场带不带。
    /// 文案由内容登记（engine.scm 的 support-catalog），引擎只按 Team.Supports 逐条去问。
    /// </summary>
    public sealed class SupportEntry
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public bool Carried { get; init; }
    }
}
