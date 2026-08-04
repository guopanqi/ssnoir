#nullable enable
using System.Collections.Generic;

namespace SSNoir.Core
{
    public sealed class PresentationSnapshot
    {
        public GameNode? RootNode { get; init; }
        public int Health { get; init; }
        public int MaxHealth { get; init; }
        public GameFailure Failure { get; init; } = GameFailure.None;
        public IReadOnlyList<RestBlocker> RestBlockers { get; init; } = new List<RestBlocker>();
        public int GrowthLevel { get; init; }
        public int WorldDay { get; init; } = 1;
        public string Location { get; init; } = string.Empty;
        public IReadOnlyDictionary<string, int> Inventory { get; init; } = new Dictionary<string, int>();
        // 三派声望（官僚 / 劳工 / 富商）的底层整数值。
        public IReadOnlyDictionary<string, int> Relations { get; init; } = new Dictionary<string, int>();
        // 内容层配置的声望档解锁诱饵，键为“势力:通用档名”（如“劳工:信任”）。
        public IReadOnlyDictionary<string, string> RelationUnlocks { get; init; } = new Dictionary<string, string>();
        // 内容层配置的各势力对正面三档的定制称呼，键同上（如“劳工:信任”→“够朋友”）。
        public IReadOnlyDictionary<string, string> RelationBandNames { get; init; } = new Dictionary<string, string>();
        public IReadOnlyList<ActorSnapshot> Actors { get; init; } = new List<ActorSnapshot>();
        // 交锋里只有主角行动（同伴不发骰），客户端据此决定是否隐藏同伴的人物簇。
        public bool IsInEncounter { get; init; }
    }
}
