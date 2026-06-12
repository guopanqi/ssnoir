#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using SSNoir.Scripting;
using Schemy;

namespace SSNoir.Core
{
    public class SceneManager
    {
        private readonly GameState _gameState;
        private readonly IScriptLoader _loader;

        private SchemeInterpreter? _worldInterpreter;
        private SchemeInterpreter? _encounterInterpreter;
        private string _encounterSceneName = string.Empty;

        public event Action? OnSceneLoaded;
        public event Action? OnWorldRefreshed;

        public List<GameNode> CurrentWorldNodes { get; private set; } = new List<GameNode>();
        public List<GameClock> CurrentClocks { get; private set; } = new List<GameClock>();

        public SchemeInterpreter ActiveInterpreter => _encounterInterpreter ?? _worldInterpreter ?? throw new InvalidOperationException("No active interpreter");
        public string CurrentSceneName => _encounterInterpreter != null ? _encounterSceneName : "world";

        public SceneManager(GameState gameState, IScriptLoader loader)
        {
            _gameState = gameState;
            _loader = loader;
            _gameState.OnStateChanged += HandleGlobalStateChanged;
        }

        private void HandleGlobalStateChanged()
        {
            var loc = _gameState.Get<string>("location");
            if (loc != CurrentSceneName)
            {
                LoadScene(loc);
            }
        }

        public void LoadScene(string sceneName)
        {
            if (sceneName == "world" || sceneName == "world/world" || sceneName == "home" || sceneName == "office" || sceneName == "club")
            {
                _encounterInterpreter = null;
                _encounterSceneName = string.Empty;
                
                if (_worldInterpreter == null)
                {
                    _worldInterpreter = new SchemeInterpreter(_gameState, _loader);
                    RegisterEncounterBridges(_worldInterpreter);
                    _worldInterpreter.LoadFile("scenes/world/world.scm");
                }
                
                if (_gameState.Get<string>("location") != "world")
                {
                    _gameState.Set("location", "world");
                }
            }
            else
            {
                // Clean the scene name by stripping "encounters/" prefix if present
                string cleanName = sceneName;
                if (cleanName.StartsWith("encounters/"))
                {
                    cleanName = cleanName.Substring("encounters/".Length);
                }

                _encounterSceneName = cleanName;
                _encounterInterpreter = new SchemeInterpreter(_gameState, _loader);
                RegisterEncounterBridges(_encounterInterpreter);
                _encounterInterpreter.LoadFile($"scenes/encounters/{cleanName}.scm");
                
                if (_gameState.Get<string>("location") != cleanName)
                {
                    _gameState.Set("location", cleanName);
                }
            }

            OnSceneLoaded?.Invoke();
            Refresh();
        }

        public void StartEncounter(string name)
        {
            LoadScene(name);
        }

        public void EndEncounter()
        {
            LoadScene("world");
        }

        private void RegisterEncounterBridges(SchemeInterpreter interpreter)
        {
            interpreter.RawInterpreter.DefineGlobal(
                Symbol.FromString("start-encounter"),
                new NativeProcedure(args =>
                {
                    if (args.Count < 1)
                        throw new ArgumentException("start-encounter requires 1 argument (encounter name)");
                    string name = args[0] is Symbol sym ? sym.AsString : args[0]?.ToString() ?? "";
                    StartEncounter(name);
                    return new None();
                }, "start-encounter")
            );

            interpreter.RawInterpreter.DefineGlobal(
                Symbol.FromString("end-encounter"),
                new NativeProcedure(args =>
                {
                    EndEncounter();
                    return new None();
                }, "end-encounter")
            );
        }

        public void Refresh()
        {
            var active = ActiveInterpreter;
            var rawData = active.Eval("(get-render-data)");
            var nodes = new List<GameNode>();

            if (rawData is List<object> list)
            {
                foreach (var item in list)
                {
                    if (item is List<object> expr && expr.Count > 0 && expr[0] is Schemy.Symbol tag)
                    {
                        if (tag.AsString == "node")
                        {
                            var parsedNode = NodeConverter.ConvertSingle(expr, active.RawInterpreter);
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
            ActiveInterpreter.Eval("(on-action)");
            Refresh();
        }

        public void EndTurn()
        {
            ActiveInterpreter.Eval("(on-turn-end)");

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

                var modifiers = node.Resolve.DifficultyModifiers;
                int modifierSum = 0;
                foreach (var mod in modifiers)
                {
                    modifierSum += mod.Value;
                }
                report.DifficultyModifiers = modifiers;

                int modifiedValue = finalValue + modifierSum;
                report.ModifiedRollValue = modifiedValue;

                if (modifiedValue <= 2)
                {
                    report.Outcome = RollOutcome.Fail;
                    node.Resolve.OnFail?.Invoke();
                }
                else if (modifiedValue <= 4)
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
