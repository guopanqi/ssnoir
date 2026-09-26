#nullable enable

namespace SSNoir
{
    /// <summary>
    /// 音乐音量：0–1，跟着存档走（_sceneManager.Settings["musicVolume"]，double 存取，
    /// 读出来不是 double 就当存档坏了直接中断，和 reduceMotion 同一套规矩）。
    /// 面板上是 关/低/中/高 四档分段控件；默认低。整体偏保守：中的响度只等于以前的低。
    /// </summary>
    public static class MusicVolume
    {
        public static readonly float[] Levels = { 0f, 0.2f, 0.4f, 0.65f };
        public const int DefaultLevel = 1;

        public static float Default => Levels[DefaultLevel];

        public static float Value { get; set; } = Levels[DefaultLevel];

        /// <summary>当前值离哪一档最近——读档回来的任意浮点数都能落回某一档。</summary>
        public static int LevelIndex
        {
            get
            {
                int best = 0;
                for (int i = 1; i < Levels.Length; i++)
                {
                    if (System.Math.Abs(Levels[i] - Value) < System.Math.Abs(Levels[best] - Value))
                        best = i;
                }
                return best;
            }
        }

        public static void SetLevel(int index)
        {
            Value = Levels[System.Math.Clamp(index, 0, Levels.Length - 1)];
        }
    }
}
