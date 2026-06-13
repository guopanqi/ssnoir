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
        private bool _turnEndedDuringAction;

        public event Action? OnSceneLoaded;
        public event Action? OnWorldRefreshed;

        public List<GameNode> CurrentWorldNodes { get; private set; } = new List<GameNode>();
        public List<GameClock> CurrentClocks { get; private set; } = new List<GameClock>();
        public PresentationSnapshot LatestSnapshot { get; private set; } = new PresentationSnapshot();

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

            bool nextIsInEncounter = !(sceneName == "world" || sceneName == "world/world" || sceneName == "home" || sceneName == "office" || sceneName == "club");
            _gameState.Team.RollActionDice(nextIsInEncounter);

            RebuildRenderTree();
            OnSceneLoaded?.Invoke();
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

            interpreter.RawInterpreter.DefineGlobal(
                Symbol.FromString("end-turn!"),
                new NativeProcedure(args =>
                {
                    EndTurn();
                    return new None();
                }, "end-turn!")
            );
        }

        public void Refresh() => RebuildRenderTree();

        public void RebuildRenderTree()
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

            var flatClocks = new List<GameClock>();
            CollectClocksRecursive(nodes, flatClocks);
            CurrentClocks = flatClocks;

            LatestSnapshot = BuildPresentationSnapshot(nodes);
            
            OnWorldRefreshed?.Invoke();
        }

        private PresentationSnapshot BuildPresentationSnapshot(List<GameNode> nodes)
        {
            var inventory = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in _gameState.Inventory.Items)
            {
                inventory[item.Key] = item.Value;
            }

            var reputation = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["mayor"] = _gameState.Get<int>("reputation:mayor"),
                ["workers"] = _gameState.Get<int>("reputation:workers"),
                ["elites"] = _gameState.Get<int>("reputation:elites"),
            };

            var actors = new List<ActorSnapshot>();
            foreach (var actor in _gameState.Team.Actors)
            {
                actors.Add(new ActorSnapshot
                {
                    Id = actor.Id,
                    Name = actor.Name,
                    Role = actor.Role,
                    Status = actor.Status,
                    Stress = actor.Stress,
                    SpentGrowthPoints = actor.SpentGrowthPoints,
                    Stats = new Dictionary<string, int>(actor.Stats),
                    ActionDice = actor.ActionDice.ToArray(),
                });
            }

            return new PresentationSnapshot
            {
                Nodes = nodes,
                Health = _gameState.Team.Health,
                MaxHealth = _gameState.Team.MaxHealth,
                Supplies = _gameState.Team.Supplies,
                MaxSupplies = _gameState.Team.MaxSupplies,
                GrowthLevel = _gameState.Team.GrowthLevel,
                Location = _gameState.Get<string>("location"),
                Inventory = inventory,
                Reputation = reputation,
                Actors = actors,
            };
        }

        private void CollectClocksRecursive(List<GameNode> nodes, List<GameClock> result)
        {
            foreach (var node in nodes)
            {
                result.AddRange(node.Clocks);
                CollectClocksRecursive(node.Children, result);
            }
        }

        private void RunOnActionRules()
        {
            ActiveInterpreter.Eval("(on-action)");
        }

        public void EndTurn()
        {
            _turnEndedDuringAction = true;
            ActiveInterpreter.Eval("(on-turn-end)");

            bool isInEncounter = !CurrentSceneName.Equals("world", StringComparison.OrdinalIgnoreCase);
            _gameState.Team.EndTurn(isInEncounter);

            RebuildRenderTree();
        }

        public ActionReport ExecuteAction(GameNode node, List<SlottedResource?> slots)
        {
            Debug.Assert(node.Resolve != null, "Cannot execute action on a node that has no resolve");
            if (node.Resolve.Type == ResolveType.Observe)
            {
                throw new InvalidOperationException("Observe actions must not be executed via ExecuteAction.");
            }

            var report = new ActionReport();

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

            var context = new ActionExecutionContext
            {
                ActorId = activeActorId,
                Mode = mode,
                SlottedResources = slots
            };
            _gameState.CurrentContext = context;
            _turnEndedDuringAction = false;

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

                    foreach (var s in slots)
                    {
                        if (s != null && s.Type == "item")
                        {
                            int cur = _gameState.Inventory.GetCount(s.ItemId);
                            _gameState.Inventory.SetCount(s.ItemId, Math.Max(0, cur - s.Qty));
                        }
                    }
                };

                if (node.Resolve.Type == ResolveType.Instant)
                {
                    report.Type = ActionType.Instant;
                    node.Resolve.Effect?.Invoke();
                    consumeResources();
                }
                else if (node.Resolve.Type == ResolveType.Roll)
                {
                    report.Type = ActionType.Roll;
                    int chosenDieVal = actorDieSlot != null ? actorDieSlot.Value : 1;
                    report.ChosenDieValue = chosenDieVal;

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
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported resolve type: {node.Resolve.Type}");
                }

                if (!_turnEndedDuringAction)
                {
                    RunOnActionRules();
                }
                else
                {
                    _turnEndedDuringAction = false;
                }
            }
            finally
            {
                _gameState.CurrentContext = null;
            }

            RebuildRenderTree();
            FillPresentationHints(report);
            return report;
        }

        private static void FillPresentationHints(ActionReport report)
        {
            var hints = new List<PresentationHint>
            {
                new PresentationHint
                {
                    Kind = PresentationHintKind.ExecuteProgress,
                    Text = "执行中...",
                    DurationSeconds = 0.3f,
                },
            };

            if (report.Type == ActionType.Roll)
            {
                hints.Add(new PresentationHint
                {
                    Kind = PresentationHintKind.RollDice,
                    DurationSeconds = 0f,
                });
            }

            if (!string.IsNullOrEmpty(report.AnimationTag))
            {
                hints.Add(new PresentationHint
                {
                    Kind = PresentationHintKind.PlayAnimation,
                    Tag = report.AnimationTag,
                    DurationSeconds = 0.5f,
                });
            }

            report.PresentationHints = hints;
        }
    }
}
