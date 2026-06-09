using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using SSNoir.Scripting;

namespace SSNoir.Core
{
    public class SceneManager
    {
        private readonly GameState _gameState;
        private SchemeInterpreter? _interpreter;
        private string _currentSceneName = string.Empty;

        public event Action? OnSceneLoaded;
        public event Action? OnWorldRefreshed;

        public List<GameNode> CurrentWorldNodes { get; private set; } = new List<GameNode>();
        public List<GameClock> CurrentClocks { get; private set; } = new List<GameClock>();
        public string CurrentSceneName => _currentSceneName;

        public SceneManager(GameState gameState)
        {
            _gameState = gameState;
            _gameState.OnStateChanged += HandleGlobalStateChanged;
        }

        private void HandleGlobalStateChanged()
        {
            var loc = _gameState.Get<string>("location");
            if (loc != _currentSceneName)
            {
                LoadScene(loc);
            }
        }

        public void LoadScene(string sceneName)
        {
            _currentSceneName = sceneName;
            
            // Re-create interpreter to fully discard old local state
            _interpreter = new SchemeInterpreter(_gameState);
            
            var relativePath = $"scenes/{sceneName}.scm";
            _interpreter.LoadFile(relativePath);

            OnSceneLoaded?.Invoke();
            Refresh();
        }

        public void Refresh()
        {
            Debug.Assert(_interpreter != null, "Interpreter must not be null when refreshing scene");
            
            var rawWorld = _interpreter!.Eval("(get-world)");
            CurrentWorldNodes = NodeConverter.ConvertList(rawWorld, _interpreter.RawInterpreter);

            object val;
            bool hasGetClocks = _interpreter.RawInterpreter.Environment.TryGetValue(Schemy.Symbol.FromString("get-clocks"), out val);
            if (hasGetClocks)
            {
                var rawClocks = _interpreter.Eval("(get-clocks)");
                CurrentClocks = ConvertClocks(rawClocks);
            }
            else
            {
                CurrentClocks = new List<GameClock>();
            }
            
            OnWorldRefreshed?.Invoke();
        }

        private List<GameClock> ConvertClocks(object rawClocks)
        {
            var clocks = new List<GameClock>();
            if (rawClocks is List<object> list)
            {
                foreach (var item in list)
                {
                    if (item is List<object> clockExpr && clockExpr.Count >= 4)
                    {
                        if (clockExpr[0] is Schemy.Symbol sym && sym.AsString == "clock")
                        {
                            var label = clockExpr[1] as string ?? "Unknown";
                            
                            int current = 0;
                            if (clockExpr[2] is double d1) current = (int)d1;
                            else if (clockExpr[2] is long l1) current = (int)l1;
                            else if (clockExpr[2] is int i1) current = i1;

                            int max = 1;
                            if (clockExpr[3] is double d2) max = (int)d2;
                            else if (clockExpr[3] is long l2) max = (int)l2;
                            else if (clockExpr[3] is int i2) max = i2;

                            clocks.Add(new GameClock
                            {
                                Label = label,
                                Current = current,
                                Max = max
                            });
                        }
                    }
                }
            }
            return clocks;
        }

        public void ExecuteEffect(GameNode node)
        {
            Debug.Assert(node.HasEffect, "Cannot execute effect on a node that has no effect");

            // Execute the action (calls the scheme procedure)
            node.Effect!.Invoke();

            // Run reactive rules
            _interpreter!.Eval("(on-action)");

            // Refresh the node tree
            Refresh();
        }
    }
}
