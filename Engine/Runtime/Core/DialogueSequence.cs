#nullable enable
using System;
using System.Collections.Generic;

namespace SSNoir.Core
{
    // 一组有序台词。Banter 和 Dialogue 共用此结构,区别只在播放器的生命周期。
    public sealed class DialogueSequence
    {
        public IReadOnlyList<DialogueLine> Lines { get; }
        // 仅显式的 remote banter 会开启。未解析的说话人由临时侧边卡承接，
        // 普通 banter 仍严格要求锚定，避免拼写或节点配置错误被悄悄吞掉。
        public bool AllowsRemoteParticipants { get; }

        public DialogueSequence(IReadOnlyList<DialogueLine> lines, bool allowsRemoteParticipants = false)
        {
            if (lines == null || lines.Count == 0)
                throw new ArgumentException("dialogue sequence cannot be empty");
            Lines = lines;
            AllowsRemoteParticipants = allowsRemoteParticipants;
        }
    }
}
