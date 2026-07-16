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
        public int Health { get; set; }
        public int GrowthLevel { get; set; }
        public List<ActorSaveData> Actors { get; set; } = new();
    }

    public class ActorSaveData
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Role { get; set; } = "";
        public string Status { get; set; } = "";
        public int Composure { get; set; }
        public int? HangoverSlotId { get; set; }
        public int? FaintSlotId { get; set; }
        public int? LossOfControlSlotId { get; set; }
        public int SpentGrowthPoints { get; set; }
        public Dictionary<string, int> Stats { get; set; } = new();
        // ActionDice intentionally omitted — re-rolled on load
    }
}
