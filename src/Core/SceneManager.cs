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
            var nodes = new List<GameNode>();

            if (rawData is List<object> list)
            {
                foreach (var item in list)
                {
                    if (item is List<object> expr && expr.Count > 0 && expr[0] is Schemy.Symbol tag)
                    {
                        if (tag.AsString == "node")
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

            // Recursively collect all clocks from the node tree to support tests/other consumers
            var flatClocks = new List<GameClock>();
            CollectClocksRecursive(nodes, flatClocks);
            CurrentClocks = flatClocks;
            
            OnWorldRefreshed?.Invoke();
        }

        private void CollectClocksRecursive(List<GameNode> nodes, List<GameClock> result)
        {
            foreach (var node in nodes)
            {
                result.AddRange(node.Clocks);
                CollectClocksRecursive(node.Children, result);
            }
        }

        public void OnActionExecuted()
        {
            _interpreter?.Eval("(on-action)");
            Refresh();
        }

        public void ExecuteEffect(GameNode node)
        {
            Debug.Assert(node.Resolve != null, "Cannot execute effect on a node that has no resolve");

            if (node.Resolve.Type == ResolveType.Instant)
            {
                node.Resolve.Effect?.Invoke();
            }
            else if (node.Resolve.Type == ResolveType.Roll)
            {
                node.Resolve.OnSuccess?.Invoke();
            }

            OnActionExecuted();
        }
    }
}
