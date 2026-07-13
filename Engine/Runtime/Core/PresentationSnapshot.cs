#nullable enable
using System.Collections.Generic;

namespace SSNoir.Core
{
    public sealed class PresentationSnapshot
    {
        public GameNode? RootNode { get; init; }
        public int Health { get; init; }
        public int MaxHealth { get; init; }
        public int Satiety { get; init; }
        public int MaxSatiety { get; init; }
        public int GrowthLevel { get; init; }
        public string Location { get; init; } = string.Empty;
        public IReadOnlyDictionary<string, int> Inventory { get; init; } = new Dictionary<string, int>();
        // 三派关系（官僚 / 劳工 / 富商）的底层整数值。
        public IReadOnlyDictionary<string, int> Relations { get; init; } = new Dictionary<string, int>();
        // 内容层配置的关系里程碑解锁说明，键为“势力:档位”（如“劳工:脸熟”）。
        public IReadOnlyDictionary<string, string> RelationUnlocks { get; init; } = new Dictionary<string, string>();
        public IReadOnlyList<ActorSnapshot> Actors { get; init; } = new List<ActorSnapshot>();
    }
}
