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
        
        // 冷静：纯缓冲。花掉本身没有惩罚，归零后每一点消耗直接转成伤势
        // ——机械效果只由伤势一处承担（见 docs/城市生活设计.md §2.2）。
        private int _composure = TeamState.MaxComposure;
        /// <summary>主角的冷静是跨日资源；同伴的冷静表示他今天还能陪你行动多久，
        /// 因此按骰位数缩放：每颗骰子 2 点。</summary>
        public int MaxComposure => Role == "companion"
            ? Math.Max(1, ActionSlotCount) * TeamState.CompanionComposurePerActionSlot
            : TeamState.MaxComposure;
        public int Composure
        {
            get => _composure;
            set => _composure = Math.Clamp(value, 0, MaxComposure);
        }

        // 骰值列表只保存尚未投入行动的骰子；SlotIds 与它严格平行，记录它来自
        // 固定骰池位置。不能再把列表下标当作位置身份：骰子被花掉后下标会移动。
        public List<int> ActionDice { get; } = new List<int>();
        public List<int> ActionDiceSlotIds { get; } = new List<int>();

        // 行动者自己的身体状态都挂在自己的骰池位置上。
        public int? HangoverSlotId { get; set; }

        // 人物经历造成的永久骰位状态。协作者每天只有 slot 0，一项永久损伤
        // 会直接附着在这颗骰上，并与宿醉等临时状态叠加。
        public string PermanentDiePenaltyLabel { get; set; } = string.Empty;
        public int PermanentDiePenalty { get; set; }

        // 这个人每次上场发几颗骰。默认由 role 决定（主角 4、同伴 1），但个别人物
        // 可以带着自己的数字入队——弗兰克带一队人来，他的行动本来就不止一次。
        public int ActionSlotCount { get; set; } = TeamState.CompanionActionSlotCount;

        // 恒定骰点：这个人的骰子不掷，每次都是同一个数。它是**人物**的性质
        // （林和他那台机器），发骰时落到骰位上，并在骰池上显示成一枚徽章。
        public int? FixedDieValue { get; set; }
        public string FixedDieLabel { get; set; } = string.Empty;

        public bool HasDefaultDieProfile =>
            FixedDieValue == null
            && ActionSlotCount == TeamState.GetDefaultActionSlotCount(Role);

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
