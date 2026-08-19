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
        public GameFailure Failure { get; private set; } = GameFailure.None;
        public IReadOnlyList<RestBlocker> RestBlockers => new List<RestBlocker>(_restBlockers.Values);

        public GameState()
        {
            Team.OnTeamChanged += CheckCollapse;
            ResetForNewGame();
        }

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

            // Initial defaults for backwards compatibility and scenes
            Set("location", "world");
            Set("chapter", 0);
            // 三派关系（官僚 / 劳工 / 富商）：底层连续整数，效果离散四档，见 engine.scm relation API。
            Set("relation:官僚", 0);
            Set("relation:劳工", 0);
            Set("relation:富商", 0);

            // Initialize inventory
            Inventory.SetCount("金钱", 15);
            Inventory.SetCount("情报", 0);
            Inventory.SetCount("药品", 1);
            Inventory.SetCount("酒", 0);
            Inventory.SetCount("香烟", 0);

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
            player.Stats["violence"] = 0;
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

        /// <summary>倒下送医的价钱。付不起就记在诊所账上，用官僚关系抵。</summary>
        public const int CollapseTreatmentFee = 80;

        // 伤势撞到倒下线：当天剩余骰子作废、付一笔治疗费、伤势回落到轻伤段。
        // 这不是 GAME OVER——倒下是最贵的兜底，不是终局（谷底校验见 docs/城市生活设计.md §2.2）。
        // ResolveCollapse 会再次触发 OnTeamChanged，靠先清 PendingCollapse 挡住重入。
        private void CheckCollapse()
        {
            if (!Team.PendingCollapse) return;
            var scar = Team.ResolveCollapse(Get<int>("世界日", 1));

            int cash = Inventory.GetCount("金钱");
            if (cash >= CollapseTreatmentFee)
            {
                Inventory.SetCount("金钱", cash - CollapseTreatmentFee);
                NotificationCenter.Push(
                    $"你在人行道上醒过来，已经躺在诊所里了。账单 {CollapseTreatmentFee} 金，先收后问。"
                    + ScarNotice(scar),
                    NotificationKind.Warning);
            }
            else
            {
                Inventory.SetCount("金钱", 0);
                string key = "relation:官僚";
                Set(key, Math.Clamp(Get<int>(key) - 1, RelationScale.Min, RelationScale.Max));
                NotificationCenter.Push(
                    "你在诊所里醒过来。身上的钱不够付账，剩下的记在了本子上——这种本子他们记得很牢。"
                    + ScarNotice(scar),
                    NotificationKind.Warning);
            }
        }

        // 送医通知的后半句：钱和骰子都会回来，这一句不会。写清楚是哪儿、扣多少，
        // 玩家从此每次投这项能力都会再看见它一遍。
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
        }
    }
}
