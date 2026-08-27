#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SSNoir.Core
{
    public class GameState
    {
        private readonly Dictionary<string, object> _states = new Dictionary<string, object>();
        private readonly Dictionary<string, RestBlocker> _restBlockers = new Dictionary<string, RestBlocker>();

        public TeamState Team { get; } = new TeamState();
        public InventoryState Inventory { get; } = new InventoryState();
        public NotificationCenter NotificationCenter { get; } = new NotificationCenter();
        public SpotlightCenter SpotlightCenter { get; } = new SpotlightCenter();
        public NarrationCenter NarrationCenter { get; } = new NarrationCenter();
        public DialogueCenter DialogueCenter { get; } = new DialogueCenter();
        public ActionExecutionContext? CurrentContext { get; set; } = null;
        public ActionReport? CurrentActionReport { get; set; } = null;
        /// <summary>
        /// 伤势到顶后已经完成医疗数值结算、但还没有由 SceneManager 完成交锋中断与送医演出的记录。
        /// 它故意不进存档：倒下在一次动作/回合结算结束前必须被同步消费。
        /// </summary>
        public Hospitalization? PendingHospitalization { get; private set; }
        public bool HasPendingHospitalization => PendingHospitalization != null;
        public GameFailure Failure { get; private set; } = GameFailure.None;
        public IReadOnlyList<RestBlocker> RestBlockers => new List<RestBlocker>(_restBlockers.Values);

        public GameState()
        {
            Team.OnTeamChanged += CheckCollapse;
            ResetForNewGame();
        }

        /// <summary>城里两个认得你的圈子。声誉键、面板与内容层的合法值都以这里为准。</summary>
        public static readonly string[] Circles = { "老码头", "商业圈" };

        public void ResetForNewGame()
        {
            Failure = GameFailure.None;
            _restBlockers.Clear();
            _states.Clear();
            Team.ResetForNewGame();
            Inventory.ApplySaveData(new Dictionary<string, int>());
            NotificationCenter.Clear();
            SpotlightCenter.Dismiss();
            CurrentContext = null;
            CurrentActionReport = null;
            PendingHospitalization = null;

            // Initial defaults for backwards compatibility and scenes
            Set("location", "world");
            Set("chapter", 0);
            // 圈内声誉（老码头 / 商业圈）：底层连续整数，效果离散档位，见 engine.scm relation API。
            // 它记的是「你的名声在哪个圈子里传开了」，不是阵营归属——所以没有成员名单，
            // 也没有第三条覆盖全城的官方关系（市政与警署由具名人物状态承担）。
            foreach (string circle in Circles)
                Set("relation:" + circle, 0);

            // Initialize inventory
            Inventory.SetCount("金钱", 15);
            Inventory.SetCount("情报", 0);
            Inventory.SetCount("药品", 1);
            Inventory.SetCount("酒", 0);
            // 开局自带两根烟。它在城市里一点用也没有，但它一直挂在物品栏上——
            // 等第一场交锋来的时候，答案已经在兜里了，玩家不用先去学一条规则再去买。
            Inventory.SetCount("香烟", 2);

            // 主角
            var player = new ActorState
            {
                Id = "player",
                Name = "尼尔",
                Role = "protagonist",
                Status = "active",
                Composure = TeamState.MaxComposure,
                ActionSlotCount = TeamState.ProtagonistActionSlotCount
            };
            player.Stats["violence"] = -1;
            player.Stats["knowledge"] = 1;
            player.Stats["sharpness"] = 0;
            player.Stats["social"] = 0;
            Team.Actors.Add(player);
            // SceneManager 进入场景时统一掷骰；这里不预生成没有骰池位置身份的裸骰。

            // 开局单人。同伴改为通过剧情 / 支线招募后加入，
            // 招募 = +1 行动力，是"花预算换更多预算"的核心 pull（招募逻辑待后续接入）。
        }

        // 读档先清除上一次会话的失败；随后恢复的健康为 0 时会由事件立即重新置失败。
        public void PrepareForLoad()
        {
            Failure = GameFailure.None;
            _restBlockers.Clear();
            PendingHospitalization = null;
        }

        public void RegisterRestBlocker(string id, string reason, string locationName, string targetNodeName)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(reason)
                || string.IsNullOrWhiteSpace(locationName) || string.IsNullOrWhiteSpace(targetNodeName))
                throw new ArgumentException("rest blocker requires non-empty id, reason, location, and target node");
            _restBlockers[id] = new RestBlocker(id, reason, locationName, targetNodeName);
        }

        public void ReleaseRestBlocker(string id) => _restBlockers.Remove(id);

        public void ClearRestBlockers() => _restBlockers.Clear();

        public void FailGame(string title, string description)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("failure title must be a non-empty string", nameof(title));
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("failure description must be a non-empty string", nameof(description));
            if (Failure.IsFailed)
                return;

            Failure = new GameFailure(true, title, description);
        }

        /// <summary>倒下送医的价钱。付得起就付，付不起的那份代价落在伤口上。</summary>
        public const int CollapseTreatmentFee = 80;

        // 伤势撞到倒下线：当天剩余骰子作废、付一笔治疗费、送医后伤势与冷静归零并留疤。
        // 这里只做跨系统的数值结算并留下 PendingHospitalization；交锋怎样失败/重试、
        // 何时结束当天以及客户端怎样进入诊所，由 SceneManager 在当前动作边界统一完成。
        // 这不是 GAME OVER——倒下是最贵的兜底，不是终局（谷底校验见 docs/城市生活设计.md §2.2）。
        // ResolveCollapse 会再次触发 OnTeamChanged，靠先清 PendingCollapse 挡住重入。
        //
        // 付不起不记账：这座城不会追着一个穷人要八十块，它只是不在他身上多花时间。
        // 代价因此落回已有的永久系统——同一处再叠一道疤，缝得快，那只手就废得更彻底。
        private void CheckCollapse()
        {
            if (!Team.PendingCollapse) return;
            if (PendingHospitalization != null)
                throw new InvalidOperationException(
                    "主角倒下后，同一次动作仍在继续施加伤势。倒下必须终止该动作后续伤害。");
            string hurtPart = Team.Injury.Part;
            int day = Get<int>("世界日", 1);
            var scar = Team.ResolveCollapse(day);

            int cash = Inventory.GetCount("金钱");
            if (cash >= CollapseTreatmentFee)
            {
                Inventory.SetCount("金钱", cash - CollapseTreatmentFee);
                PendingHospitalization = new Hospitalization(
                    $"你在人行道上醒过来，已经躺在诊所里了。账单 {CollapseTreatmentFee} 金，先收后问。"
                    + ScarNotice(scar));
            }
            else
            {
                Inventory.SetCount("金钱", 0);
                var second = Team.Scars.Add(hurtPart, day);
                PendingHospitalization = new Hospitalization(
                    "你在诊所里醒过来。身上的钱不够，他们没在你身上多花时间——缝得很快，线头留在外面。"
                    + ScarNotice(scar) + DeepScarNotice(second));
            }
        }

        public Hospitalization ConsumeHospitalization()
        {
            var hospitalization = PendingHospitalization
                ?? throw new InvalidOperationException("No pending hospitalization to consume.");
            PendingHospitalization = null;
            return hospitalization;
        }

        // 送医聚光卡的后半句：钱和骰子都会回来，这一句不会。写清楚是哪儿、扣多少，
        // 玩家从此每次投这项能力都会再看见它一遍。
        // 治得糙的那一道：同一处、同一天，第二次 −1 叠上去。
        private static string DeepScarNotice(ScarRecord scar) =>
            $" 这道缝得潦草，{scar.SkillName} 再 {ScarSet.PenaltyPerScar}。";

        private static string ScarNotice(ScarRecord scar) =>
            $" 缝合的地方留了道疤：{scar.Part}上的旧伤，{scar.SkillName} 永久 {ScarSet.PenaltyPerScar}。";

        // Pure global key-value store only (chapter, reputation, story flags).
        // Team / Inventory / action dice / growth are owned by their typed objects
        // (GameState.Team, GameState.Inventory) — read them there, not through here.
        public T Get<T>(string key, T defaultValue = default!)
        {
            if (!_states.TryGetValue(key, out var val))
            {
                return defaultValue;
            }

            try
            {
                if (typeof(T) == typeof(int) && val is double d)
                {
                    return (T)(object)(int)d;
                }
                if (typeof(T) == typeof(int) && val is long l)
                {
                    return (T)(object)(int)l;
                }
                return (T)val;
            }
            catch (InvalidCastException)
            {
                Debug.Fail($"GameState: Key '{key}' has invalid type cast to {typeof(T)} (actual: {val.GetType()})");
                throw;
            }
        }

        public void Set(string key, object value)
        {
            Debug.Assert(value != null, "GameState set value cannot be null");
            _states[key] = value;
        }

        // Returns only the pure global key-value store (chapter, reputation, etc.).
        // Team, Inventory, and ActionDice are owned by their respective objects and are NOT included here.
        public Dictionary<string, object> GetPureGlobals()
        {
            return new Dictionary<string, object>(_states);
        }

        // Replaces the entire pure globals dict. Clears old keys (no stale flags left over).
        // Loading always restores into world mode, so location is reset to "world" here;
        // SceneManager.LoadGame rebuilds the render tree explicitly afterwards.
        public void ReplacePureGlobals(Dictionary<string, object> globals)
        {
            _states.Clear();
            foreach (var kv in globals)
                _states[kv.Key] = kv.Value;
            _states["location"] = "world";
            MigrateLegacyRelations();
        }

        // 旧档里的三派声望：劳工/富商改名，官僚整条取消（市政与警署改由具名人物状态承担）。
        // 不留兼容分支——旧键在这里就地换掉或丢掉，别让它们作为孤儿键漂在 globals 里。
        private static readonly Dictionary<string, string> LegacyRelationKeys = new()
        {
            ["relation:劳工"] = "relation:老码头",
            ["relation:富商"] = "relation:商业圈",
        };

        private void MigrateLegacyRelations()
        {
            foreach (var kv in LegacyRelationKeys)
            {
                if (!_states.TryGetValue(kv.Key, out var value)) continue;
                _states.Remove(kv.Key);
                if (!_states.ContainsKey(kv.Value))
                    _states[kv.Value] = value;
            }
            _states.Remove("relation:官僚");
            foreach (string circle in Circles)
            {
                string key = "relation:" + circle;
                if (!_states.ContainsKey(key)) _states[key] = 0;
            }
        }
    }
}
