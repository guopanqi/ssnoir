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
            Set("reputation:mayor", 0);
            Set("reputation:workers", 0);
            Set("reputation:elites", 0);

            // Initialize Inventory
            Inventory.SetCount("金钱", 50);
            Inventory.SetCount("物资包", 0);
            Inventory.SetCount("枪", 1);
            Inventory.SetCount("酒", 0);

            // Initialize Team
            Team.Health = 8;
            Team.Supplies = 3;

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
            player.Stats["coding"] = 1;
            player.ActionDice.Add(rand.Next(1, 7));
            player.ActionDice.Add(rand.Next(1, 7));
            Team.Actors.Add(player);

            // 同伴：安娜
            var anna = new ActorState
            {
                Id = "anna",
                Name = "安娜",
                Role = "companion",
                Status = "active",
                Stress = 0
            };
            anna.Stats["violence"] = 1;
            anna.Stats["knowledge"] = 1;
            anna.Stats["sharpness"] = 2;
            anna.Stats["coding"] = 1;
            anna.ActionDice.Add(rand.Next(1, 7));
            anna.ActionDice.Add(rand.Next(1, 7));
            Team.Actors.Add(anna);

            // 同伴：老周
            var laozhou = new ActorState
            {
                Id = "laozhou",
                Name = "老周",
                Role = "companion",
                Status = "active",
                Stress = 0
            };
            laozhou.Stats["violence"] = 2;
            laozhou.Stats["knowledge"] = 1;
            laozhou.Stats["sharpness"] = 1;
            laozhou.Stats["coding"] = 2;
            laozhou.ActionDice.Add(rand.Next(1, 7));
            laozhou.ActionDice.Add(rand.Next(1, 7));
            Team.Actors.Add(laozhou);
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
