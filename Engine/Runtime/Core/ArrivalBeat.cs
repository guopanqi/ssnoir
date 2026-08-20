#nullable enable
using System;

namespace SSNoir.Core
{
    /// <summary>
    /// 地点入场节拍：玩家真正走进一个 Place 时，按声明顺序执行一次。
    ///
    /// Id 只用于诊断与校验，不进存档。引擎不记「这一拍放过没有」——
    /// 一拍还该不该发生，由内容自己的状态回答（拼树时决定它在不在 :arrivals 里）。
    /// 内容不置标记就是每次进入都播，这是特性：define-turn-rule 一直是同样的契约。
    /// </summary>
    public class ArrivalBeat
    {
        public string Id { get; init; } = string.Empty;
        public Action Effect { get; init; } = () => { };
    }
}
