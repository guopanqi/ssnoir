#nullable enable
using System;
using System.Text;

namespace SSNoir.Core
{
    /// <summary>
    /// 判定结算：准备值 B = 放入骰 + 技能 + 修正 直接查一张「按值定制的结果分布表」，
    /// 而不是再加一颗命运骰求和。命运骰 d6 仍然摇，但只作为在赔率条上的落点，
    /// 不改变分布本身。这样每个准备值都有清晰、独立的性格（放 1 危险但有空间、放 6 碾压），
    /// 不会被第二颗骰子的求和抹平向中间回归。
    ///
    /// 分布表（六面里各档占几面，坏 / 中 / 好），低面更差、高面更好：
    ///   B≤1 : 3 / 3 / 0   —— 五五开，有空间
    ///   B=2 : 2 / 3 / 1
    ///   B=3 : 1 / 3 / 2   —— 均衡
    ///   B=4 : 1 / 2 / 3   —— 均衡偏优
    ///   B=5 : 0 / 2 / 4   —— 显著优势
    ///   B=6 : 0 / 1 / 5   —— 很强
    ///   B≥7 : 0 / 0 / 6   —— 技能+好骰堆出的「干啥都成」
    /// 技能与修正通过抬高 / 压低 B 来移动你在这张表上的行：新手放 1 落在五五开，
    /// 老手（技能4）放 1 → B=5 直接进「显著优势」。
    /// </summary>
    public static class FateStrip
    {
        public static RollOutcome[] Compute(int placedDie, int skill, int modSum)
        {
            ValidateInputs(placedDie, skill);
            return BuildStrip(PreparedValue(placedDie, skill, modSum));
        }

        public static RollOutcome Resolve(int placedDie, int skill, int modSum, int fateDie)
        {
            ValidateInputs(placedDie, skill);
            if (fateDie < 1 || fateDie > 6)
                throw new ArgumentOutOfRangeException(nameof(fateDie), "Fate die must be between 1 and 6.");
            return BuildStrip(PreparedValue(placedDie, skill, modSum))[fateDie - 1];
        }

        public static int PreparedValue(int placedDie, int skill, int modSum)
        {
            ValidateInputs(placedDie, skill);
            return placedDie + skill + modSum;
        }

        /// <summary>分布只取决于准备值 B，因此结算后仅凭存下的 PreparedValue 就能重建同一条命运条。</summary>
        public static RollOutcome[] StripForPrepared(int prepared) => BuildStrip(prepared);

        public static string Describe(RollOutcome[] strip)
        {
            if (strip == null || strip.Length != 6)
                throw new ArgumentException("Fate strip must contain exactly six outcomes.", nameof(strip));

            var text = new StringBuilder();
            int start = 0;
            while (start < strip.Length)
            {
                int end = start;
                while (end + 1 < strip.Length && strip[end + 1] == strip[start])
                    end++;

                if (text.Length > 0) text.Append(" · ");
                text.Append(start == end ? (start + 1).ToString() : $"{start + 1}–{end + 1}");
                text.Append(' ');
                text.Append(strip[start] switch
                {
                    RollOutcome.Fail => "坏",
                    RollOutcome.Neutral => "中",
                    RollOutcome.Success => "好",
                    _ => throw new ArgumentOutOfRangeException(nameof(strip)),
                });
                start = end + 1;
            }
            return text.ToString();
        }

        /// <summary>按准备值 B 返回六格命运条：低面更差、高面更好。数字可调，结构是关键。</summary>
        private static RollOutcome[] BuildStrip(int prepared)
        {
            (int fail, int neutral) = DistributionFor(prepared);

            var strip = new RollOutcome[6];
            for (int i = 0; i < 6; i++)
            {
                strip[i] = i < fail ? RollOutcome.Fail
                         : i < fail + neutral ? RollOutcome.Neutral
                         : RollOutcome.Success;
            }
            return strip;
        }

        // 返回 (坏面数, 中面数)，好面数 = 6 − 坏 − 中。
        private static (int fail, int neutral) DistributionFor(int prepared)
        {
            return prepared switch
            {
                <= 1 => (3, 3),
                2 => (2, 3),
                3 => (1, 3),
                4 => (1, 2),
                5 => (0, 2),
                6 => (0, 1),
                _ => (0, 0), // ≥7
            };
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
