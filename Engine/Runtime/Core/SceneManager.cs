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
        public GameState GameState => _gameState;

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

            bool isInEncounter = !CurrentSceneName.Equals("world", StringComparison.OrdinalIgnoreCase);
            _gameState.Team.EndTurn(isInEncounter);

            Refresh();
        }

        public ActionReport ExecuteAction(GameNode node, List<SlottedResource?> slots)
        {
            Debug.Assert(node.Resolve != null, "Cannot execute action on a node that has no resolve");
            var report = new ActionReport();

            // 0. Validate slotted resources against requirements
            slots = slots ?? new List<SlottedResource?>();
            int reqCount = node.Requires?.Count ?? 0;
            int slotCount = slots.Count;
            if (reqCount != slotCount)
            {
                throw new InvalidOperationException($"Required resource count ({reqCount}) does not match slotted resource count ({slotCount}).");
            }

            if (reqCount > 0)
            {
                var usedDicePerActor = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < reqCount; i++)
                {
                    var req = node.Requires![i];
                    var slot = slots[i];
                    if (slot == null)
                    {
                        throw new InvalidOperationException($"Slot {i} for requirement of type '{req.Type}' is null.");
                    }

                    if (req.Type == "item")
                    {
                        if (slot.Type != "item")
                        {
                            throw new InvalidOperationException($"Requirement {i} requires an item, but slot contains '{slot.Type}'.");
                        }
                        if (!string.Equals(slot.ItemId, req.ItemId, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException($"Requirement {i} requires item '{req.ItemId}', but slot contains '{slot.ItemId}'.");
                        }
                        int reqQty = req.Qty;
                        int slotQty = slot.Qty > 0 ? slot.Qty : slot.Value;
                        if (slotQty != reqQty)
                        {
                            throw new InvalidOperationException($"Requirement {i} requires quantity {reqQty}, but slot specifies {slotQty}.");
                        }
                        int owned = _gameState.Inventory.GetCount(req.ItemId);
                        if (owned < reqQty)
                        {
                            throw new InvalidOperationException($"Insufficient inventory: required {reqQty} of '{req.ItemId}', but only have {owned}.");
                        }
                    }
                    else if (req.Type == "die")
                    {
                        if (slot.Type != "die")
                        {
                            throw new InvalidOperationException($"Requirement {i} requires a die, but slot contains '{slot.Type}'.");
                        }
                        
                        string actorId = string.IsNullOrEmpty(slot.ActorId) ? "player" : slot.ActorId;
                        var a = _gameState.Team.FindActor(actorId);
                        if (a == null)
                        {
                            throw new InvalidOperationException($"Actor '{actorId}' for slotted die not found.");
                        }
                        if (a.Status != "active")
                        {
                            throw new InvalidOperationException($"Actor '{actorId}' is not active (status: {a.Status}).");
                        }

                        string currentMode = CurrentSceneName.Equals("world", StringComparison.OrdinalIgnoreCase) ? "world" : "encounter";
                        if (currentMode == "encounter" && a.Role == "companion")
                        {
                            throw new InvalidOperationException($"Companions cannot act in encounter mode (slotted actor: {actorId}).");
                        }

                        int idx = slot.DieIndex >= 0 ? slot.DieIndex : slot.SourceIndex;
                        if (idx < 0 || idx >= a.ActionDice.Count)
                        {
                            throw new InvalidOperationException($"Die index {idx} is out of bounds for actor '{actorId}'.");
                        }

                        if (a.ActionDice[idx] != slot.Value)
                        {
                            throw new InvalidOperationException($"Die value mismatch: slotted die has value {slot.Value}, but actor's die at index {idx} has value {a.ActionDice[idx]}.");
                        }

                        if (!usedDicePerActor.ContainsKey(actorId))
                        {
                            usedDicePerActor[actorId] = new HashSet<int>();
                        }
                        if (usedDicePerActor[actorId].Contains(idx))
                        {
                            throw new InvalidOperationException($"Die at index {idx} for actor '{actorId}' is slotted more than once.");
                        }
                        usedDicePerActor[actorId].Add(idx);
                    }
                }
            }

            // 1. Determine active actor
            string activeActorId = "player";
            var actorDieSlot = slots.Find(s => s != null && s.Type == "die");
            if (actorDieSlot != null)
            {
                activeActorId = actorDieSlot.ActorId;
                if (string.IsNullOrEmpty(activeActorId))
                {
                    activeActorId = "player";
                }
            }

            var actor = _gameState.Team.FindActor(activeActorId);
            if (actor == null)
            {
                throw new InvalidOperationException($"Actor '{activeActorId}' not found.");
            }
            if (actor.Status != "active")
            {
                throw new InvalidOperationException($"Actor '{activeActorId}' is not active (status: {actor.Status}).");
            }

            string mode = CurrentSceneName.Equals("world", StringComparison.OrdinalIgnoreCase) ? "world" : "encounter";
            if (mode == "encounter" && actor.Role == "companion")
            {
                throw new InvalidOperationException("Companions cannot act in encounter mode.");
            }

            // 2. Set ActionExecutionContext
            var context = new ActionExecutionContext
            {
                ActorId = activeActorId,
                Mode = mode,
                SlottedResources = slots
            };
            _gameState.CurrentContext = context;

            try
            {
                Action consumeResources = () => {
                    var diceSlotsByActor = new Dictionary<string, List<SlottedResource>>();
                    foreach (var s in slots)
                    {
                        if (s != null && s.Type == "die")
                        {
                            string aid = string.IsNullOrEmpty(s.ActorId) ? "player" : s.ActorId;
                            if (!diceSlotsByActor.ContainsKey(aid))
                            {
                                diceSlotsByActor[aid] = new List<SlottedResource>();
                            }
                            diceSlotsByActor[aid].Add(s);
                        }
                    }

                    foreach (var kvp in diceSlotsByActor)
                    {
                        var a = _gameState.Team.FindActor(kvp.Key);
                        if (a != null)
                        {
                            var sorted = kvp.Value;
                            sorted.Sort((x, y) => y.DieIndex.CompareTo(x.DieIndex));
                            foreach (var s in sorted)
                            {
                                int idx = s.DieIndex >= 0 ? s.DieIndex : s.SourceIndex;
                                if (idx >= 0 && idx < a.ActionDice.Count)
                                {
                                    a.ActionDice.RemoveAt(idx);
                                }
                            }
                        }
                    }

                    // Consume items
                    foreach (var s in slots)
                    {
                        if (s != null && s.Type == "item")
                        {
                            int cur = _gameState.Inventory.GetCount(s.ItemId);
                            _gameState.Inventory.SetCount(s.ItemId, Math.Max(0, cur - s.Qty));
                        }
                    }
                };

                // 4. Resolve Action
                if (node.Resolve.Type == ResolveType.Instant)
                {
                    report.Type = ActionType.Instant;
                    node.Resolve.Effect?.Invoke();
                    consumeResources();
                    OnActionExecuted();
                }
                else if (node.Resolve.Type == ResolveType.Roll)
                {
                    report.Type = ActionType.Roll;
                    int chosenDieVal = actorDieSlot != null ? actorDieSlot.Value : 1;
                    report.ChosenDieValue = chosenDieVal;

                    // Get skill level
                    string skillName = node.Resolve.SkillName;
                    int skillLevel = 1;
                    if (actor.Stats.TryGetValue(skillName, out var sVal))
                    {
                        skillLevel = sVal;
                    }
                    else
                    {
                        throw new InvalidOperationException($"未知技能/属性: {skillName} 对于角色 {actor.Id}");
                    }

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

                    consumeResources();
                    OnActionExecuted();
                }
                else if (node.Resolve.Type == ResolveType.Observe)
                {
                    report.Type = ActionType.Instant;
                    consumeResources();
                    OnActionExecuted();
                }
            }
            finally
            {
                _gameState.CurrentContext = null;
            }

            return report;
        }
    }
}
