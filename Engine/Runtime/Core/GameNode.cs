#nullable enable
using System;
using System.Collections.Generic;

namespace SSNoir.Core
{
    public class GameNode
    {
        public string Name { get; set; } = string.Empty;
        public string? AnchorName { get; set; }
        public string Subtitle { get; set; } = string.Empty;
        public bool Disabled { get; set; }
        public List<GameClock> Clocks { get; } = new List<GameClock>();
        public List<string> Tags { get; set; } = new List<string>();
        public List<GameNode> Children { get; set; } = new List<GameNode>();
        
        public List<ActionCost> Requires { get; set; } = new List<ActionCost>();
        public GameResolve? Resolve { get; set; }

        /// <summary>
        /// 随身动作所属的物品（"香烟"/"酒"）。非空＝这张卡不属于任何一场交锋，
        /// 是玩家自己带进来的东西；它不在渲染树里，由 PresentationSnapshot.CarryNodes 单独交给客户端，
        /// 客户端把它画在休息键上方的非场景动作区，而不是排进场上的卡片区。
        /// 见 engine.scm 的 encounter-action-nodes 与 SceneManager.RebuildRenderTree。
        /// </summary>
        public string CarryItemId { get; set; } = string.Empty;

        /// <summary>
        /// 关系支援卡所属的人物（"弗兰克"）。非空＝这张卡是玩家带进交锋的一条人物支援：
        /// 和随身动作一样不进渲染树、由 CarryNodes 单独交给客户端；它没有需求槽，仍按
        /// 标准动作卡渲染并通过执行钮使用。见 engine.scm 的 encounter-action-nodes。
        /// </summary>
        public string SupportId { get; set; } = string.Empty;

        /// <summary>
        /// 世界地点。只有世界根的直接子节点能是 Place；交锋树里不允许出现。
        /// 「玩家现在站在哪」在引擎里没有表示，但「哪些容器算走进去了」有——
        /// 客户端只对 Place 调用 SceneManager.EnterPlace，交锋容器、人物容器、
        /// 地点内部的子容器因此结构性地不会触发入场，不需要逐个特判。
        /// </summary>
        public bool IsPlace { get; set; }

        /// <summary>走进这个 Place 时按顺序执行的入场节拍。见 ArrivalBeat。</summary>
        public List<ArrivalBeat> Arrivals { get; set; } = new List<ArrivalBeat>();

        public bool HasChildren => Children != null && Children.Count > 0;
        public bool IsContainer => Resolve == null;
        public bool HasResolve => Resolve != null;
        public bool HasExplicitAnchor => AnchorName != null;
        public string EffectiveAnchorName => AnchorName ?? Name;
    }
}
