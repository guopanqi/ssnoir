#nullable enable
using System.Collections.Generic;

namespace SSNoir.Core
{
    public sealed class PresentationSnapshot
    {
        public GameNode? RootNode { get; init; }
        // 随身动作（烟、酒）：玩家自己带进这一场的东西，不在渲染树里。
        // 客户端把它们画成从对应物品引出去的小卡，而不是排进场上的卡片区。
        // 见 SceneManager.CurrentCarryNodes。
        public IReadOnlyList<GameNode> CarryNodes { get; init; } = new List<GameNode>();
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
        // 带上限的物品才在这张表里（现在只有香烟）。绝大多数东西没有上限，客户端据此
        // 决定物品格是画数字还是画容量刻度——不是给每个格子都加一层容量。
        // 内容侧的表在 engine.scm 的 item-capacities。
        public IReadOnlyDictionary<string, int> ItemCapacities { get; init; } = new Dictionary<string, int>();
        // 卷宗：城里所有故事线各自的「现在」。交锋里为空——那时候没有别的线可想。
        public IReadOnlyList<DossierEntry> Dossier { get; init; } = new List<DossierEntry>();
        // 关系支援：已经拿到的每一条，和这一场带着哪一条。成长面板里选。
        public IReadOnlyList<SupportEntry> Supports { get; init; } = new List<SupportEntry>();
        public IReadOnlyList<ActorSnapshot> Actors { get; init; } = new List<ActorSnapshot>();
        // 交锋里只有主角行动（同伴不发骰），客户端据此决定是否隐藏同伴的人物簇。
        public bool IsInEncounter { get; init; }
    }
}
