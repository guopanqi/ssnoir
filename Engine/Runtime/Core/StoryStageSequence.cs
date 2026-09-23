#nullable enable
using System;
using System.Collections.Generic;

namespace SSNoir.Core
{
    public enum StoryStageCommandKind { Spawn, Move, Remove, Pose, Light, Sound, Say, Pause }

    public sealed class StoryStageCommand
    {
        public StoryStageCommandKind Kind { get; init; }
        public string Id { get; init; } = string.Empty;
        public string Asset { get; init; } = string.Empty;
        public string Layer { get; init; } = "middle";
        public float X { get; init; }
        public float Seconds { get; init; }
        public DialogueLine? Line { get; init; }
    }

    // A beat starts all its commands together. It ends when its longest timed command ends;
    // a Say beat waits for player input. Content validation prevents concurrent writes to one actor.
    public sealed class StoryStageBeat
    {
        public IReadOnlyList<StoryStageCommand> Commands { get; }
        public StoryStageBeat(IReadOnlyList<StoryStageCommand> commands)
        {
            if (commands == null || commands.Count == 0)
                throw new ArgumentException("stage beat cannot be empty");
            Commands = commands;
        }
    }

    public sealed class StoryStageSequence
    {
        public IReadOnlyList<StoryStageBeat> Beats { get; }
        public StoryStageSequence(IReadOnlyList<StoryStageBeat> beats)
        {
            if (beats == null || beats.Count == 0)
                throw new ArgumentException("stage sequence cannot be empty");
            Beats = beats;
        }
    }
}
