#nullable enable
using System.Collections.Generic;

namespace SSNoir.Core
{
    public enum ActionType
    {
        Roll,
        Instant
    }

    public enum RollOutcome
    {
        Success,
        Fail,
        Neutral
    }

    public class ActionReport
    {
        public ActionType Type { get; set; }
        public RollOutcome Outcome { get; set; }
        public IReadOnlyList<PresentationHint> PresentationHints { get; set; } = new List<PresentationHint>();
        public OutcomePresentation? OutcomePresentation { get; set; }
        public List<ActionEffectRecord> Effects { get; } = new List<ActionEffectRecord>();

        public void AddEffect(ActionEffectKind kind, string label, int delta, ActionEffectTone tone)
        {
            if (delta == 0)
                return;
            Effects.Add(new ActionEffectRecord
            {
                Kind = kind,
                Label = label,
                Delta = delta,
                Tone = tone
            });
        }

        public void AddNote(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new System.ArgumentException("result note cannot be empty");
            Effects.Add(new ActionEffectRecord
            {
                Kind = ActionEffectKind.Note,
                Text = text,
                Tone = ActionEffectTone.Neutral
            });
        }

        // 阻塞剧情节拍(Spotlight / Dialogue / Animation),按 Scheme 调用顺序;adopt 之前逐个播放。
        public List<BlockingStoryStep> BlockingStorySteps { get; } = new List<BlockingStoryStep>();

        // 非阻塞插话(banter),adopt 之后随旁白一起释放,锚定到动作后的新快照。
        public List<DialogueSequence> Banter { get; } = new List<DialogueSequence>();

        public List<string> NarrationIds { get; } = new List<string>();

        // Difficulty modifiers applied to this roll (e.g., "监控在线", -1)
        public List<DifficultyModifierInfo> DifficultyModifiers { get; set; } = new List<DifficultyModifierInfo>();

        // Diagnostic / rendering metadata for the rolling details
        public int ChosenDieValue { get; set; } = 1;
        public int PreparedValue { get; set; }
        public int FateDieValue { get; set; } = 1;
        public int NaturalModifier { get; set; }
        public int FinalTotal { get; set; }
    }
}
