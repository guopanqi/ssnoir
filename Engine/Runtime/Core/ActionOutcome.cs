#nullable enable
using System;

namespace SSNoir.Core
{
    // 一个结算分支就是一个效果。它没有标题、没有文案：结果条上的每一行都由引擎按状态变化
    // 自动写（钟 / 物品 / 关系 / 冷静 / 伤势），引擎写不出来的才由内容用 result-supplement! 补一行。
    public sealed class ActionOutcome
    {
        public Action? Effect { get; init; }
    }
}
