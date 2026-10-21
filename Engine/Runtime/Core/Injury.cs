#nullable enable
using System;

namespace SSNoir.Core
{
    public enum InjuryBand { None, Light, Severe }

    /// <summary>
    /// 伤势：到 2 点封锁一个行动位，到 5 点再封锁一个；7 点倒下送医。
    /// 伤势不再扣技能，刻度仍用于治疗与恢复；不会随时间自行恶化。
    /// </summary>
    public sealed class Injury
    {
        public const int LightThreshold = 1;
        public const int SevereThreshold = 4;
        public const int FirstActionSlotLossThreshold = 2;
        public const int SecondActionSlotLossThreshold = 5;
        /// <summary>刻度上限，也是触发倒下结算的阈值。</summary>
        public const int MaxSeverity = 7;
        /// <summary>倒下结算后的伤势：送医后伤势归零，永久代价由疤痕承担。</summary>
        public const int PostCollapseSeverity = 0;

        /// <summary>一般坏结果的伤害量。</summary>
        public const int OrdinaryHarm = 1;
        /// <summary>明确重创（枪伤、坠落、被围住打）的伤害量：一下就把完好的人顶到轻伤段顶上（3/3）。</summary>
        public const int SevereHarm = 3;

        // 部位与能力一一对应，玩家读到的是「手伤 · 力量 −1」，不需要记映射表。
        private static readonly (string Part, string Skill, string SkillName)[] Sites =
        {
            ("手", "violence",  "力量"),
            ("头", "knowledge", "见识"),
            ("眼", "sharpness", "敏锐"),
            ("脸", "social",    "交际"),
        };

        /// <summary>按部位名查回它对应的能力。疤痕（<see cref="ScarSet"/>）与读档都用它，
        /// 保证部位与能力的映射只有这一份。</summary>
        public static (string Part, string Skill, string SkillName) SiteOf(string part)
        {
            foreach (var site in Sites)
                if (site.Part == part) return site;
            throw new ArgumentException($"Unknown injury site '{part}'.", nameof(part));
        }

        public int Severity { get; private set; }
        public string Part { get; private set; } = string.Empty;
        public string Skill { get; private set; } = string.Empty;
        public string SkillName { get; private set; } = string.Empty;

        public InjuryBand Band =>
            Severity >= SevereThreshold ? InjuryBand.Severe :
            Severity >= LightThreshold  ? InjuryBand.Light  : InjuryBand.None;

        public static string BandName(InjuryBand band) => band switch
        {
            InjuryBand.None   => "完好",
            InjuryBand.Light  => "轻伤",
            InjuryBand.Severe => "重伤",
            _ => throw new ArgumentOutOfRangeException(nameof(band), band, "Unknown injury band.")
        };

        /// <summary>伤势到 2 点封锁一个行动位，到 5 点封锁第二个；7 点会立刻进入倒下结算。</summary>
        public int ActionDieLoss => Severity >= SecondActionSlotLossThreshold ? 2
            : Severity >= FirstActionSlotLossThreshold ? 1 : 0;
        public int SkillPenalty => 0;
        public bool CostsActionDie => ActionDieLoss > 0;
        public DifficultyModifierInfo? ModifierFor(string actorRole, string skillName) => null;

        public string Describe()
            => Band == InjuryBand.None ? string.Empty
                : ActionDieLoss == 0
                    ? $"{Part}伤 · {BandName(Band)}"
                    : $"{Part}伤 · {BandName(Band)} · 封锁{ActionDieLoss}个行动位";

        /// <summary>
        /// 受伤。身上没伤时随机落一处，已有伤则加深同一处。
        /// 返回 true 表示这一下把人打倒了，由 GameState 结算送医。
        /// </summary>
        public bool Aggravate(int amount)
        {
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Injury amount must be positive.");
            if (Severity == 0)
            {
                var site = Sites[GameRandom.Instance.Next(0, Sites.Length)];
                Part = site.Part;
                Skill = site.Skill;
                SkillName = site.SkillName;
            }
            // 到达满格就倒下。不能把 7/7 画成终点、规则却要求玩家再挨一下。
            bool collapses = Severity + amount >= MaxSeverity;
            Severity = Math.Min(MaxSeverity, Severity + amount);
            return collapses;
        }

        /// <summary>治疗。降到 0 即痊愈，部位随之清除。</summary>
        public void Heal(int amount)
        {
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Heal amount must be positive.");
            Severity = Math.Max(0, Severity - amount);
            if (Severity == 0) Reset();
        }

        public void ResolveCollapse() => Severity = PostCollapseSeverity;

        public void Reset()
        {
            Severity = 0;
            Part = string.Empty;
            Skill = string.Empty;
            SkillName = string.Empty;
        }

        /// <summary>读档：部位名是权威，能力由部位表推回，避免存档里出现两份互相矛盾的字段。</summary>
        public void Restore(int severity, string part)
        {
            if (severity < 0 || severity > MaxSeverity)
                throw new ArgumentOutOfRangeException(nameof(severity), severity,
                    $"Injury severity must be between 0 and {MaxSeverity}.");
            if (severity == 0)
            {
                Reset();
                return;
            }
            foreach (var site in Sites)
            {
                if (site.Part != part) continue;
                Severity = severity;
                Part = site.Part;
                Skill = site.Skill;
                SkillName = site.SkillName;
                return;
            }
            throw new ArgumentException($"Save file references unknown injury site '{part}'.", nameof(part));
        }
    }
}
