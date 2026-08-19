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

        public void ResetForNewGame()
        {
            _worldInterpreter = null;
            _encounterInterpreter = null;
            _encounterSceneName = string.Empty;
            _encounterCallback = null;
            _encounterEnded = false;
            _turnEndedDuringAction = false;
            _hasPendingSceneDiceRoll = false;
            _pendingSceneIsEncounter = false;
            CurrentRootNode = null;
            CurrentClocks.Clear();
            LatestSnapshot = new PresentationSnapshot();
            _gameState.ResetForNewGame();
            LoadScene("world");
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

                // 交锋自己的入场演出属于刚载入的交锋，而不是发起它的世界动作。
                // 若本次载入来自一个动作，这些步骤会写进该动作仍持有的 ActionReport；
                // Debug 直载时则由表现函数安全地忽略。
                _encounterInterpreter.Eval("(on-encounter-enter)");
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

            _gameState.Team.RollActionDice(isInEncounter, consumeHangover: false);
        }

        private void ApplyPendingSceneDiceRoll()
        {
            if (!_hasPendingSceneDiceRoll)
            {
                return;
            }

            _hasPendingSceneDiceRoll = false;
            _gameState.Team.RollActionDice(_pendingSceneIsEncounter, consumeHangover: false);
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

        /// <summary>最后一次交锋结算交回来的值。正式流程由回调消费，这里留一份供离线试跑读取。</summary>
        public object? LastEncounterResult { get; private set; }

        public void EndEncounter(object? result = null)
        {
            if (_encounterEnded) return;
            _encounterEnded = true;
            LastEncounterResult = result;

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
                Symbol.FromString("__end-turn!"),
                new NativeProcedure(args =>
                {
                    EndTurn();
                    return new None();
                }, "__end-turn!")
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

            // 3. Restore Team (health, 骰池状态, actor composure/stats)
            _gameState.PrepareForLoad();
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
            _gameState.Team.RollActionDice(isInEncounter: false, consumeHangover: false);

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

            if (rootNode.Clocks.Count > 0)
                throw new InvalidOperationException(
                    "根容器不能挂 :clocks：根节点不会被渲染为卡。请改用 clock-node 作为第一个子节点。");

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

            var relations = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["官僚"] = _gameState.Get<int>("relation:官僚"),
                ["劳工"] = _gameState.Get<int>("relation:劳工"),
                ["富商"] = _gameState.Get<int>("relation:富商"),
            };
            var relationUnlocks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var relationBandNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string faction in relations.Keys)
            {
                foreach (string tier in RelationScale.PositiveTiers)
                {
                    relationUnlocks[$"{faction}:{tier}"] =
                        _gameState.Get<string>($"relation-goal:{faction}:{tier}", "当前无新增动作");
                    relationBandNames[$"{faction}:{tier}"] =
                        _gameState.Get<string>($"relation-band-name:{faction}:{tier}", tier);
                }
            }

            bool isInEncounter = !CurrentSceneName.Equals("world", StringComparison.OrdinalIgnoreCase);
            var actors = new List<ActorSnapshot>();
            foreach (var actor in _gameState.Team.Actors)
            {
                actors.Add(new ActorSnapshot
                {
                    Id = actor.Id,
                    Name = actor.Name,
                    Role = actor.Role,
                    Status = actor.Status,
                    OnStage = TeamState.IsOnStage(actor, isInEncounter),
                    Composure = actor.Composure,
                    SpentGrowthPoints = actor.SpentGrowthPoints,
                    Stats = new Dictionary<string, int>(actor.Stats),
                    ActionSlotCount = actor.ActionSlotCount,
                    ActionDice = actor.ActionDice.ToArray(),
                    ActionDiceSlotIds = actor.ActionDiceSlotIds.ToArray(),
                    ActiveActionSlotStatuses = _gameState.Team.GetActiveActionSlotStatuses(actor),
                    PendingActionSlotStatuses = _gameState.Team.GetPendingActionSlotStatuses(actor),
                });
            }

            return new PresentationSnapshot
            {
                RootNode = rootNode,
                InjurySeverity = _gameState.Team.Injury.Severity,
                InjuryPart = _gameState.Team.Injury.Part,
                InjuryBandName = Injury.BandName(_gameState.Team.Injury.Band),
                InjurySkillName = _gameState.Team.Injury.SkillName,
                InjurySkillKey = _gameState.Team.Injury.Skill,
                InjurySkillPenalty = _gameState.Team.Injury.SkillPenalty,
                InjuryCostsActionDie = _gameState.Team.Injury.CostsActionDie,
                ScarModifiers = _gameState.Team.Scars.ModifiersBySkill(),
                ScarSummary = _gameState.Team.Scars.Describe(),
                Failure = _gameState.Failure,
                RestBlockers = _gameState.RestBlockers,
                GrowthLevel = _gameState.Team.GrowthLevel,
                WorldDay = _gameState.Get<int>("世界日", 1),
                Location = _gameState.Get<string>("location"),
                Inventory = inventory,
                Relations = relations,
                RelationUnlocks = relationUnlocks,
                RelationBandNames = relationBandNames,
                Actors = actors,
                IsInEncounter = isInEncounter,
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
            var firstPaths = new Dictionary<string, string>(StringComparer.Ordinal);
            AssertUniqueNodeNamesRecursive(rootNode, "", firstPaths);
        }

        private static void AssertUniqueNodeNamesRecursive(
            GameNode node,
            string parentPath,
            Dictionary<string, string> firstPaths)
        {
            string path = string.IsNullOrEmpty(parentPath)
                ? node.Name
                : $"{parentPath} > {node.Name}";
            if (firstPaths.TryGetValue(node.Name, out string? firstPath))
            {
                throw new InvalidOperationException(
                    $"重复节点名称: \"{node.Name}\"。节点名称在整棵渲染树中必须唯一。"
                    + $"\n首次出现: {firstPath}"
                    + $"\n再次出现: {path}");
            }
            firstPaths[node.Name] = path;
            foreach (var child in node.Children)
            {
                AssertUniqueNodeNamesRecursive(child, path, firstPaths);
            }
        }

        private static void RunOnActionRules(SchemeInterpreter actionInterpreter)
        {
            actionInterpreter.Eval("(on-action)");
        }

        public ActionReport EndTurn()
        {
            if (_isExecutingAction)
                _turnEndedDuringAction = true;
            bool isInEncounter = !CurrentSceneName.Equals("world", StringComparison.OrdinalIgnoreCase);
            bool ownsReport = _gameState.CurrentActionReport == null;
            var report = _gameState.CurrentActionReport ?? new ActionReport { Type = ActionType.Instant };
            if (ownsReport)
                _gameState.CurrentActionReport = report;
            try
            {
                int injuryBefore = _gameState.Team.Injury.Severity;
                int composureBefore = _gameState.Team.FindActor("player")?.Composure ?? 0;

                // 交锋不再每回合自动扣冷静。冷静缩到 2 点之后这条流失会让任何一场交锋
                // （了断第一幕光"老板到场"就 5 回合）在中途反复撞穿倒下线；而交锋的时间
                // 压力本来就由各自的钟表达（老板到场、拖走进度、危险钟），那些是可见的、
                // 每场不同的，比一条全局流失更好。冷静现在纯粹是"今天还能扛几次失败"。
                int automaticComposureDelta = (_gameState.Team.FindActor("player")?.Composure ?? 0) - composureBefore;
                int automaticInjuryDelta = _gameState.Team.Injury.Severity - injuryBefore;
                ActiveInterpreter.Eval("(on-turn-end)");

                bool stillInSameMode = isInEncounter == !CurrentSceneName.Equals("world", StringComparison.OrdinalIgnoreCase);
                if (stillInSameMode)
                {
                    _gameState.Team.RollActionDice(isInEncounter);
                }

                report.AddEffect(
                    ActionEffectKind.Composure, "冷静", automaticComposureDelta,
                    automaticComposureDelta > 0 ? ActionEffectTone.Positive : ActionEffectTone.Negative);

                // 伤势的正负与其他资源相反：数值涨上去是坏事。
                report.AddEffect(
                    ActionEffectKind.Injury, "伤势", automaticInjuryDelta,
                    automaticInjuryDelta > 0 ? ActionEffectTone.Negative : ActionEffectTone.Positive);
                if (automaticInjuryDelta > 0)
                {
                    report.AddNote("冷静击穿：你的手在抖，身体先一步承受了代价。");
                }

                RebuildRenderTree();
                if (ownsReport)
                {
                    FillPresentationHints(report);
                }
                return report;
            }
            finally
            {
                if (ownsReport)
                    _gameState.CurrentActionReport = null;
            }
        }

        public ActionReport UseEncounterConsumable(string itemId)
        {
            if (CurrentSceneName.Equals("world", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Encounter consumables can only be used during an encounter.");

            int restoreAmount = itemId switch
            {
                "香烟" => 2,
                "酒" => 3,
                _ => throw new ArgumentException($"Unsupported encounter consumable '{itemId}'.", nameof(itemId))
            };
            if (_gameState.Inventory.GetCount(itemId) < 1)
                throw new InvalidOperationException($"Insufficient inventory: '{itemId}'.");

            var report = new ActionReport { Type = ActionType.Instant };
            _gameState.Inventory.SetCount(itemId, _gameState.Inventory.GetCount(itemId) - 1);
            report.AddEffect(ActionEffectKind.Item, itemId, -1, ActionEffectTone.Negative);

            var player = _gameState.Team.FindActor("player")
                ?? throw new InvalidOperationException("Protagonist is missing from the team.");
            int composureBefore = player.Composure;
            _gameState.Team.RestoreComposure("player", restoreAmount);
            int composureDelta = player.Composure - composureBefore;
            report.AddEffect(ActionEffectKind.Composure, "冷静", composureDelta, ActionEffectTone.Positive);

            if (itemId == "酒")
            {
                _gameState.Team.ApplyHangover();
                report.AddNote("酒劲会留到下一次城市骰池：一格会带宿醉降质。");
            }

            RebuildRenderTree();
            FillPresentationHints(report);
            return report;
        }

        public ActionReport ExecuteAction(GameNode node, List<SlottedResource?> slots)
        {
            if (node.Disabled)
                throw new InvalidOperationException($"Node '{node.Name}' is disabled and cannot be executed.");
            Debug.Assert(node.Resolve != null, "Cannot execute action on a node that has no resolve");
            if (node.Resolve.Type == ResolveType.Observe)
            {
                throw new InvalidOperationException("Observe actions must not be executed via ExecuteAction.");
            }

            // 一个 action 的后处理规则属于发起该 action 的场景。action 本身可以
            // start/end encounter 并切换 ActiveInterpreter，但不能因此改写规则归属。
            var actionInterpreter = ActiveInterpreter;
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

                        int slotId = slot.DieIndex >= 0 ? slot.DieIndex : slot.SourceIndex;
                        int idx = a.ActionDiceSlotIds.IndexOf(slotId);
                        if (idx < 0)
                        {
                            throw new InvalidOperationException($"Action slot {slotId} has no available die for actor '{actorId}'.");
                        }

                        if (a.ActionDice[idx] != slot.Value)
                        {
                            throw new InvalidOperationException($"Die value mismatch: slotted die has value {slot.Value}, but actor's slot {slotId} has value {a.ActionDice[idx]}.");
                        }

                        if (!usedDicePerActor.ContainsKey(actorId))
                        {
                            usedDicePerActor[actorId] = new HashSet<int>();
                        }
                        if (usedDicePerActor[actorId].Contains(slotId))
                        {
                            throw new InvalidOperationException($"Die in slot {slotId} for actor '{actorId}' is slotted more than once.");
                        }
                        usedDicePerActor[actorId].Add(slotId);
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

            // 谁能出手由「他在不在队里」决定（TeamState.IsOnStage），不在这里按场景类型另判一次。
            string mode = CurrentSceneName.Equals("world", StringComparison.OrdinalIgnoreCase) ? "world" : "encounter";

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
                            foreach (var s in sorted)
                            {
                                int slotId = s.DieIndex >= 0 ? s.DieIndex : s.SourceIndex;
                                int idx = a.ActionDiceSlotIds.IndexOf(slotId);
                                if (idx >= 0)
                                {
                                    a.ActionDice.RemoveAt(idx);
                                    a.ActionDiceSlotIds.RemoveAt(idx);
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
                    int skillLevel;
                    if (actor.Stats.TryGetValue(skillName, out var sVal))
                    {
                        skillLevel = sVal;
                    }
                    else
                    {
                        throw new InvalidOperationException($"未知技能/属性: {skillName} 对于角色 {actor.Id}");
                    }

                    var rand = GameRandom.Instance;

                    var modifiers = new List<DifficultyModifierInfo>(node.Resolve.DifficultyModifiers);
                    // 伤势只压主角被打中的那一项能力，并且走和「势力敌视 −1」「非法 −2」
                    // 同一条可见修正——玩家在投骰前就看得见它，不做暗扣。
                    var injuryMod = _gameState.Team.Injury.ModifierFor(actor.Role, skillName);
                    if (injuryMod != null) modifiers.Add(injuryMod);
                    // 旧伤同理：永久，治不掉，同样摆在明面上。
                    var scarMod = _gameState.Team.Scars.ModifierFor(actor.Role, skillName);
                    if (scarMod != null) modifiers.Add(scarMod);
                    int modifierSum = 0;
                    foreach (var mod in modifiers)
                    {
                        modifierSum += mod.Value;
                    }
                    report.DifficultyModifiers = modifiers;
                    report.SkillLevel = skillLevel;
                    report.ModifierTotal = modifierSum;

                    int fateDie = rand.Next(1, 7);
                    report.PreparedValue = FateStrip.PreparedValue(chosenDieVal, skillLevel, modifierSum);
                    report.FateDieValue = fateDie;
                    report.Outcome = FateStrip.Resolve(chosenDieVal, skillLevel, modifierSum, fateDie);

                    if (report.Outcome == RollOutcome.Fail)
                    {
                        node.Resolve.FailOutcome?.Effect?.Invoke();
                        ApplyOutcomePresentation(report, node.Resolve.FailOutcome);
                    }
                    else if (report.Outcome == RollOutcome.Neutral)
                    {
                        node.Resolve.NeutralOutcome?.Effect?.Invoke();
                        ApplyOutcomePresentation(report, node.Resolve.NeutralOutcome);
                    }
                    else
                    {
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
                    RunOnActionRules(actionInterpreter);
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
