#nullable enable
using System.Collections.Generic;

namespace SSNoir.Core
{
    public sealed class ActorSnapshot
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public int Composure { get; init; }
        public int SpentGrowthPoints { get; init; }
        public IReadOnlyDictionary<string, int> Stats { get; init; } = new Dictionary<string, int>();
        public IReadOnlyList<int> ActionDice { get; init; } = new List<int>();
        // 与 ActionDice 平行，是固定骰池位置编号（0/1/2），供客户端保持空间身份。
        public IReadOnlyList<int> ActionDiceSlotIds { get; init; } = new List<int>();
        public IReadOnlyList<ActionSlotStatus> ActiveActionSlotStatuses { get; init; } = new List<ActionSlotStatus>();
        public IReadOnlyList<ActionSlotStatus> PendingActionSlotStatuses { get; init; } = new List<ActionSlotStatus>();
    }

    // 三个骰池位置是行动池的稳定空间；骰子可自由投入任意行动，但身体状态附着在位置上。
    public sealed class ActionSlotStatus
    {
        public int SlotId { get; init; }
        public string Label { get; init; } = string.Empty;
        public int DiePenalty { get; init; }
    }
}
