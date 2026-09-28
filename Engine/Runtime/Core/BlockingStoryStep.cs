#nullable enable
using System;
using System.Collections.Generic;

namespace SSNoir.Core
{
    // 阻塞性"剧情表现"节拍。与"动作表现"(执行进度/掷骰/Outcome)分属两类:
    // 动作表现固定在结局前,不可重排;剧情表现按 Scheme 调用顺序排队、逐个阻塞播放。
    public enum BlockingStoryStepKind
    {
        Video,       // 视频过场：tag 认场景里的 CutsceneSequence（机位 + 视频）。昂贵，少用；
                     // 实时 3D 的场景演出是另一条通道，不走这里
        Motion,      // 场景演出：切到指定机位，播一件道具的实时动画（道具__状态 clip），播完回来。
                     // 普通 clip 停在目标状态；once clip 每次重播并自动回到声明的 from 状态
        Dialogue,    // 阻塞对话:点击推进、锁输入、冻结导航
        Stage,       // 立绘、音效与对白共同编排的舞台演出
        Spotlight,   // 聚光弹窗:点击 dismiss
        EnterPlace,  // 采纳动作后的世界快照，并把导航落到指定地点
        AutoAction,  // 新回合开始时的强制行动；引擎已结算，客户端只播放
    }

    // 一个阻塞剧情步骤。按 Kind 只有对应字段有效。
    public sealed class BlockingStoryStep
    {
        public BlockingStoryStepKind Kind { get; init; }

        public string VideoTag { get; init; } = string.Empty;       // Kind == Video
        public string MotionProp { get; init; } = string.Empty;     // Kind == Motion：地点/道具；道具部分是 clip 名前半
        public string MotionState { get; init; } = string.Empty;    // Kind == Motion：目标状态（clip 名后半）
        public string MotionCamera { get; init; } = string.Empty;   // Kind == Motion：机位名（Camera_<名> 的 <名>），空 = 不换机位
        public DialogueSequence? Dialogue { get; init; }            // Kind == Dialogue
        public StoryStageSequence? Stage { get; init; }              // Kind == Stage
        public SpotlightCard? Spotlight { get; init; }              // Kind == Spotlight
        public string PlaceName { get; init; } = string.Empty;      // Kind == EnterPlace
        public GameNode? AutoActionNode { get; init; }               // Kind == AutoAction
        public DialogueSequence? AutoActionPrelude { get; init; }    // AutoAction：卡出现后、抓骰前的对白
        public List<SlottedResource> AutoActionSlots { get; } = new(); // Kind == AutoAction
        public ActionReport? ResolvedReport { get; init; }           // AutoAction：引擎提交的结果

        public static BlockingStoryStep ForVideo(string tag)
            => new BlockingStoryStep { Kind = BlockingStoryStepKind.Video, VideoTag = tag };

        public static BlockingStoryStep ForMotion(string prop, string state, string camera)
            => string.IsNullOrWhiteSpace(prop) || string.IsNullOrWhiteSpace(state)
                ? throw new System.ArgumentException("motion prop and state cannot be empty")
                : new BlockingStoryStep { Kind = BlockingStoryStepKind.Motion, MotionProp = prop, MotionState = state, MotionCamera = camera ?? string.Empty };

        public static BlockingStoryStep ForDialogue(DialogueSequence sequence)
            => new BlockingStoryStep { Kind = BlockingStoryStepKind.Dialogue, Dialogue = sequence };

        public static BlockingStoryStep ForStage(StoryStageSequence sequence)
            => new BlockingStoryStep { Kind = BlockingStoryStepKind.Stage, Stage = sequence };

        public static BlockingStoryStep ForSpotlight(SpotlightCard card)
            => new BlockingStoryStep { Kind = BlockingStoryStepKind.Spotlight, Spotlight = card };

        public static BlockingStoryStep ForEnterPlace(string placeName)
            => string.IsNullOrWhiteSpace(placeName)
                ? throw new System.ArgumentException("place name cannot be empty", nameof(placeName))
                : new BlockingStoryStep { Kind = BlockingStoryStepKind.EnterPlace, PlaceName = placeName };

        public static BlockingStoryStep ForResolvedAutoAction(
            string name, string title, string text, string? anchorName, DialogueSequence? prelude,
            IReadOnlyList<SlottedResource> slots, ActionReport report)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(text))
                throw new System.ArgumentException("auto action name and text cannot be empty");
            if (slots == null)
                throw new ArgumentNullException(nameof(slots));

            var step = new BlockingStoryStep
            {
                Kind = BlockingStoryStepKind.AutoAction,
                AutoActionPrelude = prelude,
                ResolvedReport = report ?? throw new ArgumentNullException(nameof(report)),
                AutoActionNode = new GameNode
                {
                    Name = name,
                    Title = title,
                    Subtitle = text,
                    AnchorName = anchorName,
                    Resolve = new GameResolve { Type = ResolveType.Instant },
                    Requires = MakeDieRequirements(slots.Count),
                },
            };
            step.AutoActionSlots.AddRange(slots);
            return step;
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
