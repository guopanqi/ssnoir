#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SSNoir.Core
{
    public class GameState
    {
        private readonly Dictionary<string, object> _states = new Dictionary<string, object>();

        public TeamState Team { get; } = new TeamState();
        public InventoryState Inventory { get; } = new InventoryState();
        public NotificationCenter NotificationCenter { get; } = new NotificationCenter();
        public SpotlightCenter SpotlightCenter { get; } = new SpotlightCenter();
        public NarrationCenter NarrationCenter { get; } = new NarrationCenter();
        public DialogueCenter DialogueCenter { get; } = new DialogueCenter();
        public ActionExecutionContext? CurrentContext { get; set; } = null;
        public ActionReport? CurrentActionReport { get; set; } = null;

        public GameState()
        {
            // Initial defaults for backwards compatibility and scenes
            Set("location", "world");
            Set("chapter", 0);
            // 三派关系（官僚 / 劳工 / 富商）：底层连续整数，效果离散四档，见 engine.scm relation API。
            Set("relation:官僚", 0);
            Set("relation:劳工", 0);
            Set("relation:富商", 0);

            // Initialize Inventory
            Inventory.SetCount("金钱", 15);
            Inventory.SetCount("情报", 0);
            Inventory.SetCount("食物", 2);
            Inventory.SetCount("药品", 1);
            Inventory.SetCount("枪", 1);
            Inventory.SetCount("酒", 0);

            // Initialize Team
            Team.Health = 8;
            Team.Satiety = 3;

            var rand = GameRandom.Instance;

            // 主角
            var player = new ActorState
            {
                Id = "player",
                Name = "主角",
                Role = "protagonist",
                Status = "active",
                Stress = 0
            };
            player.Stats["violence"] = 1;
            player.Stats["knowledge"] = 2;
            player.Stats["sharpness"] = 1;
            player.Stats["social"] = 1;
            player.ActionDice.Add(rand.Next(1, 7));
            player.ActionDice.Add(rand.Next(1, 7));
            Team.Actors.Add(player);

            // 开局单人。同伴（安娜 / 老周）改为通过剧情 / 支线招募后加入，
            // 招募 = +1 行动力，是"花预算换更多预算"的核心 pull（招募逻辑待后续接入）。
        }

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
