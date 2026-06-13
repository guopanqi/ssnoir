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
        public ActionExecutionContext? CurrentContext { get; set; } = null;

        public event Action? OnStateChanged;

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

            var rand = new Random();

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

            Team.OnTeamChanged += () => OnStateChanged?.Invoke();
            Inventory.OnInventoryChanged += () => OnStateChanged?.Invoke();
        }

        public T Get<T>(string key, T defaultValue = default!)
        {
            if (key.Equals("health", StringComparison.OrdinalIgnoreCase))
            {
                return (T)(object)Team.Health;
            }
            if (key.Equals("growth-level", StringComparison.OrdinalIgnoreCase))
            {
                return (T)(object)Team.GrowthLevel;
            }
            if (key.StartsWith("actor:spent-growth-points:", StringComparison.OrdinalIgnoreCase))
            {
                string actorId = key.Substring("actor:spent-growth-points:".Length);
                var actor = Team.FindActor(actorId)
                    ?? throw new ArgumentException($"Actor '{actorId}' not found. Cannot get spent growth points.");
                return (T)(object)actor.SpentGrowthPoints;
            }
            if (key.StartsWith("actor:available-growth-points:", StringComparison.OrdinalIgnoreCase))
            {
                string actorId = key.Substring("actor:available-growth-points:".Length);
                var actor = Team.FindActor(actorId)
                    ?? throw new ArgumentException($"Actor '{actorId}' not found. Cannot get available growth points.");
                return (T)(object)Team.GetAvailableGrowthPoints(actor);
            }
            if (key.Equals("supplies", StringComparison.OrdinalIgnoreCase))
            {
                return (T)(object)Team.Supplies;
            }
            if (key.StartsWith("item:", StringComparison.OrdinalIgnoreCase))
            {
                string itemId = key.Substring(5);
                return (T)(object)Inventory.GetCount(itemId);
            }
            if (key.Equals("action-dice", StringComparison.OrdinalIgnoreCase))
            {
                var list = new List<object>();
                foreach (var actor in Team.Actors)
                {
                    foreach (var die in actor.ActionDice)
                    {
                        list.Add(die);
                    }
                }
                return (T)(object)list;
            }
            if (key.Equals("action-dice-owners", StringComparison.OrdinalIgnoreCase))
            {
                var list = new List<object>();
                foreach (var actor in Team.Actors)
                {
                    foreach (var die in actor.ActionDice)
                    {
                        list.Add(actor.Id);
                    }
                }
                return (T)(object)list;
            }

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

            if (key.Equals("health", StringComparison.OrdinalIgnoreCase))
            {
                Team.Health = ConvertToInt(value);
                return;
            }
            if (key.Equals("growth-level", StringComparison.OrdinalIgnoreCase))
            {
                Team.GrowthLevel = ConvertToInt(value);
                OnStateChanged?.Invoke();
                return;
            }
            if (key.StartsWith("actor:spent-growth-points:", StringComparison.OrdinalIgnoreCase))
            {
                string actorId = key.Substring("actor:spent-growth-points:".Length);
                var actor = Team.FindActor(actorId)
                    ?? throw new ArgumentException($"Actor '{actorId}' not found. Cannot set spent growth points.");
                actor.SpentGrowthPoints = ConvertToInt(value);
                OnStateChanged?.Invoke();
                return;
            }
            if (key.Equals("supplies", StringComparison.OrdinalIgnoreCase))
            {
                Team.Supplies = ConvertToInt(value);
                return;
            }
            if (key.StartsWith("item:", StringComparison.OrdinalIgnoreCase))
            {
                string itemId = key.Substring(5);
                Inventory.SetCount(itemId, ConvertToInt(value));
                return;
            }

            _states[key] = value;
            OnStateChanged?.Invoke();
        }

        private int ConvertToInt(object value)
        {
            if (value is int i) return i;
            if (value is double d) return (int)d;
            if (value is long l) return (int)l;
            return Convert.ToInt32(value);
        }

        // Returns only the pure global key-value store (chapter, reputation, etc.).
        // Team, Inventory, and ActionDice are owned by their respective objects and are NOT included here.
        public Dictionary<string, object> GetPureGlobals()
        {
            return new Dictionary<string, object>(_states);
        }

        // Replaces the entire pure globals dict. Clears old keys (no stale flags left over).
        // Forces location=world regardless of what the save dict contains,
        // so HandleGlobalStateChanged in SceneManager cannot trigger a stray LoadScene.
        public void ReplacePureGlobals(Dictionary<string, object> globals)
        {
            _states.Clear();
            foreach (var kv in globals)
                _states[kv.Key] = kv.Value;
            _states["location"] = "world";
            OnStateChanged?.Invoke();
        }
    }
}
