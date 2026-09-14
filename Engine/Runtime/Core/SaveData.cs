#nullable enable
using System.Collections.Generic;

namespace SSNoir.Core
{
    public class SaveData
    {
        public string SaveTime { get; set; } = "";
        public Dictionary<string, object> Globals { get; set; } = new();
        public Dictionary<string, object> Settings { get; set; } = new();
        public TeamSaveData Team { get; set; } = new();
        public Dictionary<string, int> Inventory { get; set; } = new();
        public object? WorldData { get; set; }
    }

    public class TeamSaveData
    {
        // 伤势只存刻度和部位名；命中的能力由部位表推回，不存两份会互相矛盾的字段。
        public int InjurySeverity { get; set; }
        public string InjuryPart { get; set; } = "";
        // 疤痕同理：只存部位与留下的日子，能力由部位表推回。
        public List<ScarSaveData> Scars { get; set; } = new();
        public int GrowthLevel { get; set; }
        public List<ActorSaveData> Actors { get; set; } = new();
        // 旧存档没有骰池字段；读取时用这一位区分“旧格式”与“当天骰子确实已耗尽”。
        public bool HasSavedActionDice { get; set; }
        // 关系支援：已获得的人物支援，以及进交锋时带的那一个（见 TeamState.Supports）。
        public List<string> Supports { get; set; } = new();
        public string CarriedSupport { get; set; } = "";
    }

    public class ScarSaveData
    {
        public string Part { get; set; } = "";
        public int Day { get; set; }
    }

    public class ActorSaveData
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Role { get; set; } = "";
        public string Status { get; set; } = "";
        public int Composure { get; set; }
        public int? HangoverSlotId { get; set; }
        public string PermanentDiePenaltyLabel { get; set; } = "";
        public int PermanentDiePenalty { get; set; }
        public int SpentGrowthPoints { get; set; }
        public Dictionary<string, int> Stats { get; set; } = new();
        // 两张表严格平行：骰值 + 固定骰池位置。空表表示当天已经没有可用骰子。
        public List<int> ActionDice { get; set; } = new();
        public List<int> ActionDiceSlotIds { get; set; } = new();
    }
}
