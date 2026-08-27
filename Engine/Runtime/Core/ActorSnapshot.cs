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
        /// <summary>本场是否登场——即这一场里真的会拿到骰子的人。城市里全队都在，交锋里只有主角。
        /// 凡是「谁能出手」的界面（技能预览、命运条、手牌）都读这个，不要各自再判 Status / Role。</summary>
        public bool OnStage { get; init; }
        public int Composure { get; init; }
        public int MaxComposure { get; init; }
        public int SpentGrowthPoints { get; init; }
        public IReadOnlyDictionary<string, int> Stats { get; init; } = new Dictionary<string, int>();
        /// <summary>这个人有几个骰池位置。默认由 role 决定，个别人物带自己的数字。</summary>
        public int ActionSlotCount { get; init; }
        public IReadOnlyList<int> ActionDice { get; init; } = new List<int>();
        // 与 ActionDice 平行，是固定骰池位置编号，供客户端保持空间身份。
        public IReadOnlyList<int> ActionDiceSlotIds { get; init; } = new List<int>();
        public IReadOnlyList<ActionSlotStatus> ActiveActionSlotStatuses { get; init; } = new List<ActionSlotStatus>();
        public IReadOnlyList<ActionSlotStatus> PendingActionSlotStatuses { get; init; } = new List<ActionSlotStatus>();
    }

    // 骰池位置是行动池的稳定空间；骰子可自由投入任意行动，但身体状态附着在位置上。
    public sealed class ActionSlotStatus
    {
        public int SlotId { get; init; }
        public string Label { get; init; } = string.Empty;
        public int DiePenalty { get; init; }
        /// <summary>这个位置的骰子不掷，恒定是这个点数（林那台机器）。null = 正常掷骰。</summary>
        public int? FixedDieValue { get; init; }
    }
}
