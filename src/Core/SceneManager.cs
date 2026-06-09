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
            
            var rawData = _interpreter!.Eval("(get-render-data)");
            var clocks = new List<GameClock>();
            var nodes = new List<GameNode>();

            if (rawData is List<object> list)
            {
                foreach (var item in list)
                {
                    if (item is List<object> expr && expr.Count > 0 && expr[0] is Schemy.Symbol tag)
                    {
                        if (tag.AsString == "clock")
                        {
                            var parsedClock = ParseClock(expr);
                            if (parsedClock != null)
                            {
                                clocks.Add(parsedClock);
                            }
                        }
                        else if (tag.AsString == "node")
                        {
                            var parsedNode = NodeConverter.ConvertSingle(expr, _interpreter.RawInterpreter);
                            nodes.Add(parsedNode);
                        }
                        else
                        {
                            throw new InvalidOperationException($"Unknown tag '{tag.AsString}' in render-data item: {expr}");
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException($"Invalid render-data item: {item}");
                    }
                }
            }
            else
            {
                throw new InvalidOperationException($"get-render-data must return a list of items, got {rawData?.GetType().FullName ?? "null"}");
            }

            CurrentWorldNodes = nodes;
            CurrentClocks = clocks;
            
            OnWorldRefreshed?.Invoke();
        }

        private GameClock? ParseClock(List<object> clockExpr)
        {
            if (clockExpr.Count >= 4)
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

                    return new GameClock
                    {
                        Label = label,
                        Current = current,
                        Max = max
                    };
                }
            }
            return null;
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
