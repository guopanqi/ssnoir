#nullable enable
using System;
using System.Collections.Generic;

namespace SSNoir.Core
{
    public class ActorState
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; // "protagonist", "companion"
        public string Status { get; set; } = "active";  // "active", "away"
        
        // 冷静：满值=处变不惊，向 0 花，唯一常规真实池（见 docs/城市生活设计.md）。
        private int _composure = TeamState.MaxComposure;
        public int Composure
        {
            get => _composure;
            set => _composure = Math.Clamp(value, 0, TeamState.MaxComposure);
        }

        // 骰值列表只保存尚未投入行动的骰子；SlotIds 与它严格平行，记录它来自
        // 固定的三个骰池位置。不能再把列表下标当作位置身份：骰子被花掉后下标会移动。
        public List<int> ActionDice { get; } = new List<int>();
        public List<int> ActionDiceSlotIds { get; } = new List<int>();

        // 行动者自己的身体状态都挂在自己的骰池位置上。
        public int? HangoverSlotId { get; set; }
        public int? FaintSlotId { get; set; }
        public int? LossOfControlSlotId { get; set; }

        public int SpentGrowthPoints { get; set; } = 0;
        
        public Dictionary<string, int> Stats { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "violence", 0 },
            { "knowledge", 0 },
            { "sharpness", 0 },
            { "social", 0 }
        };
    }
}
