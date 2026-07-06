#nullable enable
using System;

namespace SSNoir.Core
{
    public readonly struct RollOddsResult
    {
        public readonly double Fail;
        public readonly double Neutral;
        public readonly double Success;

        public RollOddsResult(double fail, double neutral, double success)
        {
            Fail = fail;
            Neutral = neutral;
            Success = success;
        }
    }

    // 判定结果概率的精确解。判定规则（见 SceneManager）：一颗运气骰(d6均匀) 加上一个固定位移
    // = 放入骰的±修正(以4为中枢) + 技能平档加成(每级+1) + 难度修正，按 ≤2 失败 / 3–4 中性 / ≥5 成功 分档。
    // 唯一随机源是那颗 d6，故三档概率可精确计算，作为卡面上诚实的实时预览。
    public static class RollOdds
    {
        public static RollOddsResult Compute(int placedDie, int skillLevel, int modSum)
        {
            placedDie = Math.Clamp(placedDie, 1, 6);
            if (skillLevel < 1)
            {
                skillLevel = 1;
            }
            int shift = (placedDie - 4) + (skillLevel - 1) + modSum;

            double fail = 0, neutral = 0, success = 0;
            const double each = 1.0 / 6.0;
            for (int luck = 1; luck <= 6; luck++)
            {
                int modified = luck + shift;
                if (modified <= 2)
                {
                    fail += each;
                }
                else if (modified <= 4)
                {
                    neutral += each;
                }
                else
                {
                    success += each;
                }
            }
            return new RollOddsResult(fail, neutral, success);
        }
    }
}
