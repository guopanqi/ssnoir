#nullable enable
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
        private readonly IScriptLoader _loader;
        private SchemeInterpreter? _interpreter;
        private string _currentSceneName = string.Empty;

        public event Action? OnSceneLoaded;
        public event Action? OnWorldRefreshed;

        public List<GameNode> CurrentWorldNodes { get; private set; } = new List<GameNode>();
        public List<GameClock> CurrentClocks { get; private set; } = new List<GameClock>();
        public string CurrentSceneName => _currentSceneName;

        public SceneManager(GameState gameState, IScriptLoader loader)
        {
            _gameState = gameState;
            _loader = loader;
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
            _interpreter = new SchemeInterpreter(_gameState, _loader);
            
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

        public void EndTurn()
        {
            _interpreter?.Eval("(on-turn-end)");

            var rand = new Random();
            var newDice = new List<object> { rand.Next(1, 7), rand.Next(1, 7), rand.Next(1, 7) };
            _gameState.Set("action-dice", newDice);

            Refresh();
        }

        public ActionReport ExecuteAction(GameNode node, List<SlottedResource?> slots)
        {
            Debug.Assert(node.Resolve != null, "Cannot execute action on a node that has no resolve");
            var report = new ActionReport();

            // 1. Consume resources (dice and items/money)
            var diceToConsume = new List<int>();
            foreach (var s in slots)
            {
                if (s != null && s.Type == "die")
                {
                    diceToConsume.Add(s.SourceIndex);
                }
            }
            diceToConsume.Sort((a, b) => b.CompareTo(a));

            var diceList = _gameState.Get<List<object>>("action-dice");
            if (diceList != null)
            {
                var newDice = new List<object>(diceList);
                foreach (var idx in diceToConsume)
                {
                    if (idx >= 0 && idx < newDice.Count)
                    {
                        newDice.RemoveAt(idx);
                    }
                }
                _gameState.Set("action-dice", newDice);
            }

            foreach (var s in slots)
            {
                if (s != null && s.Type == "item")
                {
                    if (s.ItemName == "金钱")
                    {
                        int owned = _gameState.Get<int>("money");
                        _gameState.Set("money", Math.Max(0, owned - s.Value));
                    }
                    else
                    {
                        int owned = _gameState.Get<int>("item:" + s.ItemName, 0);
                        _gameState.Set("item:" + s.ItemName, Math.Max(0, owned - s.Value));
                    }
                }
            }

            // 2. Resolve Action
            if (node.Resolve.Type == ResolveType.Instant)
            {
                report.Type = ActionType.Instant;
                node.Resolve.Effect?.Invoke();
                OnActionExecuted();
            }
            else if (node.Resolve.Type == ResolveType.Roll)
            {
                report.Type = ActionType.Roll;
                var dieSlot = slots.Find(s => s != null && s.Type == "die");
                int chosenDieVal = dieSlot != null ? dieSlot.Value : 1;
                report.ChosenDieValue = chosenDieVal;

                int skillLevel = _gameState.Get<int>("skill:" + node.Resolve.SkillName, 1);
                var rand = new Random();
                var randomDice = new List<int>();
                int finalValue = chosenDieVal;

                for (int i = 0; i < skillLevel - 1; i++)
                {
                    int r = rand.Next(1, 7);
                    randomDice.Add(r);
                    if (r > finalValue)
                    {
                        finalValue = r;
                    }
                }

                report.RandomDice = randomDice;
                report.FinalRollValue = finalValue;

                if (finalValue <= 2)
                {
                    report.Outcome = RollOutcome.Fail;
                    node.Resolve.OnFail?.Invoke();
                }
                else if (finalValue <= 4)
                {
                    report.Outcome = RollOutcome.Neutral;
                    node.Resolve.OnNeutral?.Invoke();
                }
                else
                {
                    report.Outcome = RollOutcome.Success;
                    node.Resolve.OnSuccess?.Invoke();
                }

                OnActionExecuted();
            }
            else if (node.Resolve.Type == ResolveType.Observe)
            {
                report.Type = ActionType.Instant;
                OnActionExecuted();
            }

            return report;
        }
    }
}
