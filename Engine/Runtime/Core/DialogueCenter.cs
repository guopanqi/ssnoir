#nullable enable
using System;

namespace SSNoir.Core
{
    // 纯广播器:不持有任何播放/计时状态(那是前端 BanterPlayer/ConversationPlayer 的事)。
    // 只用于"动作外"的即时触发;"动作内"触发走 ActionReport.BlockingStorySteps / Banter。
    public sealed class DialogueCenter
    {
        public event Action<DialogueSequence>? OnBanterRequested;
        public event Action<DialogueSequence>? OnDialogueRequested;
        public event Action<StoryStageSequence>? OnStageRequested;
        public event Action<SSNoir.Theatre.TheatreScene>? OnTheatreRequested;

        public void RequestBanter(DialogueSequence sequence)
        {
            if (sequence == null) throw new ArgumentException("banter sequence cannot be null");
            OnBanterRequested?.Invoke(sequence);
        }

        public void RequestDialogue(DialogueSequence sequence)
        {
            if (sequence == null) throw new ArgumentException("dialogue sequence cannot be null");
            OnDialogueRequested?.Invoke(sequence);
        }

        public void RequestTheatre(SSNoir.Theatre.TheatreScene scene)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            OnTheatreRequested?.Invoke(scene);
        }

        public void RequestStage(StoryStageSequence sequence)
        {
            if (sequence == null) throw new ArgumentException("stage sequence cannot be null");
            OnStageRequested?.Invoke(sequence);
        }
    }
}
