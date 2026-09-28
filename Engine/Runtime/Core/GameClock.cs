#nullable enable
using System;

namespace SSNoir.Core
{
    // 作者声明的是「这是什么状态」，不是「画成什么形状」。具体画成格子、圆盘还是
    // 纯文字，由渲染层按 Kind + Max 决定，脚本不参与。
    //
    // 注意 Gauge 不含方向。它只是「一格一格的量，现在有多少 / 总共多少」——
    // 调查进度从 0 填到满是它，生命值从满打到 0 也是它，将来有回血就是它往回涨。
    // Current 的走向由脚本决定，样式不替脚本规定。
    public enum ClockStyle
    {
        Gauge,      // 一格一格的量：满或空会触发事情，方向由脚本自己决定
        Countdown,  // 时间在逼近，归零触发。这一条是有方向的：它只会往下走
        Readout     // 当前是多少，满/空都不触发任何事
    }

    public class GameClock
    {
        public string Label { get; set; } = string.Empty;
        public string DisplayLabel { get; set; } = string.Empty;
        public string ShownLabel => string.IsNullOrEmpty(DisplayLabel) ? Label : DisplayLabel;
        public string Note { get; set; } = string.Empty;
        public int Current { get; set; }
        public int Max { get; set; }
        public ClockStyle Style { get; set; } = ClockStyle.Gauge;
    }
}
