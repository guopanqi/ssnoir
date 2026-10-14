#nullable enable
using System;

namespace SSNoir.Core
{
    /// <summary>
    /// A+ 概率判定：准备值 B = 行动骰 + 技能 + 可见修正。
    /// 行动骰在回合开始时已经确定；执行时只按公示的百分比抽取一次结果，
    /// 不再出现第二颗可见的命运骰。好 / 中 / 坏的总概率恒为 100%。
    /// </summary>
    public static class FateStrip
    {
        public static (int Fail, int Neutral, int Success) PercentagesForPrepared(int prepared)
        {
            return prepared switch
            {
                <= 1 => (60, 40, 0),
                2 => (45, 45, 10),
                3 => (35, 45, 20),
                4 => (20, 35, 45),
                5 => (0, 40, 60),
                6 => (0, 25, 75),
                7 => (0, 10, 90),
                _ => (0, 0, 100), // B ≥ 8
            };
        }

        public static RollOutcome[] Compute(int placedDie, int skill, int modSum)
        {
            ValidateInputs(placedDie, skill);
            return StripForPrepared(PreparedValue(placedDie, skill, modSum));
        }

        /// <summary>percentageRoll 属于 [1,100]；仅用于内部抽样，不作为额外骰子展示。</summary>
        public static RollOutcome Resolve(int placedDie, int skill, int modSum, int percentageRoll)
        {
            ValidateInputs(placedDie, skill);
            if (percentageRoll < 1 || percentageRoll > 100)
                throw new ArgumentOutOfRangeException(nameof(percentageRoll),
                    "Percentage roll must be between 1 and 100.");
            var (fail, neutral, _) = PercentagesForPrepared(PreparedValue(placedDie, skill, modSum));
            return percentageRoll <= fail ? RollOutcome.Fail
                 : percentageRoll <= fail + neutral ? RollOutcome.Neutral
                 : RollOutcome.Success;
        }

        public static int PreparedValue(int placedDie, int skill, int modSum)
        {
            ValidateInputs(placedDie, skill);
            return placedDie + skill + modSum;
        }

        /// <summary>百分位排列用于预览与回归测试；实际结算和预览共用同一张概率表。</summary>
        public static RollOutcome[] StripForPrepared(int prepared)
        {
            var (fail, neutral, _) = PercentagesForPrepared(prepared);
            var strip = new RollOutcome[100];
            for (int i = 0; i < strip.Length; i++)
                strip[i] = i < fail ? RollOutcome.Fail
                         : i < fail + neutral ? RollOutcome.Neutral
                         : RollOutcome.Success;
            return strip;
        }

        public static string Describe(RollOutcome[] strip)
        {
            if (strip == null || strip.Length != 100)
                throw new ArgumentException("Percentage strip must contain exactly 100 outcomes.", nameof(strip));

            int fail = 0, neutral = 0, success = 0;
            foreach (var outcome in strip)
            {
                switch (outcome)
                {
                    case RollOutcome.Fail: fail++; break;
                    case RollOutcome.Neutral: neutral++; break;
                    case RollOutcome.Success: success++; break;
                    default: throw new ArgumentOutOfRangeException(nameof(strip));
                }
            }
            return $"坏 {fail}% · 中 {neutral}% · 好 {success}%";
        }

        private static void ValidateInputs(int placedDie, int skill)
        {
            if (placedDie < 1 || placedDie > 6)
                throw new ArgumentOutOfRangeException(nameof(placedDie), "Placed action die must be between 1 and 6.");
            if (skill < TeamState.MinStatLevel || skill > TeamState.MaxStatLevel)
                throw new ArgumentOutOfRangeException(nameof(skill),
                    $"Skill must be between {TeamState.MinStatLevel} and {TeamState.MaxStatLevel}.");
        }
    }
}
