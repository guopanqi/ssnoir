#nullable enable
using System;
using System.Collections.Generic;

namespace SSNoir.Core
{
    // 一组有序台词。Banter 和 Dialogue 共用此结构,区别只在播放器的生命周期。
    public sealed class DialogueSequence
    {
        public IReadOnlyList<DialogueLine> Lines { get; }

        public DialogueSequence(IReadOnlyList<DialogueLine> lines)
        {
            if (lines == null || lines.Count == 0)
                throw new ArgumentException("dialogue sequence cannot be empty");
            Lines = lines;
        }
    }
}
