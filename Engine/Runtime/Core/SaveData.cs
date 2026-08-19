#nullable enable
using System.Collections.Generic;

namespace SSNoir.Core
{
    public class SaveData
    {
        public string SaveTime { get; set; } = "";
        public Dictionary<string, object> Globals { get; set; } = new();
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
        // ActionDice intentionally omitted — re-rolled on load
    }
}
