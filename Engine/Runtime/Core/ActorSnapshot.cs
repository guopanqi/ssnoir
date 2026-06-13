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
        public int Stress { get; init; }
        public int SpentGrowthPoints { get; init; }
        public IReadOnlyDictionary<string, int> Stats { get; init; } = new Dictionary<string, int>();
        public IReadOnlyList<int> ActionDice { get; init; } = new List<int>();
    }
}
