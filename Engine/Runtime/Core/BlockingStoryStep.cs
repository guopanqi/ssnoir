#nullable enable
using System;
using System.Collections.Generic;

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
        AutoAction,  // 非玩家发起的完整动作：占用骰子、执行、结算效果
    }

    // 一个阻塞剧情步骤。按 Kind 只有对应字段有效。
    public sealed class BlockingStoryStep
    {
        public BlockingStoryStepKind Kind { get; init; }

        public string AnimationTag { get; init; } = string.Empty;   // Kind == Animation
        public DialogueSequence? Dialogue { get; init; }            // Kind == Dialogue
        public SpotlightCard? Spotlight { get; init; }              // Kind == Spotlight
        public string PlaceName { get; init; } = string.Empty;      // Kind == EnterPlace
        public GameNode? AutoActionNode { get; init; }               // Kind == AutoAction
        public List<SlottedResource> AutoActionSlots { get; } = new(); // Kind == AutoAction
        public Action? AutoActionEffect { get; init; }               // Kind == AutoAction
        private bool _autoActionResolved;

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

        public static BlockingStoryStep ForAutoAction(
            string name, string text, string? anchorName, int slotCount, Action effect)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(text))
                throw new System.ArgumentException("auto action name and text cannot be empty");
            if (slotCount <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(slotCount));

            return new BlockingStoryStep
            {
                Kind = BlockingStoryStepKind.AutoAction,
                AutoActionEffect = effect ?? throw new ArgumentNullException(nameof(effect)),
                AutoActionNode = new GameNode
                {
                    Name = name,
                    Subtitle = text,
                    AnchorName = anchorName,
                    Resolve = new GameResolve { Type = ResolveType.Instant },
                    Requires = MakeDieRequirements(slotCount),
                },
            };
        }

        public void ResolveAutoAction()
        {
            if (Kind != BlockingStoryStepKind.AutoAction || AutoActionEffect == null)
                throw new InvalidOperationException("Only auto-action steps can be resolved this way.");
            if (_autoActionResolved)
                throw new InvalidOperationException("Auto action has already been resolved.");
            _autoActionResolved = true;
            AutoActionEffect();
        }

        /// <summary>
        /// 自动行动允许因人物离场或当手骰不足而少拿骰；表现层只画实际取得的骰槽。
        /// </summary>
        public void MatchAutoActionRequirementsToSlots()
        {
            if (Kind != BlockingStoryStepKind.AutoAction || AutoActionNode == null)
                throw new System.InvalidOperationException("Only auto-action steps have die requirements.");
            AutoActionNode.Requires = MakeDieRequirements(AutoActionSlots.Count);
        }

        private static List<ActionCost> MakeDieRequirements(int count)
        {
            if (count < 0)
                throw new System.ArgumentOutOfRangeException(nameof(count));
            var result = new List<ActionCost>();
            for (int i = 0; i < count; i++)
                result.Add(new ActionCost { Type = "die" });
            return result;
        }
    }
}
