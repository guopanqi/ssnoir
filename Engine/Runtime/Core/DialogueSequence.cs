#nullable enable
using System;
using System.Collections.Generic;

namespace SSNoir.Core
{
    // 一组有序台词。Banter 和 Dialogue 共用此结构,区别只在播放器的生命周期。
    public sealed class DialogueSequence
    {
        public IReadOnlyList<DialogueLine> Lines { get; }
        // 显式 remote dialogue/banter 会开启。普通调用也允许前端把未解析的说话人
        // 降级为临时侧边卡，但必须同时给出内容警告，不能静默吞掉拼写或场景配置错误。
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
