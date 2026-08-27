#nullable enable
using System;

namespace SSNoir.Core
{
    public enum InjuryBand { None, Light, Severe }

    /// <summary>
    /// 伤势：全队唯一的身体轴，取代旧的健康血条。规则以本文件为唯一来源。
    ///
    /// 一次只有一处伤。第一次受伤随机命中一项能力并定下部位名，之后的伤害都加在同一条上，
    /// 归 0 才清除——所以第二次受伤必然是加重，压力不会被分散到多个部位上。
    /// 刻度本身就是康复进度，反着读：治疗即降伤势，降到 0 就是痊愈。
    ///
    ///   0     完好
    ///   1–3   轻伤   命中的能力 −1
    ///   4–6   重伤   不再扣能力，封掉一颗行动骰
    ///   7     倒下   强制送医，结算后伤势归零
    ///
    /// **伤势不会自己好。**这是它和冷静的全部区别：冷静是睡一觉就回一点的那条，
    /// 伤势是你得为它腾出点什么的那条——药品花钱，诊所同时花钱和骰，
    /// 弹簧床垫则解锁用行动骰换康复的长期通道。睡觉本身不治伤，轻伤与重伤都不会自动好。
    ///
    /// 冷静 5 点垫子 + 伤势 7 点身体 = 12 点。交锋每回合 1 点，前五个回合不流血，
    /// 第六回合进轻伤，第九回合进重伤，第十二回合倒下。一场交锋不该长成那样：真正结束它的应该是交锋自己的时钟，
    /// 这条轴只是兜底的账单。
    ///
    /// 不治疗不会自行恶化（伤势钳在当前值）。到达满格立刻倒下，结算后伤势归零，
    /// 但会留下永久疤痕，所以谷底仍然爬得起来，不会被拖进死亡螺旋。
    /// </summary>
    public sealed class Injury
    {
        public const int LightThreshold = 1;
        public const int SevereThreshold = 4;
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

        /// <summary>命中能力的判定修正：只有轻伤 −1；重伤改为封行动骰，不叠加能力扣减。</summary>
        public int SkillPenalty => Band switch
        {
            InjuryBand.Light  => -1,
            _ => 0,
        };

        /// <summary>重伤少一颗行动骰。</summary>
        public bool CostsActionDie => Band == InjuryBand.Severe;

        /// <summary>
        /// 这次判定要不要吃伤势修正；不吃返回 null。伤势只压主角被打中的那一项能力，
        /// 并且走和「势力敌视 −1」「非法 −2」同一条**可见**修正——玩家投骰前就该看见它。
        /// 卡面预览和实际结算都从这里取，否则会出现预览写 4、结算按 2 算的两套账。
        /// </summary>
        public DifficultyModifierInfo? ModifierFor(string actorRole, string skillName)
        {
            if (actorRole != "protagonist" || SkillPenalty == 0) return null;
            if (!Skill.Equals(skillName, StringComparison.OrdinalIgnoreCase)) return null;
            return new DifficultyModifierInfo { Value = SkillPenalty, Reason = Part + "伤" };
        }

        /// <summary>面板上那一行：「手伤 · 力量 −1」。完好时为空。</summary>
        public string Describe()
        {
            if (Band == InjuryBand.None) return string.Empty;
            return Band == InjuryBand.Severe
                ? $"{Part}伤 · {BandName(Band)} · −1颗骰"
                : $"{Part}伤 · {BandName(Band)} · {SkillName} {SkillPenalty}";
        }

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
