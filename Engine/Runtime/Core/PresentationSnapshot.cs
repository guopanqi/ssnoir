#nullable enable
using System.Collections.Generic;

namespace SSNoir.Core
{
    public sealed class PresentationSnapshot
    {
        public IReadOnlyList<GameNode> Nodes { get; init; } = new List<GameNode>();
        public int Health { get; init; }
        public int MaxHealth { get; init; }
        public int Supplies { get; init; }
        public int MaxSupplies { get; init; }
        public int GrowthLevel { get; init; }
        public string Location { get; init; } = string.Empty;
        public IReadOnlyDictionary<string, int> Inventory { get; init; } = new Dictionary<string, int>();
        public IReadOnlyDictionary<string, int> Reputation { get; init; } = new Dictionary<string, int>();
        public IReadOnlyList<ActorSnapshot> Actors { get; init; } = new List<ActorSnapshot>();
    }
}
