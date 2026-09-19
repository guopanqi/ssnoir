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
        public List<ActionEffectRecord> Effects { get; } = new List<ActionEffectRecord>();

        /// <summary>
        /// 这一手里时间真的翻了一页——交锋里按下休息，或者在家里睡了一觉。
        /// 由 <c>SceneManager.EndTurn</c> 打上；脚本调 <c>end-turn!</c> 时那次 EndTurn
        /// 写的是同一份报告，所以「睡觉」这类把回合结束包在动作里的节点也带着这个标记。
        /// 客户端据此在结算落地的瞬间放一次黑场，让"下一回合"有个明确的切换。
        /// </summary>
        public bool TurnEnded { get; set; }

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

        /// <summary>引擎自动行说不出的补充（"解锁：码头账房"这类）。内容侧的 result-supplement! 落到这里。</summary>
        public void AddSupplement(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new System.ArgumentException("result note cannot be empty");
            Effects.Add(new ActionEffectRecord
            {
                Kind = ActionEffectKind.Supplement,
                Text = text,
                Tone = ActionEffectTone.Neutral
            });
        }

        public void AddClockEffect(string label, int delta)
        {
            if (string.IsNullOrWhiteSpace(label))
                throw new System.ArgumentException("clock effect label cannot be empty");
            if (delta == 0)
                return;

            int existingIndex = Effects.FindIndex(effect =>
                effect.Kind == ActionEffectKind.Clock && effect.Text == label);
            int totalDelta = delta;
            if (existingIndex >= 0)
            {
                totalDelta += Effects[existingIndex].Delta ?? 0;
                Effects.RemoveAt(existingIndex);
            }

            Effects.Add(new ActionEffectRecord
            {
                Kind = ActionEffectKind.Clock,
                Label = label,
                Delta = totalDelta,
                Text = label,
                Tone = totalDelta > 0 ? ActionEffectTone.Positive : ActionEffectTone.Negative
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
        public int SkillLevel { get; set; }
        public int ModifierTotal { get; set; }
        public int PreparedValue { get; set; }
        public int FateDieValue { get; set; } = 1;
    }
}
