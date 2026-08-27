#nullable enable

namespace SSNoir.Core
{
    // 阻塞性"剧情表现"节拍。与"动作表现"(执行进度/掷骰/Outcome)分属两类:
    // 动作表现固定在结局前,不可重排;剧情表现按 Scheme 调用顺序排队、逐个阻塞播放。
    public enum BlockingStoryStepKind
    {
        Animation,   // 命名动画(v1 只携带 Tag,前端占位播放;将来接 Timeline)
        Dialogue,    // 阻塞对话:点击推进、锁输入、冻结导航
        Spotlight,   // 聚光弹窗:点击 dismiss
        EnterPlace,  // 采纳动作后的世界快照，并把导航落到指定地点
    }

    // 一个阻塞剧情步骤。按 Kind 只有对应字段有效。
    public sealed class BlockingStoryStep
    {
        public BlockingStoryStepKind Kind { get; init; }

        public string AnimationTag { get; init; } = string.Empty;   // Kind == Animation
        public DialogueSequence? Dialogue { get; init; }            // Kind == Dialogue
        public SpotlightCard? Spotlight { get; init; }              // Kind == Spotlight
        public string PlaceName { get; init; } = string.Empty;      // Kind == EnterPlace

        public static BlockingStoryStep ForAnimation(string tag)
            => new BlockingStoryStep { Kind = BlockingStoryStepKind.Animation, AnimationTag = tag };

        public static BlockingStoryStep ForDialogue(DialogueSequence sequence)
            => new BlockingStoryStep { Kind = BlockingStoryStepKind.Dialogue, Dialogue = sequence };

        public static BlockingStoryStep ForSpotlight(SpotlightCard card)
            => new BlockingStoryStep { Kind = BlockingStoryStepKind.Spotlight, Spotlight = card };

        public static BlockingStoryStep ForEnterPlace(string placeName)
            => string.IsNullOrWhiteSpace(placeName)
                ? throw new System.ArgumentException("place name cannot be empty", nameof(placeName))
                : new BlockingStoryStep { Kind = BlockingStoryStepKind.EnterPlace, PlaceName = placeName };
    }
}
