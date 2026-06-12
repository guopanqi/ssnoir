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
        public ActionExecutionContext? CurrentContext { get; set; } = null;

        public event Action? OnStateChanged;

        public GameState()
        {
            // Initial defaults for backwards compatibility and scenes
            Set("location", "world");
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
            laozhou.Stats["coding"] = 1;
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

        public Dictionary<string, object> GetAllStates()
        {
            var all = new Dictionary<string, object>(_states);
            all["health"] = Team.Health;
            all["supplies"] = Team.Supplies;
            foreach (var item in Inventory.Items)
            {
                all["item:" + item.Key] = item.Value;
            }
            return all;
        }
    }
}
