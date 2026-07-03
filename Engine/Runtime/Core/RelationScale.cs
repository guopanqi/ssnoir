#nullable enable
using System;

namespace SSNoir.Core
{
    // 势力关系的显示刻度与离散档位——UI 与 engine.scm 的关系档位共用这一份定义
    // （engine.scm 通过 native __relation-band-index 读取，不要在别处另写一套阈值）。
    //
    // 范围 [-10, 10]，边界 {-6,-3,3,6} 把它切成 5 档：
    //   0 敌视 (< -6) | 1 冷淡 [-6,-3) | 2 中立 [-3,3) | 3 脸熟 [3,6) | 4 自己人 (>= 6)
    public static class RelationScale
    {
        public const int Min = -10;
        public const int Max = 10;

        public static readonly int[] Boundaries = { -6, -3, 3, 6 };
        public static readonly string[] BandNames = { "敌视", "冷淡", "中立", "脸熟", "自己人" };

        // 值落在第几档（0..BandNames.Length-1）。
        public static int BandIndex(int value)
        {
            int i = 0;
            while (i < Boundaries.Length && value >= Boundaries[i]) i++;
            return i;
        }

        public static string BandName(int value) => BandNames[BandIndex(value)];

        // 值在 [Min,Max] 上的比例，用于进度条定位。
        public static float Fraction(int value)
            => Math.Clamp((value - Min) / (float)(Max - Min), 0f, 1f);
    }
}
