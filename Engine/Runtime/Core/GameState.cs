#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SSNoir.Core
{
    public class GameState
    {
        private readonly Dictionary<string, object> _states = new Dictionary<string, object>();

        public event Action? OnStateChanged;

        public GameState()
        {
            // Initial defaults
            Set("money", 50);
            Set("health", 100);
            Set("location", "home");

            Set("reputation:mayor", 0);
            Set("reputation:workers", 0);
            Set("reputation:elites", 0);

            var rand = new Random();
            Set("action-dice", new List<object> { rand.Next(1, 7), rand.Next(1, 7), rand.Next(1, 7) });
            Set("item:酒", 1);
            Set("item:枪", 1);
        }

        public T Get<T>(string key, T defaultValue = default!)
        {
            if (!_states.TryGetValue(key, out var val))
            {
                return defaultValue;
            }

            try
            {
                // In Scheme, numbers are double/long. Convert if needed
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
            OnStateChanged?.Invoke();
        }

        public Dictionary<string, object> GetAllStates()
        {
            return new Dictionary<string, object>(_states);
        }
    }
}
