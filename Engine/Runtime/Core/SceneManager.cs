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
        private bool _isExecutingAction;
        private bool _hasPendingSceneDiceRoll;
        private bool _pendingSceneIsEncounter;
        private Procedure? _encounterCallback;
        private bool _encounterEnded;

        public event Action? OnSceneLoaded;
        public event Action? OnWorldRefreshed;

        public GameNode? CurrentRootNode { get; private set; }
        public List<GameClock> CurrentClocks { get; private set; } = new List<GameClock>();
        public PresentationSnapshot LatestSnapshot { get; private set; } = new PresentationSnapshot();

        public SchemeInterpreter ActiveInterpreter => _encounterInterpreter ?? _worldInterpreter ?? throw new InvalidOperationException("No active interpreter");
        public string CurrentSceneName => _encounterInterpreter != null ? _encounterSceneName : "world";
        public GameState GameState => _gameState;

        public SceneManager(GameState gameState, IScriptLoader loader)
        {
            _gameState = gameState;
            _loader = loader;
        }

        // Explicit scene switch entry point for the UI. Scene transitions are an
        // explicit action — they are not triggered as a side effect of writing the
        // "location" global. No-op when already in the requested scene.
        public void GoToLocation(string sceneName)
        {
            if (sceneName != CurrentSceneName)
                LoadScene(sceneName);
        }

        // The world is a single shared interpreter; everything else is an encounter.
        private static bool IsWorldScene(string sceneName)
        {
            return sceneName == "world" || sceneName == "world/world";
        }

        public void LoadScene(string sceneName)
        {
            if (IsWorldScene(sceneName))
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

            RollSceneDice(!IsWorldScene(sceneName));

            RebuildRenderTree();
            NotifySceneLoaded();
        }

        private void RollSceneDice(bool isInEncounter)
        {
            if (_isExecutingAction)
            {
                _hasPendingSceneDiceRoll = true;
                _pendingSceneIsEncounter = isInEncounter;
                return;
            }

            _gameState.Team.RollActionDice(isInEncounter);
        }

        private void ApplyPendingSceneDiceRoll()
        {
            if (!_hasPendingSceneDiceRoll)
            {
                return;
            }

            _hasPendingSceneDiceRoll = false;
            _gameState.Team.RollActionDice(_pendingSceneIsEncounter);
        }

        private void NotifySceneLoaded()
        {
            if (!_isExecutingAction)
            {
                OnSceneLoaded?.Invoke();
            }
        }

        public void StartEncounter(string name)
        {
            _encounterEnded = false;
            LoadScene(name);
        }

        public void EndEncounter(object? result = null)
        {
            if (_encounterEnded) return;
            _encounterEnded = true;

            var cb = _encounterCallback;
            _encounterCallback = null;

            if (cb != null)
                cb.Call(new List<object> { result ?? Symbol.FromString("none") });

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
                    string name = SchemeValue.AsId(args[0]);
                    _encounterCallback = args.Count > 1 ? args[1] as Procedure : null;
                    StartEncounter(name);
                    return new None();
                }, "start-encounter")
            );

            interpreter.RawInterpreter.DefineGlobal(
                Symbol.FromString("end-encounter"),
                new NativeProcedure(args =>
                {
                    var result = args.Count > 0 ? args[0] : null;
                    EndEncounter(result);
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

        public void SaveGame() => SaveGame(SaveManager.DefaultSavePath);
        public void LoadGame() => LoadGame(SaveManager.DefaultSavePath);

        public void SaveGame(string filePath)
        {
            if (_encounterInterpreter != null)
                throw new InvalidOperationException("Cannot save during an encounter. End the encounter first.");

            var globals = _gameState.GetPureGlobals();
            globals.Remove("location"); // reconstructed on load

            var data = new SaveData
            {
                Globals   = globals,
                Team      = _gameState.Team.Serialize(),
                Inventory = new Dictionary<string, int>(_gameState.Inventory.Items),
                WorldData = _worldInterpreter?.Eval("(world-save)"),
                SaveTime  = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            };
            SaveManager.Write(filePath, data);
        }

        public void LoadGame(string filePath)
        {
            var data = SaveManager.Read(filePath);

            // 1. Force exit any active encounter
            _encounterInterpreter = null;
            _encounterSceneName   = string.Empty;
            _encounterCallback    = null;

            // 2. Ensure world interpreter exists (creates it if not yet initialized)
            if (_worldInterpreter == null)
            {
                _worldInterpreter = new SchemeInterpreter(_gameState, _loader);
                RegisterEncounterBridges(_worldInterpreter);
                _worldInterpreter.LoadFile("scenes/world/world.scm");
            }

            // 3. Restore Team (health, supplies, actor stress/stats)
            _gameState.Team.ApplySaveData(data.Team);

            // 4. Restore Inventory (replaces entirely — no stale items left over)
            _gameState.Inventory.ApplySaveData(data.Inventory);

            // 5. Replace pure globals (chapter, reputation, etc.).
            //    ReplacePureGlobals clears _states and resets location=world; the
            //    render tree is rebuilt explicitly at the end of this method.
            _gameState.ReplacePureGlobals(data.Globals);

            // 6. Restore Scheme world state (must run after C# state is fully set)
            if (data.WorldData != null)
            {
                _worldInterpreter.RawInterpreter.DefineGlobal(
                    Symbol.FromString("__world-load-data"), data.WorldData);
                _worldInterpreter.Eval("(world-load! __world-load-data)");
            }

            // 7. Roll fresh action dice for world mode
            _gameState.Team.RollActionDice(isInEncounter: false);

            // 8. Rebuild render tree and notify UI
            RebuildRenderTree();
            OnSceneLoaded?.Invoke();
        }

        public void Refresh() => RebuildRenderTree();

        public void RebuildRenderTree()
        {
            var active = ActiveInterpreter;
            var rawData = active.Eval("(get-render-data)");
            var rootNode = NodeConverter.ConvertSingle(rawData, active.RawInterpreter);

            AssertUniqueNodeNames(rootNode);
            CurrentRootNode = rootNode;

            var flatClocks = new List<GameClock>();
            CollectClocksRecursive(rootNode, flatClocks);
            CurrentClocks = flatClocks;

            LatestSnapshot = BuildPresentationSnapshot(rootNode);
            
            OnWorldRefreshed?.Invoke();
        }

        private PresentationSnapshot BuildPresentationSnapshot(GameNode rootNode)
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
                RootNode = rootNode,
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

        private void CollectClocksRecursive(GameNode node, List<GameClock> result)
        {
            result.AddRange(node.Clocks);
            foreach (var child in node.Children)
                CollectClocksRecursive(child, result);
        }

        private static void AssertUniqueNodeNames(GameNode rootNode)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            AssertUniqueNodeNamesRecursive(rootNode, seen);
        }

        private static void AssertUniqueNodeNamesRecursive(GameNode node, HashSet<string> seen)
        {
            Debug.Assert(!seen.Contains(node.Name),
                $"重复节点名称: \"{node.Name}\"。节点名称在整棵渲染树中必须唯一。");
            seen.Add(node.Name);
            foreach (var child in node.Children)
            {
                AssertUniqueNodeNamesRecursive(child, seen);
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
            _gameState.CurrentActionReport = report;
            _turnEndedDuringAction = false;
            bool wasExecutingAction = _isExecutingAction;
            _isExecutingAction = true;

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
                    node.Resolve.Outcome?.Effect?.Invoke();
                    ApplyOutcomePresentation(report, node.Resolve.Outcome);
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

                    var rand = GameRandom.Instance;
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
                        node.Resolve.FailOutcome?.Effect?.Invoke();
                        ApplyOutcomePresentation(report, node.Resolve.FailOutcome);
                    }
                    else if (modifiedValue <= 4)
                    {
                        report.Outcome = RollOutcome.Neutral;
                        node.Resolve.NeutralOutcome?.Effect?.Invoke();
                        ApplyOutcomePresentation(report, node.Resolve.NeutralOutcome);
                    }
                    else
                    {
                        report.Outcome = RollOutcome.Success;
                        node.Resolve.SuccessOutcome?.Effect?.Invoke();
                        ApplyOutcomePresentation(report, node.Resolve.SuccessOutcome);
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
                _gameState.CurrentActionReport = null;
                _isExecutingAction = wasExecutingAction;
            }

            ApplyPendingSceneDiceRoll();
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

            report.PresentationHints = hints;
        }

        private static void ApplyOutcomePresentation(ActionReport report, ActionOutcome? outcome)
        {
            if (outcome == null || !outcome.HasText)
            {
                return;
            }

            report.OutcomePresentation = outcome.Presentation;
        }
    }
}
