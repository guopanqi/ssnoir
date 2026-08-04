#nullable enable

namespace SSNoir.Core
{
    // 失败是游戏状态，不是某个客户端临时推断出的 UI 分支。
    public sealed class GameFailure
    {
        public static readonly GameFailure None = new(false, string.Empty, string.Empty);

        public bool IsFailed { get; }
        public string Title { get; }
        public string Description { get; }

        public GameFailure(bool isFailed, string title, string description)
        {
            IsFailed = isFailed;
            Title = title;
            Description = description;
        }
    }
}
