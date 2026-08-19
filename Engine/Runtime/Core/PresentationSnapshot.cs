#nullable enable
using System.Collections.Generic;

namespace SSNoir.Core
{
    public sealed class PresentationSnapshot
    {
        public GameNode? RootNode { get; init; }
        // 伤势：0 为完好。部位名与档位一起决定面板那一行怎么写，客户端不自己算档位。
        public int InjurySeverity { get; init; }
        public int InjuryMaxSeverity { get; init; } = Injury.MaxSeverity;
        public string InjuryPart { get; init; } = string.Empty;
        public string InjuryBandName { get; init; } = string.Empty;
        public string InjurySkillName { get; init; } = string.Empty;
        // 被打中的那一项能力的**内部键**（violence/knowledge/…）。客户端画赔率预览时要用它
        // 判断这次判定是否吃伤势修正——预览和结算必须读同一个来源。
        public string InjurySkillKey { get; init; } = string.Empty;
        public int InjurySkillPenalty { get; init; }
        public bool InjuryCostsActionDie { get; init; }
        // 疤痕：按能力内部键聚合好的永久修正，客户端画预览时直接查表，
        // 不自己数疤——预览和结算读的必须是同一份（见 ScarSet.ModifiersBySkill）。
        public IReadOnlyDictionary<string, DifficultyModifierInfo> ScarModifiers { get; init; }
            = new Dictionary<string, DifficultyModifierInfo>();
        // 面板上那一行「旧伤 · 手 −2」。没有疤时为空。
        public string ScarSummary { get; init; } = string.Empty;
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
